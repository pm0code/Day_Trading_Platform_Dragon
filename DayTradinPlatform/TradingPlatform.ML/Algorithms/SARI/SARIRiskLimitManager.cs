using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Common;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.ML.Common;

namespace TradingPlatform.ML.Algorithms.SARI
{
    /// <summary>
    /// Dynamic risk limit manager that adjusts trading limits based on SARI levels
    /// Provides real-time risk management and automatic limit adjustments
    /// </summary>
    public class SARIRiskLimitManager : CanonicalServiceBase
    {
        private readonly SARICalculator _sariCalculator;
        private readonly IMarketDataService _marketDataService;
        private readonly IRiskLimitConfiguration _configuration;
        private readonly object _limitsLock = new object();
        private readonly object _auditLock = new object();
        
        // Current active risk limits
        private RiskLimits _currentLimits;
        private RiskLimits _emergencyLimits;
        private readonly Dictionary<string, RiskLimitBreach> _activeBreaches;
        private readonly List<RiskLimitAuditEntry> _auditTrail;
        
        // Risk policies
        private readonly Dictionary<RiskLevel, RiskPolicy> _riskPolicies;
        private readonly Dictionary<string, SectorRiskLimit> _sectorLimits;
        private readonly Dictionary<string, AssetClassLimit> _assetClassLimits;
        
        // Monitoring state
        private bool _isEmergencyMode = false;
        private DateTime _lastLimitUpdate = DateTime.UtcNow;
        private readonly Timer _monitoringTimer;

        public SARIRiskLimitManager(
            SARICalculator sariCalculator,
            IMarketDataService marketDataService,
            IRiskLimitConfiguration configuration,
            ICanonicalLogger logger) : base(logger)
        {
            _sariCalculator = sariCalculator ?? throw new ArgumentNullException(nameof(sariCalculator));
            _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _activeBreaches = new Dictionary<string, RiskLimitBreach>();
            _auditTrail = new List<RiskLimitAuditEntry>();
            _riskPolicies = new Dictionary<RiskLevel, RiskPolicy>();
            _sectorLimits = new Dictionary<string, SectorRiskLimit>();
            _assetClassLimits = new Dictionary<string, AssetClassLimit>();
            
            _monitoringTimer = new Timer(MonitorRiskLimitsCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        protected override async Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogInfo("Initializing SARI Risk Limit Manager");

            try
            {
                // Initialize risk policies for each risk level
                InitializeRiskPolicies();
                
                // Initialize default risk limits
                InitializeDefaultLimits();
                
                // Initialize emergency limits
                InitializeEmergencyLimits();
                
                // Load sector and asset class limits
                await LoadSectorAndAssetClassLimitsAsync(cancellationToken);
                
                LogInfo("SARI Risk Limit Manager initialized successfully");
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Failed to initialize SARI Risk Limit Manager", ex);
                return TradingResult.Failure($"Initialization failed: {ex.Message}");
            }
        }

        protected override Task<TradingResult> OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            // Start risk limit monitoring
            _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(10));
            
            LogInfo("SARI Risk Limit Manager started - monitoring active");
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        protected override Task<TradingResult> OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            
            // Stop monitoring
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            // Save audit trail
            SaveAuditTrail();
            
            LogInfo("SARI Risk Limit Manager stopped");
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        /// <summary>
        /// Update risk limits based on current SARI levels
        /// </summary>
        public async Task<TradingResult<RiskLimits>> UpdateRiskLimitsAsync(
            SARIResult sariResult,
            Portfolio portfolio,
            MarketContext marketContext,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogInfo($"Updating risk limits for SARI level: {sariResult.SARIIndex:F4}, Risk Level: {sariResult.RiskLevel}");

            try
            {
                // Get risk policy for current SARI level
                var policy = GetRiskPolicy(sariResult.RiskLevel);
                LogDebug($"Applying risk policy: {policy.Name} for {sariResult.RiskLevel} risk level");

                // Calculate new limits
                var newLimits = CalculateRiskLimits(sariResult, portfolio, marketContext, policy);

                // Apply gradual or immediate adjustment based on configuration
                RiskLimits appliedLimits;
                if (_configuration.UseGradualAdjustment && !_isEmergencyMode)
                {
                    appliedLimits = ApplyGradualAdjustment(_currentLimits, newLimits);
                    LogDebug("Applied gradual risk limit adjustment");
                }
                else
                {
                    appliedLimits = newLimits;
                    LogDebug("Applied immediate risk limit adjustment");
                }

                // Update current limits
                lock (_limitsLock)
                {
                    _currentLimits = appliedLimits;
                    _lastLimitUpdate = DateTime.UtcNow;
                }

                // Create audit entry
                CreateAuditEntry(sariResult, _currentLimits, appliedLimits, "SARI-based update");

                // Check for critical conditions
                if (sariResult.RiskLevel == RiskLevel.Critical)
                {
                    await ActivateEmergencyModeAsync(sariResult, cancellationToken);
                }

                LogInfo($"Risk limits updated successfully. Position limit: {appliedLimits.MaxPositionSize:C}, " +
                       $"Leverage: {appliedLimits.MaxLeverage:F2}x, Stop loss: {appliedLimits.StopLossPercentage:P}");
                
                LogMethodExit();
                return TradingResult<RiskLimits>.Success(appliedLimits);
            }
            catch (Exception ex)
            {
                LogError("Error updating risk limits", ex);
                return TradingResult<RiskLimits>.Failure($"Risk limit update failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a proposed trade violates current risk limits
        /// </summary>
        public TradingResult<RiskLimitValidation> ValidateTrade(
            TradeProposal proposal,
            Portfolio portfolio,
            MarketContext marketContext)
        {
            LogMethodEntry();
            LogDebug($"Validating trade: {proposal.Symbol}, {proposal.Quantity} @ {proposal.Price:C}");

            try
            {
                var validation = new RiskLimitValidation
                {
                    TradeId = proposal.Id,
                    Timestamp = DateTime.UtcNow,
                    Violations = new List<RiskLimitViolation>()
                };

                RiskLimits limits;
                lock (_limitsLock)
                {
                    limits = _isEmergencyMode ? _emergencyLimits : _currentLimits;
                }

                // Check position size limit
                var tradeValue = Math.Abs(proposal.Quantity * proposal.Price);
                if (tradeValue > limits.MaxPositionSize)
                {
                    validation.Violations.Add(new RiskLimitViolation
                    {
                        LimitType = "PositionSize",
                        CurrentValue = tradeValue,
                        LimitValue = limits.MaxPositionSize,
                        Message = $"Trade value {tradeValue:C} exceeds position limit {limits.MaxPositionSize:C}"
                    });
                    LogWarning($"Position size violation: {tradeValue:C} > {limits.MaxPositionSize:C}");
                }

                // Check leverage limit
                var currentLeverage = CalculatePortfolioLeverage(portfolio);
                var postTradeLeverage = CalculatePostTradeLeverage(portfolio, proposal);
                if (postTradeLeverage > limits.MaxLeverage)
                {
                    validation.Violations.Add(new RiskLimitViolation
                    {
                        LimitType = "Leverage",
                        CurrentValue = postTradeLeverage,
                        LimitValue = limits.MaxLeverage,
                        Message = $"Post-trade leverage {postTradeLeverage:F2}x exceeds limit {limits.MaxLeverage:F2}x"
                    });
                    LogWarning($"Leverage violation: {postTradeLeverage:F2}x > {limits.MaxLeverage:F2}x");
                }

                // Check daily volume limit
                var dailyVolume = CalculateDailyVolume(portfolio) + tradeValue;
                if (dailyVolume > limits.DailyVolumeLimit)
                {
                    validation.Violations.Add(new RiskLimitViolation
                    {
                        LimitType = "DailyVolume",
                        CurrentValue = dailyVolume,
                        LimitValue = limits.DailyVolumeLimit,
                        Message = $"Daily volume {dailyVolume:C} exceeds limit {limits.DailyVolumeLimit:C}"
                    });
                    LogWarning($"Daily volume violation: {dailyVolume:C} > {limits.DailyVolumeLimit:C}");
                }

                // Check sector concentration
                var sectorExposure = CalculateSectorExposure(portfolio, proposal);
                foreach (var sector in sectorExposure)
                {
                    if (_sectorLimits.TryGetValue(sector.Key, out var sectorLimit))
                    {
                        var adjustedLimit = sectorLimit.BaseLimit * limits.SectorConcentrationMultiplier;
                        if (sector.Value > adjustedLimit)
                        {
                            validation.Violations.Add(new RiskLimitViolation
                            {
                                LimitType = "SectorConcentration",
                                CurrentValue = sector.Value,
                                LimitValue = adjustedLimit,
                                Message = $"Sector {sector.Key} exposure {sector.Value:P} exceeds limit {adjustedLimit:P}"
                            });
                            LogWarning($"Sector concentration violation: {sector.Key} = {sector.Value:P} > {adjustedLimit:P}");
                        }
                    }
                }

                // Check asset class limits
                var assetClassExposure = CalculateAssetClassExposure(portfolio, proposal);
                foreach (var assetClass in assetClassExposure)
                {
                    if (_assetClassLimits.TryGetValue(assetClass.Key, out var assetLimit))
                    {
                        var adjustedLimit = assetLimit.BaseLimit * limits.AssetClassConcentrationMultiplier;
                        if (assetClass.Value > adjustedLimit)
                        {
                            validation.Violations.Add(new RiskLimitViolation
                            {
                                LimitType = "AssetClassConcentration",
                                CurrentValue = assetClass.Value,
                                LimitValue = adjustedLimit,
                                Message = $"Asset class {assetClass.Key} exposure {assetClass.Value:P} exceeds limit {adjustedLimit:P}"
                            });
                            LogWarning($"Asset class violation: {assetClass.Key} = {assetClass.Value:P} > {adjustedLimit:P}");
                        }
                    }
                }

                // Check margin requirements
                var marginRequired = CalculateMarginRequired(proposal, limits.MarginRequirementMultiplier);
                var availableMargin = CalculateAvailableMargin(portfolio);
                if (marginRequired > availableMargin)
                {
                    validation.Violations.Add(new RiskLimitViolation
                    {
                        LimitType = "MarginRequirement",
                        CurrentValue = marginRequired,
                        LimitValue = availableMargin,
                        Message = $"Margin required {marginRequired:C} exceeds available margin {availableMargin:C}"
                    });
                    LogWarning($"Margin violation: Required {marginRequired:C} > Available {availableMargin:C}");
                }

                validation.IsValid = !validation.Violations.Any();
                validation.RiskScore = CalculateTradeRiskScore(proposal, portfolio, limits);

                if (validation.IsValid)
                {
                    LogDebug($"Trade validated successfully. Risk score: {validation.RiskScore:F2}");
                }
                else
                {
                    LogWarning($"Trade validation failed with {validation.Violations.Count} violations");
                }

                LogMethodExit();
                return TradingResult<RiskLimitValidation>.Success(validation);
            }
            catch (Exception ex)
            {
                LogError("Error validating trade", ex);
                return TradingResult<RiskLimitValidation>.Failure($"Trade validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Monitor and detect risk limit breaches
        /// </summary>
        public async Task<TradingResult<List<RiskLimitBreach>>> DetectBreachesAsync(
            Portfolio portfolio,
            MarketContext marketContext,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogDebug("Detecting risk limit breaches");

            try
            {
                var breaches = new List<RiskLimitBreach>();
                RiskLimits limits;
                
                lock (_limitsLock)
                {
                    limits = _isEmergencyMode ? _emergencyLimits : _currentLimits;
                }

                // Check portfolio-level breaches
                var portfolioBreaches = CheckPortfolioBreaches(portfolio, limits);
                breaches.AddRange(portfolioBreaches);

                // Check position-level breaches
                var positionBreaches = CheckPositionBreaches(portfolio, limits);
                breaches.AddRange(positionBreaches);

                // Check concentration breaches
                var concentrationBreaches = CheckConcentrationBreaches(portfolio, limits);
                breaches.AddRange(concentrationBreaches);

                // Update active breaches
                lock (_limitsLock)
                {
                    _activeBreaches.Clear();
                    foreach (var breach in breaches)
                    {
                        _activeBreaches[breach.Id] = breach;
                    }
                }

                // Generate alerts for new breaches
                if (breaches.Any())
                {
                    await GenerateBreachAlertsAsync(breaches, cancellationToken);
                    LogWarning($"Detected {breaches.Count} risk limit breaches");
                }
                else
                {
                    LogDebug("No risk limit breaches detected");
                }

                LogMethodExit();
                return TradingResult<List<RiskLimitBreach>>.Success(breaches);
            }
            catch (Exception ex)
            {
                LogError("Error detecting breaches", ex);
                return TradingResult<List<RiskLimitBreach>>.Failure($"Breach detection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Activate emergency risk reduction mode
        /// </summary>
        public async Task<TradingResult> ActivateEmergencyModeAsync(
            SARIResult sariResult,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogWarning($"ACTIVATING EMERGENCY MODE - SARI: {sariResult.SARIIndex:F4}");

            try
            {
                lock (_limitsLock)
                {
                    _isEmergencyMode = true;
                    _currentLimits = _emergencyLimits;
                }

                // Create emergency audit entry
                CreateAuditEntry(sariResult, _currentLimits, _emergencyLimits, "EMERGENCY MODE ACTIVATED");

                // Notify all connected systems
                await NotifyEmergencyModeAsync(sariResult, cancellationToken);

                // Initiate automatic risk reduction
                await InitiateAutomaticRiskReductionAsync(sariResult, cancellationToken);

                LogWarning("Emergency mode activated successfully");
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Error activating emergency mode", ex);
                return TradingResult.Failure($"Emergency mode activation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Deactivate emergency mode when conditions improve
        /// </summary>
        public async Task<TradingResult> DeactivateEmergencyModeAsync(
            SARIResult sariResult,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogInfo($"DEACTIVATING EMERGENCY MODE - SARI: {sariResult.SARIIndex:F4}");

            try
            {
                if (!_isEmergencyMode)
                {
                    LogDebug("Emergency mode is not active");
                    return TradingResult.Success();
                }

                // Verify conditions are safe
                if (sariResult.RiskLevel >= RiskLevel.VeryHigh)
                {
                    LogWarning($"Cannot deactivate emergency mode - risk level still {sariResult.RiskLevel}");
                    return TradingResult.Failure("Risk level too high to deactivate emergency mode");
                }

                lock (_limitsLock)
                {
                    _isEmergencyMode = false;
                    // Recalculate normal limits based on current SARI
                    var policy = GetRiskPolicy(sariResult.RiskLevel);
                    _currentLimits = CalculateRiskLimits(sariResult, null, null, policy);
                }

                // Create audit entry
                CreateAuditEntry(sariResult, _emergencyLimits, _currentLimits, "EMERGENCY MODE DEACTIVATED");

                // Notify systems
                await NotifyEmergencyModeDeactivatedAsync(sariResult, cancellationToken);

                LogInfo("Emergency mode deactivated successfully");
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Error deactivating emergency mode", ex);
                return TradingResult.Failure($"Emergency mode deactivation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current risk limits
        /// </summary>
        public TradingResult<RiskLimits> GetCurrentLimits()
        {
            LogMethodEntry();
            
            try
            {
                RiskLimits limits;
                lock (_limitsLock)
                {
                    limits = _isEmergencyMode ? _emergencyLimits : _currentLimits;
                }

                LogDebug($"Current limits: Emergency={_isEmergencyMode}, Position={limits.MaxPositionSize:C}");
                LogMethodExit();
                return TradingResult<RiskLimits>.Success(limits.Clone());
            }
            catch (Exception ex)
            {
                LogError("Error getting current limits", ex);
                return TradingResult<RiskLimits>.Failure($"Failed to get limits: {ex.Message}");
            }
        }

        /// <summary>
        /// Get risk limit audit trail
        /// </summary>
        public TradingResult<List<RiskLimitAuditEntry>> GetAuditTrail(
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            LogMethodEntry();
            
            try
            {
                List<RiskLimitAuditEntry> entries;
                lock (_auditLock)
                {
                    entries = _auditTrail.ToList();
                }

                if (startDate.HasValue)
                {
                    entries = entries.Where(e => e.Timestamp >= startDate.Value).ToList();
                }

                if (endDate.HasValue)
                {
                    entries = entries.Where(e => e.Timestamp <= endDate.Value).ToList();
                }

                LogDebug($"Retrieved {entries.Count} audit entries");
                LogMethodExit();
                return TradingResult<List<RiskLimitAuditEntry>>.Success(entries);
            }
            catch (Exception ex)
            {
                LogError("Error getting audit trail", ex);
                return TradingResult<List<RiskLimitAuditEntry>>.Failure($"Failed to get audit trail: {ex.Message}");
            }
        }

        private void InitializeRiskPolicies()
        {
            LogDebug("Initializing risk policies");

            _riskPolicies[RiskLevel.Low] = new RiskPolicy
            {
                Name = "Low Risk Policy",
                RiskLevel = RiskLevel.Low,
                PositionSizeMultiplier = 1.0m,
                LeverageMultiplier = 1.0f,
                StopLossMultiplier = 1.0f,
                VolumeMultiplier = 1.0f,
                ConcentrationMultiplier = 1.0f
            };

            _riskPolicies[RiskLevel.Medium] = new RiskPolicy
            {
                Name = "Medium Risk Policy",
                RiskLevel = RiskLevel.Medium,
                PositionSizeMultiplier = 0.8m,
                LeverageMultiplier = 0.8f,
                StopLossMultiplier = 0.9f,
                VolumeMultiplier = 0.9f,
                ConcentrationMultiplier = 0.9f
            };

            _riskPolicies[RiskLevel.High] = new RiskPolicy
            {
                Name = "High Risk Policy",
                RiskLevel = RiskLevel.High,
                PositionSizeMultiplier = 0.5m,
                LeverageMultiplier = 0.5f,
                StopLossMultiplier = 0.7f,
                VolumeMultiplier = 0.6f,
                ConcentrationMultiplier = 0.8f
            };

            _riskPolicies[RiskLevel.VeryHigh] = new RiskPolicy
            {
                Name = "Very High Risk Policy",
                RiskLevel = RiskLevel.VeryHigh,
                PositionSizeMultiplier = 0.3m,
                LeverageMultiplier = 0.3f,
                StopLossMultiplier = 0.5f,
                VolumeMultiplier = 0.4f,
                ConcentrationMultiplier = 0.6f
            };

            _riskPolicies[RiskLevel.Critical] = new RiskPolicy
            {
                Name = "Critical Risk Policy",
                RiskLevel = RiskLevel.Critical,
                PositionSizeMultiplier = 0.1m,
                LeverageMultiplier = 0.1f,
                StopLossMultiplier = 0.3f,
                VolumeMultiplier = 0.2f,
                ConcentrationMultiplier = 0.4f
            };
        }

        private void InitializeDefaultLimits()
        {
            LogDebug("Initializing default risk limits");

            _currentLimits = new RiskLimits
            {
                MaxPositionSize = _configuration.BasePositionSize,
                MaxLeverage = _configuration.BaseLeverage,
                StopLossPercentage = _configuration.BaseStopLoss,
                TakeProfitPercentage = _configuration.BaseTakeProfit,
                DailyVolumeLimit = _configuration.BaseDailyVolumeLimit,
                MaxOpenPositions = _configuration.BaseMaxOpenPositions,
                SectorConcentrationMultiplier = 1.0f,
                AssetClassConcentrationMultiplier = 1.0f,
                MarginRequirementMultiplier = 1.0f,
                LastUpdated = DateTime.UtcNow
            };
        }

        private void InitializeEmergencyLimits()
        {
            LogDebug("Initializing emergency risk limits");

            _emergencyLimits = new RiskLimits
            {
                MaxPositionSize = _configuration.BasePositionSize * 0.1m,
                MaxLeverage = 1.0f, // No leverage in emergency
                StopLossPercentage = 0.02f, // 2% stop loss
                TakeProfitPercentage = 0.01f, // 1% take profit
                DailyVolumeLimit = _configuration.BaseDailyVolumeLimit * 0.2m,
                MaxOpenPositions = Math.Max(1, _configuration.BaseMaxOpenPositions / 5),
                SectorConcentrationMultiplier = 0.5f,
                AssetClassConcentrationMultiplier = 0.5f,
                MarginRequirementMultiplier = 2.0f, // Double margin requirements
                LastUpdated = DateTime.UtcNow
            };
        }

        private async Task LoadSectorAndAssetClassLimitsAsync(CancellationToken cancellationToken)
        {
            LogDebug("Loading sector and asset class limits");

            // Default sector limits
            _sectorLimits["Technology"] = new SectorRiskLimit { Sector = "Technology", BaseLimit = 0.25f };
            _sectorLimits["Financials"] = new SectorRiskLimit { Sector = "Financials", BaseLimit = 0.20f };
            _sectorLimits["Healthcare"] = new SectorRiskLimit { Sector = "Healthcare", BaseLimit = 0.20f };
            _sectorLimits["Energy"] = new SectorRiskLimit { Sector = "Energy", BaseLimit = 0.15f };
            _sectorLimits["ConsumerDiscretionary"] = new SectorRiskLimit { Sector = "ConsumerDiscretionary", BaseLimit = 0.15f };
            _sectorLimits["Industrials"] = new SectorRiskLimit { Sector = "Industrials", BaseLimit = 0.15f };
            _sectorLimits["Materials"] = new SectorRiskLimit { Sector = "Materials", BaseLimit = 0.10f };
            _sectorLimits["Utilities"] = new SectorRiskLimit { Sector = "Utilities", BaseLimit = 0.10f };
            _sectorLimits["RealEstate"] = new SectorRiskLimit { Sector = "RealEstate", BaseLimit = 0.10f };

            // Default asset class limits
            _assetClassLimits["Equity"] = new AssetClassLimit { AssetClass = "Equity", BaseLimit = 0.80f };
            _assetClassLimits["Options"] = new AssetClassLimit { AssetClass = "Options", BaseLimit = 0.20f };
            _assetClassLimits["ETF"] = new AssetClassLimit { AssetClass = "ETF", BaseLimit = 0.50f };
            _assetClassLimits["Futures"] = new AssetClassLimit { AssetClass = "Futures", BaseLimit = 0.15f };
            _assetClassLimits["Crypto"] = new AssetClassLimit { AssetClass = "Crypto", BaseLimit = 0.10f };

            await Task.CompletedTask;
        }

        private RiskPolicy GetRiskPolicy(RiskLevel riskLevel)
        {
            return _riskPolicies.GetValueOrDefault(riskLevel, _riskPolicies[RiskLevel.Medium]);
        }

        private RiskLimits CalculateRiskLimits(
            SARIResult sariResult,
            Portfolio portfolio,
            MarketContext marketContext,
            RiskPolicy policy)
        {
            LogDebug($"Calculating risk limits with policy: {policy.Name}");

            var limits = new RiskLimits
            {
                // Position size adjusted by SARI and policy
                MaxPositionSize = _configuration.BasePositionSize * policy.PositionSizeMultiplier * (1 - sariResult.SARIIndex),
                
                // Leverage reduced based on risk level
                MaxLeverage = _configuration.BaseLeverage * policy.LeverageMultiplier,
                
                // Stop loss tightened in high risk environments
                StopLossPercentage = _configuration.BaseStopLoss * policy.StopLossMultiplier,
                
                // Take profit reduced to lock in gains faster
                TakeProfitPercentage = _configuration.BaseTakeProfit * (2 - policy.StopLossMultiplier),
                
                // Volume limits reduced
                DailyVolumeLimit = _configuration.BaseDailyVolumeLimit * policy.VolumeMultiplier,
                
                // Fewer open positions in risky environments
                MaxOpenPositions = (int)(_configuration.BaseMaxOpenPositions * policy.PositionSizeMultiplier),
                
                // Concentration limits
                SectorConcentrationMultiplier = policy.ConcentrationMultiplier,
                AssetClassConcentrationMultiplier = policy.ConcentrationMultiplier,
                
                // Margin requirements increased
                MarginRequirementMultiplier = 1.0f + (1.0f - policy.LeverageMultiplier),
                
                LastUpdated = DateTime.UtcNow
            };

            // Apply market regime adjustments
            if (marketContext != null)
            {
                ApplyMarketRegimeAdjustments(limits, marketContext.MarketRegime);
            }

            // Apply scenario-specific adjustments
            ApplyScenarioAdjustments(limits, sariResult);

            return limits;
        }

        private void ApplyMarketRegimeAdjustments(RiskLimits limits, MarketRegime regime)
        {
            LogDebug($"Applying market regime adjustments for {regime}");

            switch (regime)
            {
                case MarketRegime.Crisis:
                    limits.MaxPositionSize *= 0.5m;
                    limits.MaxLeverage *= 0.3f;
                    limits.DailyVolumeLimit *= 0.4m;
                    break;
                    
                case MarketRegime.Volatile:
                    limits.MaxPositionSize *= 0.7m;
                    limits.MaxLeverage *= 0.6f;
                    limits.DailyVolumeLimit *= 0.7m;
                    break;
                    
                case MarketRegime.Stable:
                    // No additional adjustments for stable regime
                    break;
            }
        }

        private void ApplyScenarioAdjustments(RiskLimits limits, SARIResult sariResult)
        {
            LogDebug("Applying scenario-specific adjustments");

            // Find dominant scenarios
            var topScenarios = sariResult.ScenarioResults
                .OrderByDescending(s => s.WeightedImpact)
                .Take(2)
                .ToList();

            foreach (var scenario in topScenarios)
            {
                switch (scenario.ScenarioId)
                {
                    case "RATE_SHOCK":
                        // Reduce leverage further for rate-sensitive positions
                        limits.MaxLeverage *= 0.8f;
                        break;
                        
                    case "TECH_BUBBLE_2":
                        // Reduce tech sector concentration
                        if (_sectorLimits.ContainsKey("Technology"))
                        {
                            limits.SectorConcentrationMultiplier *= 0.7f;
                        }
                        break;
                        
                    case "2008_FINANCIAL_CRISIS":
                        // Increase margin requirements significantly
                        limits.MarginRequirementMultiplier *= 1.5f;
                        break;
                }
            }
        }

        private RiskLimits ApplyGradualAdjustment(RiskLimits current, RiskLimits target)
        {
            LogDebug("Applying gradual risk limit adjustment");

            var adjustmentRate = _configuration.GradualAdjustmentRate;
            
            return new RiskLimits
            {
                MaxPositionSize = current.MaxPositionSize + (target.MaxPositionSize - current.MaxPositionSize) * (decimal)adjustmentRate,
                MaxLeverage = current.MaxLeverage + (target.MaxLeverage - current.MaxLeverage) * adjustmentRate,
                StopLossPercentage = current.StopLossPercentage + (target.StopLossPercentage - current.StopLossPercentage) * adjustmentRate,
                TakeProfitPercentage = current.TakeProfitPercentage + (target.TakeProfitPercentage - current.TakeProfitPercentage) * adjustmentRate,
                DailyVolumeLimit = current.DailyVolumeLimit + (target.DailyVolumeLimit - current.DailyVolumeLimit) * (decimal)adjustmentRate,
                MaxOpenPositions = (int)(current.MaxOpenPositions + (target.MaxOpenPositions - current.MaxOpenPositions) * adjustmentRate),
                SectorConcentrationMultiplier = current.SectorConcentrationMultiplier + (target.SectorConcentrationMultiplier - current.SectorConcentrationMultiplier) * adjustmentRate,
                AssetClassConcentrationMultiplier = current.AssetClassConcentrationMultiplier + (target.AssetClassConcentrationMultiplier - current.AssetClassConcentrationMultiplier) * adjustmentRate,
                MarginRequirementMultiplier = current.MarginRequirementMultiplier + (target.MarginRequirementMultiplier - current.MarginRequirementMultiplier) * adjustmentRate,
                LastUpdated = DateTime.UtcNow
            };
        }

        private void CreateAuditEntry(SARIResult sariResult, RiskLimits oldLimits, RiskLimits newLimits, string reason)
        {
            LogDebug($"Creating audit entry: {reason}");

            var entry = new RiskLimitAuditEntry
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                SARIIndex = sariResult.SARIIndex,
                RiskLevel = sariResult.RiskLevel,
                OldLimits = oldLimits.Clone(),
                NewLimits = newLimits.Clone(),
                Reason = reason,
                IsEmergencyMode = _isEmergencyMode
            };

            lock (_auditLock)
            {
                _auditTrail.Add(entry);
                
                // Keep only last 1000 entries
                if (_auditTrail.Count > 1000)
                {
                    _auditTrail.RemoveAt(0);
                }
            }
        }

        private decimal CalculatePortfolioLeverage(Portfolio portfolio)
        {
            if (portfolio.CashBalance <= 0) return 0;
            return portfolio.TotalValue / portfolio.CashBalance;
        }

        private decimal CalculatePostTradeLeverage(Portfolio portfolio, TradeProposal proposal)
        {
            var tradeValue = proposal.Quantity * proposal.Price;
            var newTotalValue = portfolio.TotalValue + Math.Abs(tradeValue);
            var newCashBalance = portfolio.CashBalance - Math.Abs(tradeValue);
            
            if (newCashBalance <= 0) return 999m; // Max leverage
            return newTotalValue / newCashBalance;
        }

        private decimal CalculateDailyVolume(Portfolio portfolio)
        {
            // In practice, would track actual daily volume
            return portfolio.TotalValue * 0.1m; // Estimate 10% turnover
        }

        private Dictionary<string, float> CalculateSectorExposure(Portfolio portfolio, TradeProposal proposal)
        {
            var sectorExposure = new Dictionary<string, float>();
            var totalValue = portfolio.TotalValue;

            // Calculate current sector exposures
            foreach (var holding in portfolio.Holdings.Values)
            {
                var sector = holding.Sector ?? "Unknown";
                if (!sectorExposure.ContainsKey(sector))
                {
                    sectorExposure[sector] = 0;
                }
                sectorExposure[sector] += (float)(holding.MarketValue / totalValue);
            }

            // Add proposed trade
            if (proposal != null)
            {
                var proposedValue = proposal.Quantity * proposal.Price;
                var newTotalValue = totalValue + Math.Abs(proposedValue);
                var proposedSector = proposal.Sector ?? "Unknown";
                
                // Recalculate all exposures with new total
                foreach (var sector in sectorExposure.Keys.ToList())
                {
                    sectorExposure[sector] = (float)((decimal)sectorExposure[sector] * totalValue / newTotalValue);
                }
                
                if (!sectorExposure.ContainsKey(proposedSector))
                {
                    sectorExposure[proposedSector] = 0;
                }
                sectorExposure[proposedSector] += (float)(Math.Abs(proposedValue) / newTotalValue);
            }

            return sectorExposure;
        }

        private Dictionary<string, float> CalculateAssetClassExposure(Portfolio portfolio, TradeProposal proposal)
        {
            var assetClassExposure = new Dictionary<string, float>();
            var totalValue = portfolio.TotalValue;

            // Calculate current asset class exposures
            foreach (var holding in portfolio.Holdings.Values)
            {
                var assetClass = holding.AssetClass ?? "Unknown";
                if (!assetClassExposure.ContainsKey(assetClass))
                {
                    assetClassExposure[assetClass] = 0;
                }
                assetClassExposure[assetClass] += (float)(holding.MarketValue / totalValue);
            }

            // Add proposed trade
            if (proposal != null)
            {
                var proposedValue = proposal.Quantity * proposal.Price;
                var newTotalValue = totalValue + Math.Abs(proposedValue);
                var proposedAssetClass = proposal.AssetClass ?? "Equity";
                
                // Recalculate all exposures with new total
                foreach (var assetClass in assetClassExposure.Keys.ToList())
                {
                    assetClassExposure[assetClass] = (float)((decimal)assetClassExposure[assetClass] * totalValue / newTotalValue);
                }
                
                if (!assetClassExposure.ContainsKey(proposedAssetClass))
                {
                    assetClassExposure[proposedAssetClass] = 0;
                }
                assetClassExposure[proposedAssetClass] += (float)(Math.Abs(proposedValue) / newTotalValue);
            }

            return assetClassExposure;
        }

        private decimal CalculateMarginRequired(TradeProposal proposal, float marginMultiplier)
        {
            var baseMargin = Math.Abs(proposal.Quantity * proposal.Price) * 0.25m; // 25% base margin
            return baseMargin * (decimal)marginMultiplier;
        }

        private decimal CalculateAvailableMargin(Portfolio portfolio)
        {
            // Simple calculation - in practice would be more complex
            return portfolio.CashBalance * 0.5m; // 50% of cash available for margin
        }

        private float CalculateTradeRiskScore(TradeProposal proposal, Portfolio portfolio, RiskLimits limits)
        {
            float riskScore = 0;

            // Size risk
            var sizeRatio = (float)(Math.Abs(proposal.Quantity * proposal.Price) / limits.MaxPositionSize);
            riskScore += sizeRatio * 0.3f;

            // Leverage risk
            var leverageRatio = (float)(CalculatePostTradeLeverage(portfolio, proposal) / (decimal)limits.MaxLeverage);
            riskScore += leverageRatio * 0.3f;

            // Volatility risk
            riskScore += proposal.ImpliedVolatility * 0.2f;

            // Concentration risk
            var sectorExposure = CalculateSectorExposure(portfolio, proposal);
            var maxSectorExposure = sectorExposure.Values.DefaultIfEmpty(0).Max();
            riskScore += maxSectorExposure * 0.2f;

            return Math.Min(1.0f, riskScore); // Cap at 1.0
        }

        private List<RiskLimitBreach> CheckPortfolioBreaches(Portfolio portfolio, RiskLimits limits)
        {
            var breaches = new List<RiskLimitBreach>();

            // Check total leverage
            var currentLeverage = CalculatePortfolioLeverage(portfolio);
            if (currentLeverage > (decimal)limits.MaxLeverage)
            {
                breaches.Add(new RiskLimitBreach
                {
                    Id = Guid.NewGuid().ToString(),
                    BreachType = "PortfolioLeverage",
                    Severity = BreachSeverity.High,
                    CurrentValue = currentLeverage,
                    LimitValue = (decimal)limits.MaxLeverage,
                    Description = $"Portfolio leverage {currentLeverage:F2}x exceeds limit {limits.MaxLeverage:F2}x",
                    DetectedAt = DateTime.UtcNow
                });
            }

            // Check number of open positions
            if (portfolio.Holdings.Count > limits.MaxOpenPositions)
            {
                breaches.Add(new RiskLimitBreach
                {
                    Id = Guid.NewGuid().ToString(),
                    BreachType = "OpenPositions",
                    Severity = BreachSeverity.Medium,
                    CurrentValue = portfolio.Holdings.Count,
                    LimitValue = limits.MaxOpenPositions,
                    Description = $"Open positions {portfolio.Holdings.Count} exceeds limit {limits.MaxOpenPositions}",
                    DetectedAt = DateTime.UtcNow
                });
            }

            return breaches;
        }

        private List<RiskLimitBreach> CheckPositionBreaches(Portfolio portfolio, RiskLimits limits)
        {
            var breaches = new List<RiskLimitBreach>();

            foreach (var holding in portfolio.Holdings.Values)
            {
                // Check individual position size
                if (holding.MarketValue > limits.MaxPositionSize)
                {
                    breaches.Add(new RiskLimitBreach
                    {
                        Id = Guid.NewGuid().ToString(),
                        BreachType = "PositionSize",
                        Severity = BreachSeverity.High,
                        Symbol = holding.Symbol,
                        CurrentValue = holding.MarketValue,
                        LimitValue = limits.MaxPositionSize,
                        Description = $"Position {holding.Symbol} value {holding.MarketValue:C} exceeds limit {limits.MaxPositionSize:C}",
                        DetectedAt = DateTime.UtcNow
                    });
                }

                // Check unrealized loss
                var lossPercentage = holding.UnrealizedPnL / (holding.AveragePrice * holding.Quantity);
                if (lossPercentage < -(decimal)limits.StopLossPercentage)
                {
                    breaches.Add(new RiskLimitBreach
                    {
                        Id = Guid.NewGuid().ToString(),
                        BreachType = "StopLoss",
                        Severity = BreachSeverity.Critical,
                        Symbol = holding.Symbol,
                        CurrentValue = lossPercentage,
                        LimitValue = -(decimal)limits.StopLossPercentage,
                        Description = $"Position {holding.Symbol} loss {lossPercentage:P} exceeds stop loss {limits.StopLossPercentage:P}",
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            return breaches;
        }

        private List<RiskLimitBreach> CheckConcentrationBreaches(Portfolio portfolio, RiskLimits limits)
        {
            var breaches = new List<RiskLimitBreach>();

            // Check sector concentration
            var sectorExposure = CalculateSectorExposure(portfolio, null);
            foreach (var sector in sectorExposure)
            {
                if (_sectorLimits.TryGetValue(sector.Key, out var sectorLimit))
                {
                    var adjustedLimit = sectorLimit.BaseLimit * limits.SectorConcentrationMultiplier;
                    if (sector.Value > adjustedLimit)
                    {
                        breaches.Add(new RiskLimitBreach
                        {
                            Id = Guid.NewGuid().ToString(),
                            BreachType = "SectorConcentration",
                            Severity = BreachSeverity.Medium,
                            CurrentValue = (decimal)sector.Value,
                            LimitValue = (decimal)adjustedLimit,
                            Description = $"Sector {sector.Key} exposure {sector.Value:P} exceeds limit {adjustedLimit:P}",
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            // Check asset class concentration
            var assetClassExposure = CalculateAssetClassExposure(portfolio, null);
            foreach (var assetClass in assetClassExposure)
            {
                if (_assetClassLimits.TryGetValue(assetClass.Key, out var assetLimit))
                {
                    var adjustedLimit = assetLimit.BaseLimit * limits.AssetClassConcentrationMultiplier;
                    if (assetClass.Value > adjustedLimit)
                    {
                        breaches.Add(new RiskLimitBreach
                        {
                            Id = Guid.NewGuid().ToString(),
                            BreachType = "AssetClassConcentration",
                            Severity = BreachSeverity.Medium,
                            CurrentValue = (decimal)assetClass.Value,
                            LimitValue = (decimal)adjustedLimit,
                            Description = $"Asset class {assetClass.Key} exposure {assetClass.Value:P} exceeds limit {adjustedLimit:P}",
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            return breaches;
        }

        private async Task GenerateBreachAlertsAsync(List<RiskLimitBreach> breaches, CancellationToken cancellationToken)
        {
            LogDebug($"Generating alerts for {breaches.Count} breaches");

            // Group by severity
            var criticalBreaches = breaches.Where(b => b.Severity == BreachSeverity.Critical).ToList();
            var highBreaches = breaches.Where(b => b.Severity == BreachSeverity.High).ToList();

            if (criticalBreaches.Any())
            {
                LogError($"CRITICAL: {criticalBreaches.Count} critical risk limit breaches detected");
                // In practice, would send immediate alerts
            }

            if (highBreaches.Any())
            {
                LogWarning($"HIGH: {highBreaches.Count} high severity risk limit breaches detected");
                // In practice, would send high priority alerts
            }

            await Task.CompletedTask;
        }

        private async Task NotifyEmergencyModeAsync(SARIResult sariResult, CancellationToken cancellationToken)
        {
            LogWarning("Notifying systems of emergency mode activation");
            
            // In practice, would notify:
            // - Trading engines to halt new positions
            // - Risk management systems
            // - Portfolio managers
            // - Compliance teams
            
            await Task.CompletedTask;
        }

        private async Task NotifyEmergencyModeDeactivatedAsync(SARIResult sariResult, CancellationToken cancellationToken)
        {
            LogInfo("Notifying systems of emergency mode deactivation");
            
            // In practice, would notify all systems that normal operations can resume
            
            await Task.CompletedTask;
        }

        private async Task InitiateAutomaticRiskReductionAsync(SARIResult sariResult, CancellationToken cancellationToken)
        {
            LogWarning("Initiating automatic risk reduction");
            
            // In practice, would:
            // 1. Cancel all open orders
            // 2. Close positions exceeding emergency limits
            // 3. Reduce leverage to emergency levels
            // 4. Increase hedging positions
            
            await Task.CompletedTask;
        }

        private void MonitorRiskLimitsCallback(object state)
        {
            try
            {
                LogDebug("Risk limit monitoring cycle started");
                
                // Check if limits need refresh
                lock (_limitsLock)
                {
                    var timeSinceUpdate = DateTime.UtcNow - _lastLimitUpdate;
                    if (timeSinceUpdate > TimeSpan.FromMinutes(5))
                    {
                        LogDebug("Risk limits may be stale, refresh recommended");
                    }
                }
                
                // Log current state
                LogDebug($"Emergency Mode: {_isEmergencyMode}, Active Breaches: {_activeBreaches.Count}");
            }
            catch (Exception ex)
            {
                LogError("Error in risk limit monitoring", ex);
            }
        }

        private void SaveAuditTrail()
        {
            LogDebug("Saving risk limit audit trail");
            
            // In practice, would persist to database or file
            lock (_auditLock)
            {
                LogInfo($"Audit trail contains {_auditTrail.Count} entries");
            }
        }
    }

    // Supporting classes
    public class RiskLimits
    {
        public decimal MaxPositionSize { get; set; }
        public float MaxLeverage { get; set; }
        public float StopLossPercentage { get; set; }
        public float TakeProfitPercentage { get; set; }
        public decimal DailyVolumeLimit { get; set; }
        public int MaxOpenPositions { get; set; }
        public float SectorConcentrationMultiplier { get; set; }
        public float AssetClassConcentrationMultiplier { get; set; }
        public float MarginRequirementMultiplier { get; set; }
        public DateTime LastUpdated { get; set; }

        public RiskLimits Clone()
        {
            return new RiskLimits
            {
                MaxPositionSize = MaxPositionSize,
                MaxLeverage = MaxLeverage,
                StopLossPercentage = StopLossPercentage,
                TakeProfitPercentage = TakeProfitPercentage,
                DailyVolumeLimit = DailyVolumeLimit,
                MaxOpenPositions = MaxOpenPositions,
                SectorConcentrationMultiplier = SectorConcentrationMultiplier,
                AssetClassConcentrationMultiplier = AssetClassConcentrationMultiplier,
                MarginRequirementMultiplier = MarginRequirementMultiplier,
                LastUpdated = LastUpdated
            };
        }
    }

    public class RiskPolicy
    {
        public string Name { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public decimal PositionSizeMultiplier { get; set; }
        public float LeverageMultiplier { get; set; }
        public float StopLossMultiplier { get; set; }
        public float VolumeMultiplier { get; set; }
        public float ConcentrationMultiplier { get; set; }
    }

    public class TradeProposal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Symbol { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string Side { get; set; } // Buy/Sell
        public string OrderType { get; set; } // Market/Limit
        public string Sector { get; set; }
        public string AssetClass { get; set; } = "Equity";
        public float ImpliedVolatility { get; set; }
        public DateTime ProposedAt { get; set; } = DateTime.UtcNow;
    }

    public class RiskLimitValidation
    {
        public string TradeId { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsValid { get; set; }
        public List<RiskLimitViolation> Violations { get; set; }
        public float RiskScore { get; set; }
    }

    public class RiskLimitViolation
    {
        public string LimitType { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal LimitValue { get; set; }
        public string Message { get; set; }
    }

    public class RiskLimitBreach
    {
        public string Id { get; set; }
        public string BreachType { get; set; }
        public BreachSeverity Severity { get; set; }
        public string Symbol { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal LimitValue { get; set; }
        public string Description { get; set; }
        public DateTime DetectedAt { get; set; }
    }

    public enum BreachSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class RiskLimitAuditEntry
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public float SARIIndex { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public RiskLimits OldLimits { get; set; }
        public RiskLimits NewLimits { get; set; }
        public string Reason { get; set; }
        public bool IsEmergencyMode { get; set; }
    }

    public class SectorRiskLimit
    {
        public string Sector { get; set; }
        public float BaseLimit { get; set; }
    }

    public class AssetClassLimit
    {
        public string AssetClass { get; set; }
        public float BaseLimit { get; set; }
    }

    public interface IRiskLimitConfiguration
    {
        decimal BasePositionSize { get; }
        float BaseLeverage { get; }
        float BaseStopLoss { get; }
        float BaseTakeProfit { get; }
        decimal BaseDailyVolumeLimit { get; }
        int BaseMaxOpenPositions { get; }
        bool UseGradualAdjustment { get; }
        float GradualAdjustmentRate { get; }
    }

    public class MarketContext
    {
        public MarketRegime MarketRegime { get; set; }
        public float MarketVolatility { get; set; }
        public Dictionary<string, float> EconomicIndicators { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum MarketRegime
    {
        Stable,
        Volatile,
        Crisis
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