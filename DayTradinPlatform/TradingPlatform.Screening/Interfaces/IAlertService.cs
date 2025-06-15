// File: TradingPlatform.Screening.Interfaces\IAlertService.cs

using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Interfaces
{
    /// <summary>
    /// Defines the contract for alert services in the screening system.
    /// All implementations must use canonical models and be standards-compliant.
    /// </summary>
    public interface IAlertService
    {
        /// <summary>
        /// Sends an alert based on the provided ScreeningResult and alert configuration.
        /// </summary>
        Task SendAlertAsync(ScreeningResult result, AlertConfiguration configuration);
    }
}

// Total Lines: 17
