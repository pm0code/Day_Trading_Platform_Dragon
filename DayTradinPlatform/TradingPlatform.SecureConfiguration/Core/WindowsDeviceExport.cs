using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TradingPlatform.SecureConfiguration.Core
{
    /// <summary>
    /// Windows 11 specific device export functionality with USB device support
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WindowsDeviceExport
    {
        private readonly ILogger<WindowsDeviceExport> _logger;
        private readonly SecureExportImport _exportImport;
        
        public WindowsDeviceExport(ILogger<WindowsDeviceExport> logger)
        {
            _logger = logger;
            _exportImport = new SecureExportImport(logger);
        }

        /// <summary>
        /// Detect all removable USB devices on Windows 11
        /// </summary>
        public async Task<List<UsbDeviceInfo>> DetectUsbDevicesAsync()
        {
            var devices = new List<UsbDeviceInfo>();
            
            await Task.Run(() =>
            {
                try
                {
                    // Use WMI to detect USB drives
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");
                    
                    foreach (ManagementObject drive in searcher.Get())
                    {
                        var deviceId = drive["DeviceID"]?.ToString() ?? string.Empty;
                        var model = drive["Model"]?.ToString() ?? "Unknown USB Device";
                        var serialNumber = drive["SerialNumber"]?.ToString() ?? "N/A";
                        
                        // Get partition information
                        using var partitionSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceId}'}} " +
                            "WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                        
                        foreach (ManagementObject partition in partitionSearcher.Get())
                        {
                            // Get logical disk information
                            using var logicalSearcher = new ManagementObjectSearcher(
                                $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} " +
                                "WHERE AssocClass=Win32_LogicalDiskToPartition");
                            
                            foreach (ManagementObject logical in logicalSearcher.Get())
                            {
                                var driveLetter = logical["DeviceID"]?.ToString() ?? string.Empty;
                                var volumeName = logical["VolumeName"]?.ToString() ?? "USB Drive";
                                var fileSystem = logical["FileSystem"]?.ToString() ?? "Unknown";
                                var freeSpace = Convert.ToInt64(logical["FreeSpace"] ?? 0);
                                var totalSize = Convert.ToInt64(logical["Size"] ?? 0);
                                
                                devices.Add(new UsbDeviceInfo
                                {
                                    DeviceId = deviceId,
                                    DriveLetter = driveLetter,
                                    Model = model,
                                    SerialNumber = serialNumber,
                                    VolumeName = volumeName,
                                    FileSystem = fileSystem,
                                    FreeSpaceBytes = freeSpace,
                                    TotalSizeBytes = totalSize,
                                    IsWritable = IsDeviceWritable(driveLetter),
                                    IsEncrypted = IsBitLockerEnabled(driveLetter)
                                });
                            }
                        }
                    }
                    
                    _logger.LogInformation("Detected {Count} USB devices", devices.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to detect USB devices");
                }
            });
            
            return devices;
        }

        /// <summary>
        /// Export configuration to a specific USB device with enhanced security
        /// </summary>
        public async Task<ExportResult> ExportToUsbDeviceAsync(
            string configPath,
            string keyPath,
            UsbDeviceInfo usbDevice,
            string? password = null,
            UsbExportOptions? options = null)
        {
            try
            {
                options ??= new UsbExportOptions();
                
                _logger.LogInformation("Starting export to USB device: {Device} ({DriveLetter})", 
                    usbDevice.Model, usbDevice.DriveLetter);
                
                // Verify USB device is still connected
                if (!Directory.Exists(usbDevice.DriveLetter))
                {
                    return ExportResult.Failure("USB device is no longer available");
                }
                
                // Check available space
                var exportSize = EstimateExportSize(configPath, keyPath);
                if (usbDevice.FreeSpaceBytes < exportSize * 2) // 2x for safety
                {
                    return ExportResult.Failure(
                        $"Insufficient space on USB device. Required: {exportSize * 2 / 1024 / 1024}MB, " +
                        $"Available: {usbDevice.FreeSpaceBytes / 1024 / 1024}MB");
                }
                
                // Create secure export directory on USB
                var exportDir = CreateSecureExportDirectory(usbDevice.DriveLetter, options);
                var exportFileName = GenerateExportFileName(options);
                var exportPath = Path.Combine(exportDir, exportFileName);
                
                // Add USB device information to export
                var deviceBinding = new UsbDeviceBinding
                {
                    DeviceSerialNumber = usbDevice.SerialNumber,
                    DeviceModel = usbDevice.Model,
                    ExportedToDevice = usbDevice.VolumeName,
                    RequiresSpecificDevice = options.BindToSpecificUsb
                };
                
                // Create enhanced export options
                var enhancedOptions = new ExportOptions
                {
                    UseAdditionalDPAPI = true,
                    SignExport = true,
                    IncludePaths = false,
                    ExpiryTime = options.ExpiryTime,
                    ExportReason = $"Export to USB: {usbDevice.Model}"
                };
                
                // Export with user binding
                var exportResult = await _exportImport.ExportWithUserBindingAsync(
                    configPath, keyPath, exportPath, password, enhancedOptions);
                
                if (!exportResult.IsSuccess)
                {
                    return exportResult;
                }
                
                // Write USB device binding information
                var bindingPath = Path.ChangeExtension(exportPath, ".device");
                await WriteDeviceBinding(bindingPath, deviceBinding);
                
                // Create verification file
                if (options.CreateVerificationFile)
                {
                    await CreateVerificationFile(exportDir, exportResult.ExportInfo!);
                }
                
                // Safely eject preparation
                if (options.PrepareForSafeEject)
                {
                    FlushFileSystemBuffers(usbDevice.DriveLetter);
                }
                
                // Create README file
                if (options.CreateReadme)
                {
                    await CreateReadmeFile(exportDir, usbDevice, password != null);
                }
                
                _logger.LogInformation("Export to USB completed successfully: {Path}", exportPath);
                
                return ExportResult.Success(new ExportInfo
                {
                    ExportPath = exportPath,
                    FileSizeBytes = new FileInfo(exportPath).Length,
                    RequiresPassword = !string.IsNullOrEmpty(password),
                    ExpiresAt = options.ExpiryTime,
                    UserBound = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export to USB device");
                return ExportResult.Failure($"USB export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Import configuration from USB device with verification
        /// </summary>
        public async Task<ImportResult> ImportFromUsbDeviceAsync(
            string usbExportPath,
            string targetConfigPath,
            string targetKeyPath,
            string? password = null,
            bool verifyDeviceBinding = true)
        {
            try
            {
                _logger.LogInformation("Starting import from USB device: {Path}", usbExportPath);
                
                // Verify file exists
                if (!File.Exists(usbExportPath))
                {
                    return ImportResult.Failure("Export file not found on USB device");
                }
                
                // Check device binding if required
                if (verifyDeviceBinding)
                {
                    var bindingPath = Path.ChangeExtension(usbExportPath, ".device");
                    if (File.Exists(bindingPath))
                    {
                        var bindingResult = await VerifyDeviceBinding(bindingPath);
                        if (!bindingResult.IsValid)
                        {
                            _logger.LogWarning("Device binding verification failed: {Reason}", 
                                bindingResult.Reason);
                            
                            if (!bindingResult.AllowImport)
                            {
                                return ImportResult.Failure(
                                    $"This export is bound to a specific USB device: {bindingResult.Reason}");
                            }
                        }
                    }
                }
                
                // Import with user verification
                var importResult = await _exportImport.ImportWithUserVerificationAsync(
                    usbExportPath, targetConfigPath, targetKeyPath, password);
                
                if (!importResult.IsSuccess)
                {
                    return importResult;
                }
                
                // Log USB import details
                _logger.LogInformation(
                    "Successfully imported configuration from USB device. " +
                    "Original export: {User} on {Date} from {Machine}",
                    importResult.ImportInfo?.OriginalUser,
                    importResult.ImportInfo?.OriginalExportDate,
                    importResult.ImportInfo?.OriginalMachine);
                
                return importResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import from USB device");
                return ImportResult.Failure($"USB import failed: {ex.Message}");
            }
        }

        #region Helper Methods

        private bool IsDeviceWritable(string driveLetter)
        {
            try
            {
                var driveInfo = new DriveInfo(driveLetter.TrimEnd('\\'));
                return driveInfo.IsReady && driveInfo.DriveType == DriveType.Removable;
            }
            catch
            {
                return false;
            }
        }

        private bool IsBitLockerEnabled(string driveLetter)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter='{driveLetter}'");
                
                foreach (ManagementObject volume in searcher.Get())
                {
                    var protectionStatus = Convert.ToInt32(volume["ProtectionStatus"] ?? 0);
                    return protectionStatus > 0;
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return false;
        }

        private long EstimateExportSize(string configPath, string keyPath)
        {
            try
            {
                var configSize = new FileInfo(configPath).Length;
                var keySize = new FileInfo(keyPath).Length;
                // Estimate with encryption overhead and metadata
                return (configSize + keySize) * 3;
            }
            catch
            {
                return 10 * 1024 * 1024; // Default 10MB estimate
            }
        }

        private string CreateSecureExportDirectory(string driveLetter, UsbExportOptions options)
        {
            var basePath = Path.Combine(driveLetter, "SecureConfig");
            
            if (options.UseTimestampedFolder)
            {
                basePath = Path.Combine(basePath, $"Export_{DateTime.Now:yyyyMMdd_HHmmss}");
            }
            
            Directory.CreateDirectory(basePath);
            
            // Set directory attributes
            if (options.HideExportFolder)
            {
                File.SetAttributes(basePath, FileAttributes.Hidden | FileAttributes.System);
            }
            
            return basePath;
        }

        private string GenerateExportFileName(UsbExportOptions options)
        {
            var baseName = options.ExportFileName ?? "secure_config";
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var randomSuffix = GenerateRandomString(8);
            
            return $"{baseName}_{timestamp}_{randomSuffix}.scexport";
        }

        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new byte[length];
            
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(random);
            
            var result = new StringBuilder(length);
            foreach (var b in random)
            {
                result.Append(chars[b % chars.Length]);
            }
            
            return result.ToString();
        }

        private async Task WriteDeviceBinding(string path, UsbDeviceBinding binding)
        {
            var json = JsonSerializer.Serialize(binding, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(path, json);
        }

        private async Task<DeviceBindingVerification> VerifyDeviceBinding(string bindingPath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(bindingPath);
                var binding = JsonSerializer.Deserialize<UsbDeviceBinding>(json);
                
                if (binding == null || !binding.RequiresSpecificDevice)
                {
                    return DeviceBindingVerification.Valid();
                }
                
                // Get current USB devices
                var currentDevices = await DetectUsbDevicesAsync();
                var matchingDevice = currentDevices.FirstOrDefault(d => 
                    d.SerialNumber == binding.DeviceSerialNumber);
                
                if (matchingDevice == null)
                {
                    return DeviceBindingVerification.Invalid(
                        $"Required USB device not found: {binding.DeviceModel}",
                        allowImport: false);
                }
                
                return DeviceBindingVerification.Valid();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to verify device binding");
                return DeviceBindingVerification.Invalid("Could not verify device binding", true);
            }
        }

        private void FlushFileSystemBuffers(string driveLetter)
        {
            try
            {
                var drivePath = $"\\\\.\\{driveLetter.TrimEnd('\\')}";
                using var handle = CreateFile(
                    drivePath,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    IntPtr.Zero,
                    FileMode.Open,
                    0,
                    IntPtr.Zero);
                
                if (!handle.IsInvalid)
                {
                    FlushFileBuffers(handle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to flush file system buffers");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
            string lpFileName,
            FileAccess dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            FileMode dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushFileBuffers(Microsoft.Win32.SafeHandles.SafeFileHandle hFile);

        private async Task CreateVerificationFile(string exportDir, ExportInfo exportInfo)
        {
            var verificationData = new
            {
                ExportedAt = DateTime.UtcNow,
                ExportedBy = Environment.UserName,
                MachineName = Environment.MachineName,
                FileSize = exportInfo.FileSizeBytes,
                RequiresPassword = exportInfo.RequiresPassword,
                ExpiresAt = exportInfo.ExpiresAt,
                VerificationCode = GenerateVerificationCode()
            };
            
            var json = JsonSerializer.Serialize(verificationData, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            var verificationPath = Path.Combine(exportDir, "export_verification.json");
            await File.WriteAllTextAsync(verificationPath, json);
        }

        private string GenerateVerificationCode()
        {
            using var sha256 = SHA256.Create();
            var data = Encoding.UTF8.GetBytes(
                $"{Environment.UserName}|{Environment.MachineName}|{DateTime.UtcNow:O}");
            var hash = sha256.ComputeHash(data);
            return Convert.ToBase64String(hash).Substring(0, 16);
        }

        private async Task CreateReadmeFile(string exportDir, UsbDeviceInfo device, bool hasPassword)
        {
            var readme = new StringBuilder();
            readme.AppendLine("SECURE CONFIGURATION EXPORT");
            readme.AppendLine("===========================");
            readme.AppendLine();
            readme.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            readme.AppendLine($"From: {Environment.MachineName}");
            readme.AppendLine($"By: {Environment.UserName}");
            readme.AppendLine($"To USB: {device.Model} ({device.VolumeName})");
            readme.AppendLine();
            readme.AppendLine("IMPORTANT INFORMATION:");
            readme.AppendLine("---------------------");
            readme.AppendLine("• This export is encrypted and tied to your Windows user account");
            readme.AppendLine("• Only the same Windows user can import this configuration");
            readme.AppendLine($"• Password protected: {(hasPassword ? "YES" : "NO")}");
            readme.AppendLine("• Keep this USB device secure");
            readme.AppendLine();
            readme.AppendLine("TO IMPORT:");
            readme.AppendLine("----------");
            readme.AppendLine("1. Insert this USB device into the target computer");
            readme.AppendLine("2. Log in with the same Windows account");
            readme.AppendLine("3. Use the import function in your application");
            if (hasPassword)
            {
                readme.AppendLine("4. Enter the password when prompted");
            }
            readme.AppendLine();
            readme.AppendLine("SECURITY NOTICE:");
            readme.AppendLine("----------------");
            readme.AppendLine("This file contains encrypted sensitive information.");
            readme.AppendLine("Do not share this USB device with others.");
            readme.AppendLine("Store in a secure location when not in use.");
            
            var readmePath = Path.Combine(exportDir, "README.txt");
            await File.WriteAllTextAsync(readmePath, readme.ToString());
        }

        #endregion

        #region Nested Types

        private class UsbDeviceBinding
        {
            public string DeviceSerialNumber { get; set; } = string.Empty;
            public string DeviceModel { get; set; } = string.Empty;
            public string ExportedToDevice { get; set; } = string.Empty;
            public bool RequiresSpecificDevice { get; set; }
        }

        private class DeviceBindingVerification
        {
            public bool IsValid { get; set; }
            public string? Reason { get; set; }
            public bool AllowImport { get; set; } = true;
            
            public static DeviceBindingVerification Valid() => new() { IsValid = true };
            
            public static DeviceBindingVerification Invalid(string reason, bool allowImport = true) => new()
            {
                IsValid = false,
                Reason = reason,
                AllowImport = allowImport
            };
        }

        #endregion
    }

    #region Public Types

    /// <summary>
    /// Information about a USB device
    /// </summary>
    public class UsbDeviceInfo
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DriveLetter { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string VolumeName { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public long FreeSpaceBytes { get; set; }
        public long TotalSizeBytes { get; set; }
        public bool IsWritable { get; set; }
        public bool IsEncrypted { get; set; }
        
        public string DisplayName => $"{VolumeName} ({DriveLetter}) - {Model}";
        public string FreeSpaceGB => $"{FreeSpaceBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        public string TotalSizeGB => $"{TotalSizeBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }

    /// <summary>
    /// Options for USB export
    /// </summary>
    public class UsbExportOptions
    {
        /// <summary>
        /// Bind export to specific USB device serial number
        /// </summary>
        public bool BindToSpecificUsb { get; set; } = false;
        
        /// <summary>
        /// Create timestamped folder for export
        /// </summary>
        public bool UseTimestampedFolder { get; set; } = true;
        
        /// <summary>
        /// Hide export folder (Windows attributes)
        /// </summary>
        public bool HideExportFolder { get; set; } = false;
        
        /// <summary>
        /// Custom export file name (without extension)
        /// </summary>
        public string? ExportFileName { get; set; }
        
        /// <summary>
        /// Create verification file
        /// </summary>
        public bool CreateVerificationFile { get; set; } = true;
        
        /// <summary>
        /// Create README file
        /// </summary>
        public bool CreateReadme { get; set; } = true;
        
        /// <summary>
        /// Prepare for safe USB ejection
        /// </summary>
        public bool PrepareForSafeEject { get; set; } = true;
        
        /// <summary>
        /// Export expiry time
        /// </summary>
        public DateTime? ExpiryTime { get; set; }
    }

    #endregion
}