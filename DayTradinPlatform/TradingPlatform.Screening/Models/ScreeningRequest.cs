// File: TradingPlatform.Screening\Models\ScreeningRequest.cs

using System;
using System.Collections.Generic;
using TradingPlatform.Core.Interfaces; // Added for logger injection
using TradingPlatform.Core.Models;

namespace TradingPlatform.Screening.Models
{
    /// <summary>
    /// Represents a request to screen a set of symbols using specified trading criteria.
    /// All numeric and financial values use decimal for precision.
    /// </summary>
    public class ScreeningRequest
    {
        private readonly ILogger _logger; // Logger for TradingCriteria

        public ScreeningRequest(ILogger logger) // Constructor injection for logger
        {
            _logger = logger;
            Criteria = new TradingCriteria(logger); // Inject logger into TradingCriteria
        }


        public List<string> Symbols { get; set; } = new();
        public TradingCriteria Criteria { get; } // Now readonly, initialized in constructor
        public string MarketCode { get; set; } = "US";
        public ScreeningMode Mode { get; set; } = ScreeningMode.RealTime;
        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(5);
        public int MaxResults { get; set; } = 100;
        public bool EnableAlerts { get; set; } = true;
        public List<string> AlertChannels { get; set; } = new() { "Desktop" };
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = "MVP_User";
    }

    // ... (Other enums and classes in the file remain unchanged)
}
// Total Lines: 49
