using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Newtonsoft.Json;
using TradingPlatform.CodeAnalysis.Integration.Models;

namespace TradingPlatform.CodeAnalysis.Integration
{
    /// <summary>
    /// Service that provides real-time feedback to AI assistants (Claude and Augment)
    /// about code quality issues detected by analyzers.
    /// </summary>
    public class RealTimeFeedbackService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly MessageTranslator _translator;
        private readonly ConcurrentQueue<DiagnosticInfo> _diagnosticQueue;
        private readonly Timer _batchTimer;
        private readonly SemaphoreSlim _sendSemaphore;
        private readonly FeedbackConfiguration _configuration;
        private bool _disposed;

        public RealTimeFeedbackService(FeedbackConfiguration configuration = null)
        {
            _configuration = configuration ?? FeedbackConfiguration.LoadFromEnvironment();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _translator = new MessageTranslator();
            _diagnosticQueue = new ConcurrentQueue<DiagnosticInfo>();
            _sendSemaphore = new SemaphoreSlim(1, 1);
            
            // Batch diagnostics every 500ms to avoid overwhelming the endpoints
            _batchTimer = new Timer(ProcessBatch, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            if (!string.IsNullOrEmpty(_configuration.ClaudeApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _configuration.ClaudeApiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            }
        }

        public void EnqueueDiagnostic(Diagnostic diagnostic, string filePath, string projectName)
        {
            if (!_configuration.EnableRealTimeFeedback) return;

            var diagnosticInfo = new DiagnosticInfo
            {
                File = filePath,
                Line = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1,
                Column = diagnostic.Location.GetLineSpan().StartLinePosition.Character + 1,
                Rule = diagnostic.Id,
                Message = diagnostic.GetMessage(),
                Severity = diagnostic.Severity.ToString(),
                Category = diagnostic.Descriptor.Category,
                ProjectName = projectName,
                Timestamp = DateTime.UtcNow
            };

            _diagnosticQueue.Enqueue(diagnosticInfo);
        }

        private async void ProcessBatch(object state)
        {
            if (_diagnosticQueue.IsEmpty || !await _sendSemaphore.WaitAsync(0))
                return;

            try
            {
                var batch = new List<DiagnosticInfo>();
                while (_diagnosticQueue.TryDequeue(out var diagnostic) && batch.Count < 50)
                {
                    batch.Add(diagnostic);
                }

                if (batch.Any())
                {
                    await SendBatchToEndpoints(batch);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the analyzer
                Console.Error.WriteLine($"Failed to send diagnostic feedback: {ex.Message}");
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        private async Task SendBatchToEndpoints(List<DiagnosticInfo> diagnostics)
        {
            var tasks = new List<Task>();

            if (_configuration.EnableClaudeFeedback)
            {
                tasks.Add(SendToClaudeAsync(diagnostics));
            }

            if (_configuration.EnableAugmentFeedback)
            {
                tasks.Add(SendToAugmentAsync(diagnostics));
            }

            if (_configuration.EnableLocalFile)
            {
                tasks.Add(WriteToLocalFileAsync(diagnostics));
            }

            await Task.WhenAll(tasks);
        }

        private async Task SendToClaudeAsync(List<DiagnosticInfo> diagnostics)
        {
            if (string.IsNullOrEmpty(_configuration.ClaudeApiKey)) return;

            try
            {
                var messages = diagnostics.Select(d => _translator.TranslateForClaude(d)).ToList();
                var requestBody = new
                {
                    model = "claude-3-sonnet-20240229",
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = JsonConvert.SerializeObject(new
                            {
                                type = "code_analysis_batch",
                                timestamp = DateTime.UtcNow,
                                diagnostics = messages
                            })
                        }
                    },
                    max_tokens = 1
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_configuration.ClaudeEndpoint, content);
                // We don't need to process the response for feedback
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to send to Claude: {ex.Message}");
            }
        }

        private async Task SendToAugmentAsync(List<DiagnosticInfo> diagnostics)
        {
            if (string.IsNullOrEmpty(_configuration.AugmentApiKey)) return;

            try
            {
                var messages = diagnostics.Select(d => _translator.TranslateForAugment(d)).ToList();
                
                var json = JsonConvert.SerializeObject(new
                {
                    events = messages
                });

                var request = new HttpRequestMessage(HttpMethod.Post, _configuration.AugmentEndpoint)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                
                request.Headers.Add("Authorization", $"Bearer {_configuration.AugmentApiKey}");

                var response = await _httpClient.SendAsync(request);
                // We don't need to process the response for feedback
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to send to Augment: {ex.Message}");
            }
        }

        private async Task WriteToLocalFileAsync(List<DiagnosticInfo> diagnostics)
        {
            try
            {
                var outputPath = Path.Combine(_configuration.LocalOutputPath, "diagnostics.json");
                var existingDiagnostics = new List<DiagnosticInfo>();

                if (File.Exists(outputPath))
                {
                    var existingJson = await File.ReadAllTextAsync(outputPath);
                    existingDiagnostics = JsonConvert.DeserializeObject<List<DiagnosticInfo>>(existingJson) ?? new List<DiagnosticInfo>();
                }

                existingDiagnostics.AddRange(diagnostics);

                // Keep only last 1000 diagnostics
                if (existingDiagnostics.Count > 1000)
                {
                    existingDiagnostics = existingDiagnostics.Skip(existingDiagnostics.Count - 1000).ToList();
                }

                var json = JsonConvert.SerializeObject(existingDiagnostics, Formatting.Indented);
                await File.WriteAllTextAsync(outputPath, json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write to local file: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _batchTimer?.Dispose();
            _sendSemaphore?.Dispose();
            _httpClient?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Configuration for real-time feedback service.
    /// </summary>
    public class FeedbackConfiguration
    {
        public bool EnableRealTimeFeedback { get; set; } = true;
        public bool EnableClaudeFeedback { get; set; } = true;
        public bool EnableAugmentFeedback { get; set; } = true;
        public bool EnableLocalFile { get; set; } = true;

        public string ClaudeEndpoint { get; set; } = "https://api.anthropic.com/v1/messages";
        public string ClaudeApiKey { get; set; }

        public string AugmentEndpoint { get; set; } = "https://api.augmentcode.com/v1/feedback";
        public string AugmentApiKey { get; set; }

        public string LocalOutputPath { get; set; } = Path.Combine(Environment.CurrentDirectory, "Output");

        public static FeedbackConfiguration LoadFromEnvironment()
        {
            return new FeedbackConfiguration
            {
                EnableRealTimeFeedback = GetBoolEnv("CODEANALYSIS_REALTIME_FEEDBACK", true),
                EnableClaudeFeedback = GetBoolEnv("CODEANALYSIS_CLAUDE_ENABLED", true),
                EnableAugmentFeedback = GetBoolEnv("CODEANALYSIS_AUGMENT_ENABLED", true),
                EnableLocalFile = GetBoolEnv("CODEANALYSIS_LOCAL_FILE", true),
                ClaudeApiKey = Environment.GetEnvironmentVariable("CLAUDE_API_KEY"),
                AugmentApiKey = Environment.GetEnvironmentVariable("AUGMENT_API_KEY"),
                LocalOutputPath = Environment.GetEnvironmentVariable("CODEANALYSIS_OUTPUT_PATH") ?? 
                                Path.Combine(Environment.CurrentDirectory, "Output")
            };
        }

        private static bool GetBoolEnv(string name, bool defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }
    }
}