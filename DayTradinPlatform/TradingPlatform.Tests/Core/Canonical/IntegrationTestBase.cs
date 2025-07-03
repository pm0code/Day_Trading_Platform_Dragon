using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using Moq;

namespace TradingPlatform.Tests.Core.Canonical
{
    /// <summary>
    /// Base class for all integration tests
    /// Provides service container setup and common test infrastructure
    /// </summary>
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected IServiceProvider ServiceProvider { get; private set; }
        protected IServiceCollection Services { get; private set; }
        protected Mock<ITradingLogger> MockLogger { get; private set; }
        protected ITestOutputHelper Output { get; }
        
        protected IntegrationTestBase(ITestOutputHelper output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Services = new ServiceCollection();
            MockLogger = new Mock<ITradingLogger>();
            
            SetupMockLogger();
        }
        
        public virtual async Task InitializeAsync()
        {
            // Configure services
            ConfigureServices(Services);
            
            // Build service provider
            ServiceProvider = Services.BuildServiceProvider();
            
            // Allow derived classes to perform additional initialization
            await OnInitializeAsync();
        }
        
        public virtual async Task DisposeAsync()
        {
            await OnDisposeAsync();
            
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        
        /// <summary>
        /// Configure services for the test. Override to add test-specific services.
        /// </summary>
        protected abstract void ConfigureServices(IServiceCollection services);
        
        /// <summary>
        /// Additional initialization logic. Override if needed.
        /// </summary>
        protected virtual Task OnInitializeAsync() => Task.CompletedTask;
        
        /// <summary>
        /// Additional cleanup logic. Override if needed.
        /// </summary>
        protected virtual Task OnDisposeAsync() => Task.CompletedTask;
        
        /// <summary>
        /// Setup mock logger to output to test console
        /// </summary>
        private void SetupMockLogger()
        {
            MockLogger.Setup(x => x.LogTrace(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, object[]>((msg, args) => LogToOutput("TRACE", msg, args));
                
            MockLogger.Setup(x => x.LogDebug(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, object[]>((msg, args) => LogToOutput("DEBUG", msg, args));
                
            MockLogger.Setup(x => x.LogInfo(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, object[]>((msg, args) => LogToOutput("INFO", msg, args));
                
            MockLogger.Setup(x => x.LogWarning(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, object[]>((msg, args) => LogToOutput("WARN", msg, args));
                
            MockLogger.Setup(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<object[]>()))
                .Callback<string, Exception, object[]>((msg, ex, args) => 
                {
                    LogToOutput("ERROR", msg, args);
                    if (ex != null)
                    {
                        Output.WriteLine($"  Exception: {ex.Message}");
                        Output.WriteLine($"  StackTrace: {ex.StackTrace}");
                    }
                });
                
            MockLogger.Setup(x => x.LogCritical(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<object[]>()))
                .Callback<string, Exception, object[]>((msg, ex, args) => 
                {
                    LogToOutput("CRITICAL", msg, args);
                    if (ex != null)
                    {
                        Output.WriteLine($"  Exception: {ex.Message}");
                        Output.WriteLine($"  StackTrace: {ex.StackTrace}");
                    }
                });
                
            MockLogger.Setup(x => x.LogPerformance(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<object[]>()))
                .Callback<string, long, object[]>((operation, ms, args) => 
                    Output.WriteLine($"[PERF] {operation} completed in {ms}ms"));
                    
            MockLogger.Setup(x => x.LogMetric(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>()))
                .Callback<string, object, string>((name, value, unit) => 
                    Output.WriteLine($"[METRIC] {name}: {value} {unit ?? ""}"));
        }
        
        private void LogToOutput(string level, string message, params object[] args)
        {
            try
            {
                var formattedMessage = args?.Length > 0 
                    ? string.Format(message, args) 
                    : message;
                    
                Output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{level}] {formattedMessage}");
            }
            catch
            {
                // Fallback if formatting fails
                Output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{level}] {message}");
            }
        }
        
        /// <summary>
        /// Assert that an async operation completes within the specified time
        /// </summary>
        protected async Task AssertCompletesWithinAsync(int milliseconds, Func<Task> operation)
        {
            var task = operation();
            var delayTask = Task.Delay(milliseconds);
            
            var completedTask = await Task.WhenAny(task, delayTask);
            
            if (completedTask == delayTask)
            {
                throw new TimeoutException($"Operation did not complete within {milliseconds}ms");
            }
            
            await task; // Ensure any exceptions are propagated
        }
        
        /// <summary>
        /// Verify logger was called with specific parameters
        /// </summary>
        protected void VerifyLoggerCalled(string level, Times times)
        {
            switch (level.ToLower())
            {
                case "info":
                    MockLogger.Verify(x => x.LogInfo(It.IsAny<string>(), It.IsAny<object[]>()), times);
                    break;
                case "warning":
                    MockLogger.Verify(x => x.LogWarning(It.IsAny<string>(), It.IsAny<object[]>()), times);
                    break;
                case "error":
                    MockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<object[]>()), times);
                    break;
                case "debug":
                    MockLogger.Verify(x => x.LogDebug(It.IsAny<string>(), It.IsAny<object[]>()), times);
                    break;
            }
        }
    }
}