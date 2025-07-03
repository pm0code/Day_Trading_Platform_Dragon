using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TradingPlatform.SecureConfiguration.Builders;
using TradingPlatform.SecureConfiguration.Core;
using TradingPlatform.SecureConfiguration.Implementations;

namespace TradingPlatform.Tests.SecureConfiguration
{
    /// <summary>
    /// Integration tests for complete USB export/import workflows
    /// Tests end-to-end scenarios to ensure the feature works reliably
    /// </summary>
    public class UsbExportImportIntegrationTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<ILogger<WindowsDeviceExport>> _mockDeviceLogger;
        private readonly Mock<ILogger<ApiKeyConfiguration>> _mockConfigLogger;
        private readonly string _testDataPath;
        private readonly string _simulatedUsbPath;
        private readonly List<string> _tempFiles;

        public UsbExportImportIntegrationTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockDeviceLogger = new Mock<ILogger<WindowsDeviceExport>>();
            _mockConfigLogger = new Mock<ILogger<ApiKeyConfiguration>>();
            
            _testDataPath = Path.Combine(Path.GetTempPath(), $"UsbIntegrationTests_{Guid.NewGuid()}");
            _simulatedUsbPath = Path.Combine(_testDataPath, "USB_Drive");
            
            Directory.CreateDirectory(_testDataPath);
            Directory.CreateDirectory(_simulatedUsbPath);
            
            _tempFiles = new List<string>();
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles.Where(File.Exists))
            {
                try { File.Delete(file); } catch { }
            }

            if (Directory.Exists(_testDataPath))
            {
                try { Directory.Delete(_testDataPath, true); } catch { }
            }
        }

        #region Complete Workflow Tests

        [Fact]
        public async Task CompleteExportImportWorkflow_ShouldSucceed()
        {
            // Arrange - Create initial configuration
            var originalConfig = new ApiKeyConfiguration(_mockConfigLogger.Object, "TestApp");
            await originalConfig.InitializeAsync();
            
            // Set some test values
            await originalConfig.SetValueAsync("API_KEY_1", "test-key-123");
            await originalConfig.SetValueAsync("API_KEY_2", "test-key-456");
            await originalConfig.SetValueAsync("SECRET_TOKEN", "secret-789");

            // Simulate USB device
            var usbDevice = new UsbDeviceInfo
            {
                DeviceId = "\\Device\\Test",
                DriveLetter = _simulatedUsbPath,
                Model = "Test USB Device",
                SerialNumber = "TEST123",
                VolumeName = "TestDrive",
                FileSystem = "NTFS",
                FreeSpaceBytes = 1024 * 1024 * 1024, // 1GB
                TotalSizeBytes = 8L * 1024 * 1024 * 1024, // 8GB
                IsWritable = true,
                IsEncrypted = false
            };

            // Act - Export to USB
            var exportBuilder = UsbExportBuilder.Create(_mockLogger.Object);
            var exportResult = await exportBuilder
                .WithUsbDevice(usbDevice)
                .WithPassword("ExportPassword123!")
                .BindToDevice(true)
                .WithExpiry(TimeSpan.FromDays(7))
                .WithOptions(opts =>
                {
                    opts.CreateReadme = true;
                    opts.CreateVerificationFile = true;
                    opts.UseTimestampedFolder = true;
                })
                .ExportAsync(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.encrypted"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.key"));

            // Assert export succeeded
            Assert.True(exportResult.IsSuccess);
            Assert.NotNull(exportResult.ExportInfo);
            Assert.True(File.Exists(exportResult.ExportInfo.ExportPath));

            // Verify all expected files were created
            var exportDir = Path.GetDirectoryName(exportResult.ExportInfo.ExportPath)!;
            Assert.True(File.Exists(Path.Combine(exportDir, "README.txt")));
            Assert.True(File.Exists(Path.Combine(exportDir, "export_verification.json")));
            
            var deviceBindingFile = Path.ChangeExtension(exportResult.ExportInfo.ExportPath, ".device");
            Assert.True(File.Exists(deviceBindingFile));

            // Act - Import from USB
            var importConfig = new ApiKeyConfiguration(_mockConfigLogger.Object, "TestApp2");
            var deviceExport = new WindowsDeviceExport(_mockDeviceLogger.Object);
            
            var importResult = await deviceExport.ImportFromUsbDeviceAsync(
                exportResult.ExportInfo.ExportPath,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "TestApp2", "SecureConfig", "config.encrypted"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "TestApp2", "SecureConfig", "config.key"),
                "ExportPassword123!",
                verifyDeviceBinding: false); // Skip device binding in tests

            // Assert import succeeded
            Assert.True(importResult.IsSuccess);
            Assert.NotNull(importResult.ImportInfo);

            // Verify imported configuration has same values
            await importConfig.InitializeAsync();
            Assert.Equal("test-key-123", importConfig.GetValue("API_KEY_1"));
            Assert.Equal("test-key-456", importConfig.GetValue("API_KEY_2"));
            Assert.Equal("secret-789", importConfig.GetValue("SECRET_TOKEN"));
        }

        [Fact]
        public async Task ExportImport_WithWrongPassword_ShouldFail()
        {
            // Arrange
            var config = new ApiKeyConfiguration(_mockConfigLogger.Object, "TestApp");
            await config.InitializeAsync();
            await config.SetValueAsync("TEST_KEY", "test-value");

            var usbDevice = new UsbDeviceInfo
            {
                DriveLetter = _simulatedUsbPath,
                Model = "Test USB",
                SerialNumber = "TEST123",
                FreeSpaceBytes = 1024 * 1024 * 100
            };

            // Export with password
            var exportBuilder = UsbExportBuilder.Create(_mockLogger.Object);
            var exportResult = await exportBuilder
                .WithUsbDevice(usbDevice)
                .WithPassword("CorrectPassword123!")
                .ExportAsync(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.encrypted"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.key"));

            Assert.True(exportResult.IsSuccess);

            // Act - Try to import with wrong password
            var deviceExport = new WindowsDeviceExport(_mockDeviceLogger.Object);
            var importResult = await deviceExport.ImportFromUsbDeviceAsync(
                exportResult.ExportInfo.ExportPath,
                Path.Combine(_testDataPath, "import.config"),
                Path.Combine(_testDataPath, "import.key"),
                "WrongPassword123!",
                verifyDeviceBinding: false);

            // Assert
            Assert.False(importResult.IsSuccess);
            Assert.Contains("password", importResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Expiry Tests

        [Fact]
        public async Task Export_WithExpiry_ShouldBeRespected()
        {
            // Arrange
            var config = new ApiKeyConfiguration(_mockConfigLogger.Object, "TestApp");
            await config.InitializeAsync();
            await config.SetValueAsync("TEST_KEY", "test-value");

            var usbDevice = new UsbDeviceInfo
            {
                DriveLetter = _simulatedUsbPath,
                Model = "Test USB",
                FreeSpaceBytes = 1024 * 1024 * 100
            };

            // Export with past expiry (to simulate expired export)
            var pastExpiry = DateTime.UtcNow.AddMinutes(-1);
            
            var exportBuilder = UsbExportBuilder.Create(_mockLogger.Object);
            var exportResult = await exportBuilder
                .WithUsbDevice(usbDevice)
                .WithOptions(opts => opts.ExpiryTime = pastExpiry)
                .ExportAsync(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.encrypted"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.key"));

            Assert.True(exportResult.IsSuccess);

            // Act - Try to import expired export
            var deviceExport = new WindowsDeviceExport(_mockDeviceLogger.Object);
            var importResult = await deviceExport.ImportFromUsbDeviceAsync(
                exportResult.ExportInfo.ExportPath,
                Path.Combine(_testDataPath, "import.config"),
                Path.Combine(_testDataPath, "import.key"),
                verifyDeviceBinding: false);

            // Assert
            Assert.False(importResult.IsSuccess);
            Assert.Contains("expired", importResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Multiple USB Devices Tests

        [Fact]
        public async Task Export_ToMultipleDevices_ShouldCreateUniqueExports()
        {
            // Arrange
            var config = new ApiKeyConfiguration(_mockConfigLogger.Object, "TestApp");
            await config.InitializeAsync();
            await config.SetValueAsync("TEST_KEY", "test-value");

            // Simulate multiple USB devices
            var usbDevices = new List<UsbDeviceInfo>();
            for (int i = 0; i < 3; i++)
            {
                var devicePath = Path.Combine(_testDataPath, $"USB_{i}");
                Directory.CreateDirectory(devicePath);
                
                usbDevices.Add(new UsbDeviceInfo
                {
                    DriveLetter = devicePath,
                    Model = $"USB Device {i}",
                    SerialNumber = $"SERIAL{i}",
                    VolumeName = $"Drive{i}",
                    FreeSpaceBytes = 1024 * 1024 * 100
                });
            }

            var exportPaths = new List<string>();

            // Act - Export to each device
            foreach (var device in usbDevices)
            {
                var builder = UsbExportBuilder.Create(_mockLogger.Object);
                var result = await builder
                    .WithUsbDevice(device)
                    .WithPassword($"Password{device.SerialNumber}")
                    .BindToDevice(true)
                    .ExportAsync(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                            "TestApp", "SecureConfig", "config.encrypted"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                            "TestApp", "SecureConfig", "config.key"));

                Assert.True(result.IsSuccess);
                exportPaths.Add(result.ExportInfo.ExportPath);
            }

            // Assert - All exports are unique
            Assert.Equal(3, exportPaths.Count);
            Assert.Equal(exportPaths.Count, exportPaths.Distinct().Count());
            
            // Verify each export is in its own USB device directory
            for (int i = 0; i < exportPaths.Count; i++)
            {
                Assert.Contains($"USB_{i}", exportPaths[i]);
            }
        }

        #endregion

        #region Error Recovery Tests

        [Fact]
        public async Task Export_WithInsufficientSpace_ShouldFailGracefully()
        {
            // Arrange
            var config = new ApiKeyConfiguration(_mockConfigLogger.Object, "TestApp");
            await config.InitializeAsync();
            
            // Create large configuration
            for (int i = 0; i < 100; i++)
            {
                await config.SetValueAsync($"KEY_{i}", new string('x', 10000));
            }

            var usbDevice = new UsbDeviceInfo
            {
                DriveLetter = _simulatedUsbPath,
                Model = "Small USB",
                FreeSpaceBytes = 1024, // Only 1KB free
                TotalSizeBytes = 1024
            };

            // Act
            var builder = UsbExportBuilder.Create(_mockLogger.Object);
            var result = await builder
                .WithUsbDevice(usbDevice)
                .ExportAsync(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.encrypted"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.key"));

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Insufficient space", result.ErrorMessage);
        }

        [Fact]
        public async Task Import_WithCorruptedFile_ShouldFailWithClearError()
        {
            // Arrange
            var corruptedExportPath = Path.Combine(_simulatedUsbPath, "corrupted.scexport");
            File.WriteAllText(corruptedExportPath, "This is not a valid export file!");

            // Act
            var deviceExport = new WindowsDeviceExport(_mockDeviceLogger.Object);
            var result = await deviceExport.ImportFromUsbDeviceAsync(
                corruptedExportPath,
                Path.Combine(_testDataPath, "import.config"),
                Path.Combine(_testDataPath, "import.key"));

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("USB import failed", result.ErrorMessage);
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task Export_LargeConfiguration_ShouldCompleteInReasonableTime()
        {
            // Arrange
            var config = new ApiKeyConfiguration(_mockConfigLogger.Object, "TestApp");
            await config.InitializeAsync();
            
            // Create moderately large configuration
            for (int i = 0; i < 1000; i++)
            {
                await config.SetValueAsync($"KEY_{i}", $"VALUE_{i}_" + new string('x', 100));
            }

            var usbDevice = new UsbDeviceInfo
            {
                DriveLetter = _simulatedUsbPath,
                Model = "Fast USB",
                FreeSpaceBytes = 1024 * 1024 * 100
            };

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var builder = UsbExportBuilder.Create(_mockLogger.Object);
            var result = await builder
                .WithUsbDevice(usbDevice)
                .WithPassword("TestPassword123!")
                .ExportAsync(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.encrypted"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "TestApp", "SecureConfig", "config.key"));
            
            stopwatch.Stop();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
                $"Export took too long: {stopwatch.ElapsedMilliseconds}ms");
        }

        #endregion
    }
}