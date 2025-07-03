using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingPlatform.SecureConfiguration.Core;

namespace TradingPlatform.SecureConfiguration.Builders
{
    /// <summary>
    /// Fluent builder for USB device export operations on Windows 11
    /// </summary>
    public class UsbExportBuilder
    {
        private readonly ILogger _logger;
        private readonly WindowsDeviceExport _deviceExport;
        private UsbDeviceInfo? _selectedDevice;
        private string? _password;
        private UsbExportOptions _options = new();
        
        public UsbExportBuilder(ILogger logger)
        {
            _logger = logger;
            _deviceExport = new WindowsDeviceExport(logger);
        }

        /// <summary>
        /// Start building a USB export operation
        /// </summary>
        public static UsbExportBuilder Create(ILogger logger) => new(logger);

        /// <summary>
        /// Automatically detect and select the first available USB device
        /// </summary>
        public async Task<UsbExportBuilder> WithAutoDetectedUsbAsync()
        {
            var devices = await _deviceExport.DetectUsbDevicesAsync();
            
            if (!devices.Any())
            {
                throw new InvalidOperationException("No USB devices detected");
            }
            
            _selectedDevice = devices.First();
            _logger.LogInformation("Auto-selected USB device: {Device}", _selectedDevice.DisplayName);
            
            return this;
        }

        /// <summary>
        /// Select a specific USB device
        /// </summary>
        public UsbExportBuilder WithUsbDevice(UsbDeviceInfo device)
        {
            _selectedDevice = device ?? throw new ArgumentNullException(nameof(device));
            return this;
        }

        /// <summary>
        /// Interactively select a USB device
        /// </summary>
        public async Task<UsbExportBuilder> WithInteractiveUsbSelectionAsync()
        {
            var devices = await _deviceExport.DetectUsbDevicesAsync();
            
            if (!devices.Any())
            {
                throw new InvalidOperationException("No USB devices detected");
            }
            
            Console.WriteLine("\nAvailable USB Devices:");
            Console.WriteLine("======================");
            
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                Console.WriteLine($"\n{i + 1}. {device.DisplayName}");
                Console.WriteLine($"   Model: {device.Model}");
                Console.WriteLine($"   Serial: {device.SerialNumber}");
                Console.WriteLine($"   File System: {device.FileSystem}");
                Console.WriteLine($"   Free Space: {device.FreeSpaceGB}");
                Console.WriteLine($"   Total Size: {device.TotalSizeGB}");
                
                if (device.IsEncrypted)
                {
                    Console.WriteLine("   🔐 BitLocker Encrypted");
                }
            }
            
            Console.Write("\nSelect device (1-{0}): ", devices.Count);
            if (int.TryParse(Console.ReadLine(), out var selection) && 
                selection >= 1 && selection <= devices.Count)
            {
                _selectedDevice = devices[selection - 1];
                Console.WriteLine($"\n✓ Selected: {_selectedDevice.DisplayName}");
            }
            else
            {
                throw new InvalidOperationException("Invalid device selection");
            }
            
            return this;
        }

        /// <summary>
        /// Add password protection
        /// </summary>
        public UsbExportBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        /// <summary>
        /// Interactively set password
        /// </summary>
        public UsbExportBuilder WithInteractivePassword()
        {
            Console.Write("\nEnter password for export (optional, press Enter to skip): ");
            var password = ReadPassword();
            
            if (!string.IsNullOrEmpty(password))
            {
                Console.Write("Confirm password: ");
                var confirm = ReadPassword();
                
                if (password != confirm)
                {
                    throw new InvalidOperationException("Passwords do not match");
                }
                
                _password = password;
                Console.WriteLine("✓ Password protection enabled");
            }
            else
            {
                Console.WriteLine("✓ No password protection");
            }
            
            return this;
        }

        /// <summary>
        /// Bind export to specific USB device (can only be imported from same device)
        /// </summary>
        public UsbExportBuilder BindToDevice(bool bind = true)
        {
            _options.BindToSpecificUsb = bind;
            return this;
        }

        /// <summary>
        /// Set export expiry
        /// </summary>
        public UsbExportBuilder WithExpiry(TimeSpan expiry)
        {
            _options.ExpiryTime = DateTime.UtcNow.Add(expiry);
            return this;
        }

        /// <summary>
        /// Set export options
        /// </summary>
        public UsbExportBuilder WithOptions(Action<UsbExportOptions> configure)
        {
            configure?.Invoke(_options);
            return this;
        }

        /// <summary>
        /// Export configuration to selected USB device
        /// </summary>
        public async Task<ExportResult> ExportAsync(string configPath, string keyPath)
        {
            if (_selectedDevice == null)
            {
                throw new InvalidOperationException("No USB device selected");
            }
            
            _logger.LogInformation("Starting USB export to {Device}", _selectedDevice.DisplayName);
            
            // Show export summary
            Console.WriteLine("\n📤 Export Summary:");
            Console.WriteLine("==================");
            Console.WriteLine($"Device: {_selectedDevice.DisplayName}");
            Console.WriteLine($"Password Protected: {(_password != null ? "Yes" : "No")}");
            Console.WriteLine($"Device Binding: {(_options.BindToSpecificUsb ? "Yes" : "No")}");
            
            if (_options.ExpiryTime.HasValue)
            {
                Console.WriteLine($"Expires: {_options.ExpiryTime.Value.ToLocalTime():yyyy-MM-dd HH:mm}");
            }
            
            Console.Write("\nProceed with export? (Y/N): ");
            if (Console.ReadLine()?.ToUpperInvariant() != "Y")
            {
                return ExportResult.Failure("Export cancelled by user");
            }
            
            // Perform export
            var result = await _deviceExport.ExportToUsbDeviceAsync(
                configPath, keyPath, _selectedDevice, _password, _options);
            
            if (result.IsSuccess)
            {
                Console.WriteLine($"\n✅ Export completed successfully!");
                Console.WriteLine($"📁 Location: {result.ExportInfo?.ExportPath}");
                Console.WriteLine($"💾 Size: {result.ExportInfo?.FileSizeBytes / 1024:N0} KB");
                
                if (_options.PrepareForSafeEject)
                {
                    Console.WriteLine("\n⚠️  You can now safely remove the USB device");
                }
            }
            else
            {
                Console.WriteLine($"\n❌ Export failed: {result.ErrorMessage}");
            }
            
            return result;
        }

        /// <summary>
        /// Get list of available USB devices
        /// </summary>
        public async Task<List<UsbDeviceInfo>> GetAvailableDevicesAsync()
        {
            return await _deviceExport.DetectUsbDevicesAsync();
        }

        private string ReadPassword()
        {
            var password = "";
            ConsoleKeyInfo key;
            
            do
            {
                key = Console.ReadKey(intercept: true);
                
                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
            }
            while (key.Key != ConsoleKey.Enter);
            
            Console.WriteLine();
            return password;
        }
    }

    /// <summary>
    /// Extension methods for easy USB export
    /// </summary>
    public static class UsbExportExtensions
    {
        /// <summary>
        /// Export configuration to USB with interactive device selection
        /// </summary>
        public static async Task<ExportResult> ExportToUsbAsync(
            this ISecureConfiguration config,
            ILogger logger,
            string? password = null)
        {
            // Get config paths
            var configPath = GetConfigPath(config);
            var keyPath = GetKeyPath(config);
            
            var builder = await UsbExportBuilder.Create(logger)
                .WithInteractiveUsbSelectionAsync();
            
            if (!string.IsNullOrEmpty(password))
            {
                builder.WithPassword(password);
            }
            else
            {
                builder.WithInteractivePassword();
            }
            
            return await builder
                .WithOptions(opts =>
                {
                    opts.CreateReadme = true;
                    opts.CreateVerificationFile = true;
                    opts.UseTimestampedFolder = true;
                })
                .ExportAsync(configPath, keyPath);
        }

        /// <summary>
        /// Quick export to first available USB device
        /// </summary>
        public static async Task<ExportResult> QuickExportToUsbAsync(
            this ISecureConfiguration config,
            ILogger logger,
            string? password = null,
            TimeSpan? expiry = null)
        {
            var configPath = GetConfigPath(config);
            var keyPath = GetKeyPath(config);
            
            var builder = await UsbExportBuilder.Create(logger)
                .WithAutoDetectedUsbAsync();
            
            if (!string.IsNullOrEmpty(password))
            {
                builder.WithPassword(password);
            }
            
            if (expiry.HasValue)
            {
                builder.WithExpiry(expiry.Value);
            }
            
            return await builder.ExportAsync(configPath, keyPath);
        }

        private static string GetConfigPath(ISecureConfiguration config)
        {
            // This would need to be exposed by the interface or use reflection
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TradingPlatform", // This should be dynamic based on app name
                "SecureConfig"
            );
            
            return Path.Combine(appDataPath, "config.encrypted");
        }

        private static string GetKeyPath(ISecureConfiguration config)
        {
            var configPath = GetConfigPath(config);
            return Path.ChangeExtension(configPath, ".key");
        }
    }
}