// File: TradingPlatform.Screening.Models\AlertConfiguration.cs

namespace TradingPlatform.Screening.Models
{
    /// <summary>
    /// Configuration for alerting and notification thresholds in the screening system.
    /// References only the canonical AlertLevel enum from CriteriaResult.cs.
    /// </summary>
    public class AlertConfiguration
    {
        /// <summary>
        /// Minimum alert level required to trigger a notification.
        /// </summary>
        public AlertLevel MinimumAlertLevel { get; set; } = AlertLevel.Medium;

        /// <summary>
        /// List of channels to send alerts to (e.g., "Desktop", "Email", "SMS").
        /// </summary>
        public List<string> Channels { get; set; } = new() { "Desktop" };

        /// <summary>
        /// Enables or disables alerting.
        /// </summary>
        public bool EnableAlerts { get; set; } = true;

        /// <summary>
        /// Optional: Custom user notification preferences.
        /// </summary>
        public Dictionary<string, object> NotificationPreferences { get; set; } = new();
    }
}

// Total Lines: 25
