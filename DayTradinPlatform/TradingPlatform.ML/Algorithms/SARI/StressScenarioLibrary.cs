using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Common;
using TradingPlatform.Core.Canonical;

namespace TradingPlatform.ML.Algorithms.SARI
{
    /// <summary>
    /// Comprehensive library of stress scenarios for SARI algorithm
    /// Based on historical crises and forward-looking hypothetical scenarios
    /// </summary>
    public class StressScenarioLibrary : CanonicalServiceBase
    {
        private readonly Dictionary<string, StressScenario> _scenarios;
        private readonly Dictionary<MarketRegime, List<string>> _regimeScenarios;
        
        public StressScenarioLibrary(ICanonicalLogger logger) : base(logger)
        {
            _scenarios = new Dictionary<string, StressScenario>();
            _regimeScenarios = new Dictionary<MarketRegime, List<string>>();
            InitializeScenarios();
        }

        protected override Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogInfo($"Initializing stress scenario library with {_scenarios.Count} scenarios");
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        protected override Task<TradingResult> OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        protected override Task<TradingResult> OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        /// <summary>
        /// Get all scenarios applicable to current market regime
        /// </summary>
        public TradingResult<List<StressScenario>> GetScenariosForRegime(MarketRegime regime)
        {
            LogMethodEntry();
            LogDebug($"Getting scenarios for regime: {regime}");

            try
            {
                var scenarios = new List<StressScenario>();

                // Add base scenarios (always applicable)
                scenarios.AddRange(_scenarios.Values.Where(s => s.AlwaysActive));

                // Add regime-specific scenarios
                if (_regimeScenarios.TryGetValue(regime, out var regimeSpecific))
                {
                    foreach (var scenarioId in regimeSpecific)
                    {
                        if (_scenarios.TryGetValue(scenarioId, out var scenario))
                        {
                            scenarios.Add(scenario);
                        }
                    }
                }

                LogInfo($"Found {scenarios.Count} scenarios for {regime} regime");
                LogMethodExit();
                return TradingResult<List<StressScenario>>.Success(scenarios);
            }
            catch (Exception ex)
            {
                LogError("Error getting scenarios for regime", ex);
                return TradingResult<List<StressScenario>>.Failure($"Failed to get scenarios: {ex.Message}");
            }
        }

        /// <summary>
        /// Get specific scenario by ID
        /// </summary>
        public TradingResult<StressScenario> GetScenario(string scenarioId)
        {
            LogMethodEntry();
            
            if (_scenarios.TryGetValue(scenarioId, out var scenario))
            {
                LogDebug($"Retrieved scenario: {scenarioId}");
                LogMethodExit();
                return TradingResult<StressScenario>.Success(scenario);
            }

            LogWarning($"Scenario not found: {scenarioId}");
            LogMethodExit();
            return TradingResult<StressScenario>.Failure($"Scenario not found: {scenarioId}");
        }

        /// <summary>
        /// Update scenario probability based on market conditions
        /// </summary>
        public TradingResult UpdateScenarioProbability(string scenarioId, float newProbability, string reason)
        {
            LogMethodEntry();
            LogInfo($"Updating probability for scenario {scenarioId}: {newProbability:F4} (Reason: {reason})");

            try
            {
                if (!_scenarios.TryGetValue(scenarioId, out var scenario))
                {
                    return TradingResult.Failure($"Scenario not found: {scenarioId}");
                }

                if (newProbability < 0 || newProbability > 1)
                {
                    return TradingResult.Failure("Probability must be between 0 and 1");
                }

                scenario.Probability = newProbability;
                scenario.LastUpdated = DateTime.UtcNow;
                scenario.UpdateReason = reason;

                LogInfo($"Successfully updated scenario {scenarioId} probability");
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Error updating scenario probability", ex);
                return TradingResult.Failure($"Failed to update probability: {ex.Message}");
            }
        }

        private void InitializeScenarios()
        {
            LogDebug("Initializing stress scenario library");

            // Historical Crisis Scenarios
            InitializeHistoricalScenarios();
            
            // Hypothetical Forward-Looking Scenarios
            InitializeHypotheticalScenarios();
            
            // Regime-Specific Mappings
            InitializeRegimeMappings();

            LogInfo($"Initialized {_scenarios.Count} stress scenarios");
        }

        private void InitializeHistoricalScenarios()
        {
            // 2008 Financial Crisis
            AddScenario(new StressScenario
            {
                Id = "2008_FINANCIAL_CRISIS",
                Name = "2008 Financial Crisis",
                Category = ScenarioCategory.Historical,
                Description = "Global financial crisis triggered by subprime mortgage collapse",
                Probability = 0.05f,
                Severity = StressSeverity.Extreme,
                TimeHorizon = TimeHorizon.ThreeMonth,
                AlwaysActive = true,
                AssetClassShocks = new Dictionary<string, AssetClassShock>
                {
                    ["Equity"] = new AssetClassShock { 
                        ShockPercent = -0.40f, 
                        VolatilityMultiplier = 3.0f,
                        CorrelationOverride = 0.9f 
                    },
                    ["CorporateBonds"] = new AssetClassShock { 
                        ShockPercent = -0.20f, 
                        SpreadWidening = 400 
                    },
                    ["Commodities"] = new AssetClassShock { 
                        ShockPercent = -0.30f 
                    },
                    ["RealEstate"] = new AssetClassShock { 
                        ShockPercent = -0.35f 
                    }
                },
                SectorShocks = new Dictionary<string, float>
                {
                    ["Financials"] = -0.60f,
                    ["RealEstate"] = -0.50f,
                    ["Energy"] = -0.35f,
                    ["ConsumerDiscretionary"] = -0.30f
                },
                LiquidityImpact = new LiquidityShock
                {
                    BidAskMultiplier = 5.0f,
                    VolumeReduction = 0.7f,
                    MarketDepthReduction = 0.8f
                }
            });

            // 2020 COVID Crash
            AddScenario(new StressScenario
            {
                Id = "2020_COVID_CRASH",
                Name = "2020 COVID-19 Market Crash",
                Category = ScenarioCategory.Historical,
                Description = "Pandemic-induced market crash with unprecedented volatility",
                Probability = 0.04f,
                Severity = StressSeverity.Severe,
                TimeHorizon = TimeHorizon.OneMonth,
                AlwaysActive = true,
                AssetClassShocks = new Dictionary<string, AssetClassShock>
                {
                    ["Equity"] = new AssetClassShock { 
                        ShockPercent = -0.35f, 
                        VolatilityMultiplier = 4.0f 
                    },
                    ["Oil"] = new AssetClassShock { 
                        ShockPercent = -0.70f 
                    },
                    ["CorporateBonds"] = new AssetClassShock { 
                        ShockPercent = -0.15f, 
                        SpreadWidening = 300 
                    }
                },
                SectorShocks = new Dictionary<string, float>
                {
                    ["Airlines"] = -0.70f,
                    ["Hotels"] = -0.65f,
                    ["Energy"] = -0.50f,
                    ["Financials"] = -0.40f,
                    ["Technology"] = -0.25f,
                    ["Healthcare"] = -0.10f
                }
            });

            // 1987 Black Monday
            AddScenario(new StressScenario
            {
                Id = "1987_BLACK_MONDAY",
                Name = "1987 Black Monday",
                Category = ScenarioCategory.Historical,
                Description = "Single-day 20%+ market crash",
                Probability = 0.02f,
                Severity = StressSeverity.Extreme,
                TimeHorizon = TimeHorizon.OneDay,
                AlwaysActive = false,
                AssetClassShocks = new Dictionary<string, AssetClassShock>
                {
                    ["Equity"] = new AssetClassShock { 
                        ShockPercent = -0.22f, 
                        VolatilityMultiplier = 5.0f 
                    }
                }
            });

            // 2018 Volmageddon
            AddScenario(new StressScenario
            {
                Id = "2018_VOLMAGEDDON",
                Name = "2018 Volatility Spike (Volmageddon)",
                Category = ScenarioCategory.Historical,
                Description = "VIX spiked 115% in one day, short-vol strategies imploded",
                Probability = 0.03f,
                Severity = StressSeverity.Moderate,
                TimeHorizon = TimeHorizon.OneDay,
                AlwaysActive = false,
                AssetClassShocks = new Dictionary<string, AssetClassShock>
                {
                    ["Equity"] = new AssetClassShock { 
                        ShockPercent = -0.10f, 
                        VolatilityMultiplier = 2.5f 
                    },
                    ["Volatility"] = new AssetClassShock { 
                        ShockPercent = 1.15f 
                    }
                },
                SpecialEffects = new List<string>
                {
                    "Short volatility strategies -90%",
                    "Risk parity deleveraging",
                    "Correlation breakdown"
                }
            });
        }

        private void InitializeHypotheticalScenarios()
        {
            // Tech Bubble 2.0
            AddScenario(new StressScenario
            {
                Id = "TECH_BUBBLE_2",
                Name = "Tech Bubble 2.0 Burst",
                Category = ScenarioCategory.Hypothetical,
                Description = "Major correction in overvalued technology stocks",
                Probability = 0.10f,
                Severity = StressSeverity.Severe,
                TimeHorizon = TimeHorizon.ThreeMonth,
                AlwaysActive = false,
                AssetClassShocks = new Dictionary<string, AssetClassShock>
                {
                    ["Equity"] = new AssetClassShock { ShockPercent = -0.30f }
                },
                SectorShocks = new Dictionary<string, float>
                {
                    ["Technology"] = -0.50f,
                    ["Communications"] = -0.40f,
                    ["ConsumerDiscretionary"] = -0.25f,
                    ["Financials"] = -0.20f,
                    ["Utilities"] = -0.05f,
                    ["ConsumerStaples"] = -0.05f
                }
            });

            // China Hard Landing
            AddScenario(new StressScenario
            {
                Id = "CHINA_HARD_LANDING",
                Name = "China Economic Hard Landing",
                Category = ScenarioCategory.Hypothetical,
                Description = "Significant slowdown in Chinese economy affecting global markets",
                Probability = 0.08f,
                Severity = StressSeverity.Severe,
                TimeHorizon = TimeHorizon.SixMonth,
                AlwaysActive = false,
                AssetClassShocks = new Dictionary<string, AssetClassShock>
                {
                    ["EmergingMarkets"] = new AssetClassShock { ShockPercent = -0.40f },
                    ["Commodities"] = new AssetClassShock { ShockPercent = -0.30f },
                    ["DevelopedEquity"] = new AssetClassShock { ShockPercent = -0.20f }
                },
                RegionShocks = new Dictionary<string, float>
                {
                    ["China"] = -0.50f,
                    ["EmergingAsia"] = -0.35f,
                    ["LatinAmerica"] = -0.25f,
                    ["Europe"] = -0.15f,
                    ["US"] = -0.10f
                }
            });

            // Interest Rate Shock
            AddScenario(new StressScenario
            {
                Id = "RATE_SHOCK",
                Name = "Sudden Interest Rate Spike",
                Category = ScenarioCategory.Hypothetical,
                Description = "Central banks forced to raise rates aggressively to combat inflation",
                Probability = 0.15f,
                Severity = StressSeverity.Moderate,
                TimeHorizon = TimeHorizon.OneMonth,
                AlwaysActive = true,
                AssetClassShocks = new Dictionary<string, AssetClassShock>
                {
                    ["Bonds"] = new AssetClassShock { 
                        ShockPercent = -0.15f,
                        YieldChange = 300 // basis points
                    },
                    ["Equity"] = new AssetClassShock { ShockPercent = -0.20f },
                    ["RealEstate"] = new AssetClassShock { ShockPercent = -0.25f }
                },
                SectorShocks = new Dictionary<string, float>
                {
                    ["Utilities"] = -0.30f,
                    ["RealEstate"] = -0.35f,
                    ["Financials"] = 0.10f, // Banks benefit
                    ["Technology"] = -0.25f
                }
            });

            // Cyber Attack
            AddScenario(new StressScenario
            {
                Id = "CYBER_ATTACK",
                Name = "Major Cyber Attack on Financial System",
                Category = ScenarioCategory.Hypothetical,
                Description = "Coordinated cyber attack disrupting financial infrastructure",
                Probability = 0.05f,
                Severity = StressSeverity.Severe,
                TimeHorizon = TimeHorizon.OneWeek,
                AlwaysActive = true,
                AssetClassShocks = new Dictionary<string, AssetClassShock>
                {
                    ["Equity"] = new AssetClassShock { ShockPercent = -0.25f }
                },
                SectorShocks = new Dictionary<string, float>
                {
                    ["Financials"] = -0.40f,
                    ["Technology"] = -0.35f,
                    ["Utilities"] = -0.20f,
                    ["Gold"] = 0.15f // Flight to safety
                },
                LiquidityImpact = new LiquidityShock
                {
                    BidAskMultiplier = 3.0f,
                    VolumeReduction = 0.5f,
                    MarketDepthReduction = 0.6f
                },
                SpecialEffects = new List<string>
                {
                    "Payment systems disrupted",
                    "Trading halts possible",
                    "Counterparty risk spike"
                }
            });

            // Geopolitical Crisis
            AddScenario(new StressScenario
            {
                Id = "GEOPOLITICAL_CRISIS",
                Name = "Major Geopolitical Conflict",
                Category = ScenarioCategory.Hypothetical,
                Description = "Military conflict involving major powers",
                Probability = 0.06f,
                Severity = StressSeverity.Severe,
                TimeHorizon = TimeHorizon.OneMonth,
                AlwaysActive = true,
                AssetClassShocks = new Dictionary<string, AssetClassShock>
                {
                    ["Equity"] = new AssetClassShock { ShockPercent = -0.25f },
                    ["Oil"] = new AssetClassShock { ShockPercent = 0.50f },
                    ["Gold"] = new AssetClassShock { ShockPercent = 0.20f },
                    ["Bonds"] = new AssetClassShock { ShockPercent = 0.05f } // Flight to quality
                },
                RegionShocks = new Dictionary<string, float>
                {
                    ["ConflictRegion"] = -0.50f,
                    ["Europe"] = -0.30f,
                    ["EmergingMarkets"] = -0.25f,
                    ["US"] = -0.15f
                }
            });
        }

        private void InitializeRegimeMappings()
        {
            // Crisis regime - all scenarios potentially active
            _regimeScenarios[MarketRegime.Crisis] = new List<string>
            {
                "2008_FINANCIAL_CRISIS",
                "2020_COVID_CRASH",
                "1987_BLACK_MONDAY",
                "CYBER_ATTACK",
                "GEOPOLITICAL_CRISIS"
            };

            // Volatile regime
            _regimeScenarios[MarketRegime.Volatile] = new List<string>
            {
                "2018_VOLMAGEDDON",
                "TECH_BUBBLE_2",
                "RATE_SHOCK",
                "CHINA_HARD_LANDING"
            };

            // Bearish regime
            _regimeScenarios[MarketRegime.Bearish] = new List<string>
            {
                "TECH_BUBBLE_2",
                "CHINA_HARD_LANDING",
                "RATE_SHOCK"
            };

            // Normal regime - lower probability scenarios
            _regimeScenarios[MarketRegime.Normal] = new List<string>
            {
                "RATE_SHOCK",
                "CYBER_ATTACK"
            };

            // Stable regime - only tail risk scenarios
            _regimeScenarios[MarketRegime.Stable] = new List<string>
            {
                "CYBER_ATTACK",
                "GEOPOLITICAL_CRISIS"
            };
        }

        private void AddScenario(StressScenario scenario)
        {
            _scenarios[scenario.Id] = scenario;
            LogDebug($"Added scenario: {scenario.Id} - {scenario.Name}");
        }
    }

    /// <summary>
    /// Comprehensive stress scenario definition
    /// </summary>
    public class StressScenario
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ScenarioCategory Category { get; set; }
        public string Description { get; set; }
        public float Probability { get; set; }
        public StressSeverity Severity { get; set; }
        public TimeHorizon TimeHorizon { get; set; }
        public bool AlwaysActive { get; set; }
        
        // Shocks by asset class
        public Dictionary<string, AssetClassShock> AssetClassShocks { get; set; }
        
        // Shocks by sector
        public Dictionary<string, float> SectorShocks { get; set; }
        
        // Shocks by region
        public Dictionary<string, float> RegionShocks { get; set; }
        
        // Liquidity impact
        public LiquidityShock LiquidityImpact { get; set; }
        
        // Special effects (text descriptions)
        public List<string> SpecialEffects { get; set; }
        
        // Tracking
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string UpdateReason { get; set; }
        
        // Conditional factors
        public Dictionary<string, float> ConditionalFactors { get; set; }
    }

    public class AssetClassShock
    {
        public float ShockPercent { get; set; }
        public float VolatilityMultiplier { get; set; } = 1.0f;
        public float? CorrelationOverride { get; set; }
        public int? SpreadWidening { get; set; } // basis points
        public int? YieldChange { get; set; } // basis points
    }

    public class LiquidityShock
    {
        public float BidAskMultiplier { get; set; }
        public float VolumeReduction { get; set; }
        public float MarketDepthReduction { get; set; }
    }

    public enum ScenarioCategory
    {
        Historical,
        Hypothetical,
        Reverse,
        Regulatory
    }

    public enum StressSeverity
    {
        Mild,
        Moderate,
        Severe,
        Extreme
    }

    public enum TimeHorizon
    {
        OneDay,
        OneWeek,
        OneMonth,
        ThreeMonth,
        SixMonth
    }
}