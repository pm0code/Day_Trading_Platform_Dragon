// TradingPlatform.FinancialCalculations.Validators.FinancialCalculationValidator
// Comprehensive validation framework for financial calculation inputs and outputs

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.FinancialCalculations.Configuration;
using TradingPlatform.FinancialCalculations.Interfaces;
using TradingPlatform.FinancialCalculations.Models;

namespace TradingPlatform.FinancialCalculations.Validators;

/// <summary>
/// Comprehensive validation service for financial calculations
/// Provides input validation, output validation, data quality checks, and regulatory compliance validation
/// </summary>
public class FinancialCalculationValidator : ICalculationValidator
{
    #region Private Fields

    private readonly ValidationConfiguration _config;
    private readonly ILogger<FinancialCalculationValidator> _logger;
    private readonly List<string> _validationErrors;
    private readonly Dictionary<string, IValidator> _validators;

    #endregion

    #region Constructor

    public FinancialCalculationValidator(
        ValidationConfiguration configuration,
        ILogger<FinancialCalculationValidator> logger)
    {
        _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validationErrors = new List<string>();
        _validators = new Dictionary<string, IValidator>();

        InitializeValidators();
    }

    #endregion

    #region ICalculationValidator Implementation

    /// <summary>
    /// Validate calculation input parameters
    /// </summary>
    public async Task<TradingResult<bool>> ValidateInputAsync<T>(T input, string calculationType) where T : class
    {
        try
        {
            _validationErrors.Clear();

            if (input == null)
            {
                _validationErrors.Add("Input cannot be null");
                return TradingResult<bool>.Failure(new ArgumentNullException(nameof(input)));
            }

            // Perform basic validation
            await ValidateBasicConstraintsAsync(input, calculationType);

            // Perform type-specific validation
            await ValidateTypeSpecificAsync(input, calculationType);

            // Perform data quality validation
            if (_config.EnableDataSanityChecks)
            {
                await ValidateDataQualityAsync(input, calculationType);
            }

            // Perform outlier detection
            if (_config.EnableOutlierDetection)
            {
                await ValidateOutliersAsync(input, calculationType);
            }

            if (_validationErrors.Any())
            {
                var errorMessage = string.Join("; ", _validationErrors);
                _logger.LogWarning("Input validation failed for {CalculationType}: {Errors}", 
                    calculationType, errorMessage);
                return TradingResult<bool>.Failure(new ValidationException(errorMessage));
            }

            _logger.LogDebug("Input validation passed for {CalculationType}", calculationType);
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Input validation failed for {CalculationType}", calculationType);
            return TradingResult<bool>.Failure(ex);
        }
    }

    /// <summary>
    /// Validate calculation output results
    /// </summary>
    public async Task<TradingResult<bool>> ValidateOutputAsync<T>(T output, string calculationType) where T : class
    {
        try
        {
            _validationErrors.Clear();

            if (output == null)
            {
                _validationErrors.Add("Output cannot be null");
                return TradingResult<bool>.Failure(new ArgumentNullException(nameof(output)));
            }

            // Validate base calculation result properties
            if (output is FinancialCalculationResult financialResult)
            {
                await ValidateFinancialCalculationResultAsync(financialResult);
            }

            // Perform type-specific output validation
            await ValidateOutputTypeSpecificAsync(output, calculationType);

            // Validate numerical results
            await ValidateNumericalResultsAsync(output, calculationType);

            // Validate regulatory compliance
            await ValidateRegulatoryComplianceAsync(output, calculationType);

            if (_validationErrors.Any())
            {
                var errorMessage = string.Join("; ", _validationErrors);
                _logger.LogWarning("Output validation failed for {CalculationType}: {Errors}", 
                    calculationType, errorMessage);
                return TradingResult<bool>.Failure(new ValidationException(errorMessage));
            }

            _logger.LogDebug("Output validation passed for {CalculationType}", calculationType);
            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Output validation failed for {CalculationType}", calculationType);
            return TradingResult<bool>.Failure(ex);
        }
    }

    /// <summary>
    /// Validate decimal precision meets requirements
    /// </summary>
    public async Task<TradingResult<bool>> ValidateDecimalPrecisionAsync(decimal value, int requiredPrecision)
    {
        return await Task.Run(() =>
        {
            try
            {
                var scaleFactor = (decimal)Math.Pow(10, requiredPrecision);
                var rounded = Math.Round(value * scaleFactor) / scaleFactor;
                var difference = Math.Abs(value - rounded);

                if (difference >= 1e-10m)
                {
                    var error = $"Decimal precision violation: value {value} does not meet required precision of {requiredPrecision} decimal places";
                    _logger.LogWarning(error);
                    return TradingResult<bool>.Failure(new ValidationException(error));
                }

                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decimal precision validation failed");
                return TradingResult<bool>.Failure(ex);
            }
        });
    }

    /// <summary>
    /// Validate data quality for financial datasets
    /// </summary>
    public async Task<TradingResult<bool>> ValidateDataQualityAsync(IEnumerable<decimal> data, string dataType)
    {
        return await Task.Run(() =>
        {
            try
            {
                _validationErrors.Clear();

                var dataArray = data.ToArray();
                
                if (dataArray.Length == 0)
                {
                    _validationErrors.Add($"{dataType} dataset cannot be empty");
                    return TradingResult<bool>.Failure(new ValidationException($"{dataType} dataset cannot be empty"));
                }

                // Check for invalid values
                var invalidCount = dataArray.Count(d => decimal.IsNaN(d) || decimal.IsInfinity(d));
                if (invalidCount > 0)
                {
                    _validationErrors.Add($"{dataType} dataset contains {invalidCount} invalid values (NaN or Infinity)");
                }

                // Check for missing data ratio
                var missingCount = dataArray.Count(d => d == 0);
                var missingRatio = (double)missingCount / dataArray.Length;
                if (missingRatio > _config.MaxMissingDataRatio)
                {
                    _validationErrors.Add($"{dataType} dataset has {missingRatio:P} missing data, exceeding limit of {_config.MaxMissingDataRatio:P}");
                }

                // Check for data freshness (if applicable)
                if (_config.EnableDataFreshnesChecks && dataType.Contains("Price"))
                {
                    // This would require timestamp information - placeholder for now
                    _logger.LogDebug("Data freshness check for {DataType} - implementation needed", dataType);
                }

                if (_validationErrors.Any())
                {
                    var errorMessage = string.Join("; ", _validationErrors);
                    return TradingResult<bool>.Failure(new ValidationException(errorMessage));
                }

                return TradingResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data quality validation failed for {DataType}", dataType);
                return TradingResult<bool>.Failure(ex);
            }
        });
    }

    /// <summary>
    /// Get all validation errors from the last validation
    /// </summary>
    public async Task<TradingResult<List<string>>> GetValidationErrorsAsync()
    {
        return await Task.FromResult(TradingResult<List<string>>.Success(_validationErrors.ToList()));
    }

    #endregion

    #region Private Validation Methods

    private async Task ValidateBasicConstraintsAsync<T>(T input, string calculationType) where T : class
    {
        // Use DataAnnotations for basic validation
        var validationContext = new ValidationContext(input);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(input, validationContext, validationResults, true))
        {
            foreach (var result in validationResults)
            {
                _validationErrors.Add(result.ErrorMessage ?? "Unknown validation error");
            }
        }

        // Use FluentValidation if available
        if (_validators.TryGetValue(calculationType.ToUpperInvariant(), out var validator))
        {
            var fluentResult = await validator.ValidateAsync(new ValidationContext<object>(input));
            if (!fluentResult.IsValid)
            {
                _validationErrors.AddRange(fluentResult.Errors.Select(e => e.ErrorMessage));
            }
        }
    }

    private async Task ValidateTypeSpecificAsync<T>(T input, string calculationType) where T : class
    {
        await Task.Run(() =>
        {
            switch (calculationType.ToUpperInvariant())
            {
                case "PORTFOLIOMETRICS":
                    ValidatePortfolioInput(input);
                    break;
                    
                case "OPTIONPRICING":
                case "BLACKSCHOLES":
                case "MONTECARLO":
                    ValidateOptionPricingInput(input);
                    break;
                    
                case "RISKMETRICS":
                case "VAR":
                    ValidateRiskInput(input);
                    break;
                    
                case "TECHNICALINDICATOR":
                    ValidateTechnicalIndicatorInput(input);
                    break;
                    
                default:
                    _logger.LogDebug("No specific validation available for calculation type: {CalculationType}", calculationType);
                    break;
            }
        });
    }

    private void ValidatePortfolioInput<T>(T input) where T : class
    {
        if (input is ValueTuple<List<PositionData>, Dictionary<string, decimal>> portfolioParams)
        {
            var (positions, prices) = portfolioParams;
            
            if (positions.Count == 0)
            {
                _validationErrors.Add("Portfolio position list cannot be empty");
                return;
            }

            if (prices.Count == 0)
            {
                _validationErrors.Add("Price dictionary cannot be empty");
                return;
            }

            foreach (var position in positions)
            {
                ValidatePosition(position);
            }

            foreach (var price in prices)
            {
                ValidatePrice(price.Key, price.Value);
            }
        }
        else if (input is List<PositionData> positions)
        {
            foreach (var position in positions)
            {
                ValidatePosition(position);
            }
        }
    }

    private void ValidatePosition(PositionData position)
    {
        if (string.IsNullOrWhiteSpace(position.Symbol))
        {
            _validationErrors.Add("Position symbol cannot be null or empty");
        }
        else if (!IsValidSymbol(position.Symbol))
        {
            _validationErrors.Add($"Invalid position symbol format: {position.Symbol}");
        }

        if (position.Quantity == 0)
        {
            _validationErrors.Add($"Position quantity cannot be zero for {position.Symbol}");
        }

        if (Math.Abs(position.Quantity) > _config.MaxQuantity)
        {
            _validationErrors.Add($"Position quantity {position.Quantity} exceeds maximum limit of {_config.MaxQuantity} for {position.Symbol}");
        }

        if (position.AveragePrice <= 0)
        {
            _validationErrors.Add($"Average price must be positive for {position.Symbol}");
        }

        if (position.AveragePrice > _config.MaxPriceValue)
        {
            _validationErrors.Add($"Average price {position.AveragePrice} exceeds maximum limit of {_config.MaxPriceValue} for {position.Symbol}");
        }

        if (position.CurrentPrice <= 0)
        {
            _validationErrors.Add($"Current price must be positive for {position.Symbol}");
        }

        if (position.CurrentPrice > _config.MaxPriceValue)
        {
            _validationErrors.Add($"Current price {position.CurrentPrice} exceeds maximum limit of {_config.MaxPriceValue} for {position.Symbol}");
        }

        var marketValue = Math.Abs(position.Quantity * position.CurrentPrice);
        if (marketValue > _config.MaxPortfolioValue)
        {
            _validationErrors.Add($"Position market value {marketValue:C} exceeds maximum portfolio value limit for {position.Symbol}");
        }
    }

    private void ValidatePrice(string symbol, decimal price)
    {
        if (price <= 0)
        {
            _validationErrors.Add($"Price must be positive for {symbol}");
        }

        if (price > _config.MaxPriceValue)
        {
            _validationErrors.Add($"Price {price} exceeds maximum limit of {_config.MaxPriceValue} for {symbol}");
        }
    }

    private void ValidateOptionPricingInput<T>(T input) where T : class
    {
        if (input is OptionPricingParameters optionParams)
        {
            if (string.IsNullOrWhiteSpace(optionParams.Symbol))
            {
                _validationErrors.Add("Option symbol cannot be null or empty");
            }

            if (optionParams.Strike <= 0)
            {
                _validationErrors.Add("Strike price must be positive");
            }

            if (optionParams.SpotPrice <= 0)
            {
                _validationErrors.Add("Spot price must be positive");
            }

            if (optionParams.TimeToExpiry <= 0)
            {
                _validationErrors.Add("Time to expiry must be positive");
            }

            if (optionParams.TimeToExpiry > 10) // More than 10 years
            {
                _validationErrors.Add("Time to expiry cannot exceed 10 years");
            }

            if (optionParams.RiskFreeRate < 0)
            {
                _validationErrors.Add("Risk-free rate cannot be negative");
            }

            if (optionParams.RiskFreeRate > _config.MaxInterestRate)
            {
                _validationErrors.Add($"Risk-free rate {optionParams.RiskFreeRate:P} exceeds maximum limit of {_config.MaxInterestRate:P}");
            }

            if (optionParams.Volatility <= 0)
            {
                _validationErrors.Add("Volatility must be positive");
            }

            if (optionParams.Volatility > _config.MaxVolatility)
            {
                _validationErrors.Add($"Volatility {optionParams.Volatility:P} exceeds maximum limit of {_config.MaxVolatility:P}");
            }

            if (optionParams.DividendYield < 0)
            {
                _validationErrors.Add("Dividend yield cannot be negative");
            }
        }
    }

    private void ValidateRiskInput<T>(T input) where T : class
    {
        // Risk-specific validation logic
        if (input is ValueTuple<List<PositionData>, Dictionary<string, List<decimal>>> riskParams)
        {
            var (positions, priceHistory) = riskParams;
            
            ValidatePortfolioInput(positions);
            
            foreach (var history in priceHistory)
            {
                if (history.Value.Count < _config.MinDataPointsForOutlierDetection)
                {
                    _validationErrors.Add($"Insufficient price history for {history.Key}: {history.Value.Count} points (minimum {_config.MinDataPointsForOutlierDetection})");
                }

                foreach (var price in history.Value)
                {
                    ValidatePrice(history.Key, price);
                }
            }
        }
    }

    private void ValidateTechnicalIndicatorInput<T>(T input) where T : class
    {
        // Technical indicator specific validation
        if (input is List<decimal> prices)
        {
            if (prices.Count < 2)
            {
                _validationErrors.Add("Technical indicator requires at least 2 price points");
            }

            foreach (var price in prices)
            {
                if (price <= 0)
                {
                    _validationErrors.Add("All prices must be positive for technical indicators");
                    break;
                }
            }
        }
    }

    private async Task ValidateDataQualityAsync<T>(T input, string calculationType) where T : class
    {
        await Task.Run(() =>
        {
            // Extract numeric data for quality checks
            var numericData = ExtractNumericData(input);
            
            if (numericData.Any())
            {
                // Check for extreme values
                var min = numericData.Min();
                var max = numericData.Max();
                var range = max - min;
                
                if (range == 0)
                {
                    _validationErrors.Add("Data has no variance (all values are identical)");
                }
                
                // Check for suspicious patterns
                var uniqueValues = numericData.Distinct().Count();
                var totalValues = numericData.Count();
                
                if (uniqueValues == 1 && totalValues > 1)
                {
                    _validationErrors.Add("All data values are identical - possible data quality issue");
                }
                
                if (uniqueValues < totalValues * 0.1 && totalValues > 10)
                {
                    _validationErrors.Add("Very low data variance detected - possible data quality issue");
                }
            }
        });
    }

    private async Task ValidateOutliersAsync<T>(T input, string calculationType) where T : class
    {
        await Task.Run(() =>
        {
            var numericData = ExtractNumericData(input);
            
            if (numericData.Count >= _config.MinDataPointsForOutlierDetection)
            {
                var outliers = DetectOutliers(numericData);
                
                if (outliers.Any())
                {
                    var outlierCount = outliers.Count();
                    var outlierRatio = (double)outlierCount / numericData.Count;
                    
                    if (outlierRatio > 0.05) // More than 5% outliers
                    {
                        if (_config.EnableOutlierRemoval)
                        {
                            _validationErrors.Add($"High outlier ratio detected: {outlierRatio:P} ({outlierCount} outliers)");
                        }
                        else
                        {
                            _logger.LogWarning("Outliers detected but removal is disabled: {OutlierCount} outliers ({OutlierRatio:P})", 
                                outlierCount, outlierRatio);
                        }
                    }
                }
            }
        });
    }

    private async Task ValidateFinancialCalculationResultAsync(FinancialCalculationResult result)
    {
        await Task.Run(() =>
        {
            if (string.IsNullOrEmpty(result.CalculationId))
            {
                _validationErrors.Add("Calculation ID cannot be null or empty");
            }

            if (string.IsNullOrEmpty(result.CalculationType))
            {
                _validationErrors.Add("Calculation type cannot be null or empty");
            }

            if (result.CalculatedAt == default)
            {
                _validationErrors.Add("Calculation timestamp cannot be default");
            }

            if (result.CalculationTimeMs < 0)
            {
                _validationErrors.Add("Calculation time cannot be negative");
            }

            if (result.DecimalPrecision < 0 || result.DecimalPrecision > 10)
            {
                _validationErrors.Add("Decimal precision must be between 0 and 10");
            }
        });
    }

    private async Task ValidateOutputTypeSpecificAsync<T>(T output, string calculationType) where T : class
    {
        await Task.Run(() =>
        {
            switch (calculationType.ToUpperInvariant())
            {
                case "PORTFOLIOMETRICS":
                    if (output is PortfolioCalculationResult portfolioResult)
                    {
                        ValidatePortfolioCalculationResult(portfolioResult);
                    }
                    break;
                    
                case "OPTIONPRICING":
                case "BLACKSCHOLES":
                case "MONTECARLO":
                    if (output is OptionPricingResult optionResult)
                    {
                        ValidateOptionPricingResult(optionResult);
                    }
                    break;
                    
                case "RISKMETRICS":
                    if (output is RiskMetrics riskResult)
                    {
                        ValidateRiskMetricsResult(riskResult);
                    }
                    break;
            }
        });
    }

    private void ValidatePortfolioCalculationResult(PortfolioCalculationResult result)
    {
        if (result.TotalValue < 0)
        {
            _validationErrors.Add("Portfolio total value cannot be negative");
        }

        if (Math.Abs(result.TotalReturnPercent) > 1000) // More than 1000% return
        {
            _validationErrors.Add($"Portfolio return percentage {result.TotalReturnPercent:F2}% seems unrealistic");
        }

        var totalWeight = result.Positions.Sum(p => p.PositionWeight);
        if (Math.Abs(totalWeight - 100) > 0.1m) // Allow small rounding differences
        {
            _validationErrors.Add($"Position weights sum to {totalWeight:F2}% instead of 100%");
        }
    }

    private void ValidateOptionPricingResult(OptionPricingResult result)
    {
        if (result.TheoreticalPrice < 0)
        {
            _validationErrors.Add("Option theoretical price cannot be negative");
        }

        if (Math.Abs(result.Delta) > 1.0m)
        {
            _validationErrors.Add($"Option delta {result.Delta:F4} is outside the valid range [-1, 1]");
        }

        if (result.Gamma < 0)
        {
            _validationErrors.Add("Option gamma cannot be negative");
        }

        if (result.ImpliedVolatility < 0 || result.ImpliedVolatility > 10.0m)
        {
            _validationErrors.Add($"Implied volatility {result.ImpliedVolatility:P} is outside reasonable range [0%, 1000%]");
        }
    }

    private void ValidateRiskMetricsResult(RiskMetrics result)
    {
        if (result.PortfolioValue <= 0)
        {
            _validationErrors.Add("Portfolio value must be positive for risk metrics");
        }

        if (result.PortfolioVolatility < 0)
        {
            _validationErrors.Add("Portfolio volatility cannot be negative");
        }

        if (result.VaR95 < 0)
        {
            _validationErrors.Add("VaR95 should be positive (representing potential loss)");
        }

        if (result.MaxDrawdown < 0)
        {
            _validationErrors.Add("Maximum drawdown should be positive");
        }

        if (result.ConcentrationRisk < 0 || result.ConcentrationRisk > 1)
        {
            _validationErrors.Add($"Concentration risk {result.ConcentrationRisk:F4} should be between 0 and 1");
        }
    }

    private async Task ValidateNumericalResultsAsync<T>(T output, string calculationType) where T : class
    {
        await Task.Run(() =>
        {
            var numericData = ExtractNumericData(output);
            
            foreach (var value in numericData)
            {
                if (decimal.IsNaN(value) || decimal.IsInfinity(value))
                {
                    _validationErrors.Add($"Calculation result contains invalid numeric value: {value}");
                }
            }
        });
    }

    private async Task ValidateRegulatoryComplianceAsync<T>(T output, string calculationType) where T : class
    {
        await Task.Run(() =>
        {
            // SOX compliance - decimal precision for financial values
            if (output is PortfolioCalculationResult portfolioResult)
            {
                if (!ValidateDecimalPrecision(portfolioResult.TotalValue, 2).Result.IsSuccess)
                {
                    _validationErrors.Add("SOX compliance violation: Total value precision");
                }
                
                if (!ValidateDecimalPrecision(portfolioResult.UnrealizedPnL, 2).Result.IsSuccess)
                {
                    _validationErrors.Add("SOX compliance violation: P&L precision");
                }
            }
            
            // Basel III compliance - risk metrics
            if (output is RiskMetrics riskResult)
            {
                if (riskResult.VaR95 > riskResult.PortfolioValue * 0.20m)
                {
                    _validationErrors.Add("Basel III compliance violation: VaR95 exceeds 20% of portfolio value");
                }
                
                if (riskResult.LeverageRatio > 3.0m)
                {
                    _validationErrors.Add("Basel III compliance violation: Leverage ratio exceeds 3.0");
                }
            }
        });
    }

    #endregion

    #region Helper Methods

    private bool IsValidSymbol(string symbol)
    {
        // Basic symbol validation - alphanumeric, possibly with dots or hyphens
        return Regex.IsMatch(symbol, @"^[A-Z0-9\.\-]{1,10}$");
    }

    private List<decimal> ExtractNumericData<T>(T input) where T : class
    {
        var numericData = new List<decimal>();
        
        var properties = typeof(T).GetProperties()
            .Where(p => p.PropertyType == typeof(decimal) || 
                       p.PropertyType == typeof(decimal?) ||
                       p.PropertyType == typeof(double) ||
                       p.PropertyType == typeof(float));
        
        foreach (var property in properties)
        {
            try
            {
                var value = property.GetValue(input);
                if (value != null)
                {
                    if (value is decimal decValue)
                        numericData.Add(decValue);
                    else if (value is double doubleValue)
                        numericData.Add((decimal)doubleValue);
                    else if (value is float floatValue)
                        numericData.Add((decimal)floatValue);
                }
            }
            catch
            {
                // Ignore properties that can't be accessed
            }
        }
        
        return numericData;
    }

    private IEnumerable<decimal> DetectOutliers(List<decimal> data)
    {
        if (data.Count < 3) return Enumerable.Empty<decimal>();
        
        var mean = data.Average();
        var variance = data.Select(x => (x - mean) * (x - mean)).Average();
        var stdDev = (decimal)Math.Sqrt((double)variance);
        
        var threshold = (decimal)_config.OutlierThresholdStdDev * stdDev;
        
        return data.Where(x => Math.Abs(x - mean) > threshold);
    }

    private void InitializeValidators()
    {
        // Initialize FluentValidation validators
        _validators.Add("PORTFOLIOMETRICS", new PortfolioParametersValidator());
        _validators.Add("OPTIONPRICING", new OptionPricingParametersValidator());
        _validators.Add("BLACKSCHOLES", new OptionPricingParametersValidator());
        _validators.Add("MONTECARLO", new MonteCarloParametersValidator());
    }

    #endregion
}

#region FluentValidation Validators

/// <summary>
/// FluentValidation validator for portfolio parameters
/// </summary>
public class PortfolioParametersValidator : AbstractValidator<object>
{
    public PortfolioParametersValidator()
    {
        // Add fluent validation rules as needed
        RuleFor(x => x).NotNull().WithMessage("Portfolio parameters cannot be null");
    }
}

/// <summary>
/// FluentValidation validator for option pricing parameters
/// </summary>
public class OptionPricingParametersValidator : AbstractValidator<object>
{
    public OptionPricingParametersValidator()
    {
        // Add fluent validation rules as needed
        RuleFor(x => x).NotNull().WithMessage("Option pricing parameters cannot be null");
    }
}

/// <summary>
/// FluentValidation validator for Monte Carlo parameters
/// </summary>
public class MonteCarloParametersValidator : AbstractValidator<object>
{
    public MonteCarloParametersValidator()
    {
        // Add fluent validation rules as needed
        RuleFor(x => x).NotNull().WithMessage("Monte Carlo parameters cannot be null");
    }
}

#endregion