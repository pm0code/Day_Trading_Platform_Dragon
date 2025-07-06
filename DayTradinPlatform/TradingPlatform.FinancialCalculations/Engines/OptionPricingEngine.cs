// TradingPlatform.FinancialCalculations.Engines.OptionPricingEngine
// GPU-accelerated option pricing engine with Black-Scholes, Monte Carlo, and Binomial models

using System.Collections.Concurrent;
using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.FinancialCalculations.Canonical;
using TradingPlatform.FinancialCalculations.Configuration;
using TradingPlatform.FinancialCalculations.Compliance;
using TradingPlatform.FinancialCalculations.Interfaces;
using TradingPlatform.FinancialCalculations.Kernels;
using TradingPlatform.FinancialCalculations.Models;

namespace TradingPlatform.FinancialCalculations.Engines;

/// <summary>
/// High-performance option pricing engine with GPU acceleration
/// Supports Black-Scholes, Monte Carlo, and Binomial Tree models
/// Provides comprehensive Greeks calculation and implied volatility solving
/// </summary>
public class OptionPricingEngine : CanonicalFinancialCalculatorBase, IOptionPricingService
{
    #region Private Fields

    private readonly ConcurrentDictionary<string, OptionPricingCache> _pricingCache;
    private readonly Random _random;
    private readonly object _calculationLock = new();
    
    // GPU kernels
    private Action<Index1D, MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                    MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                    MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<byte, Stride1D.Dense>,
                    MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                    MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                    MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>>? _blackScholesKernel;

    private Action<Index1D, MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                    MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                    MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<byte, Stride1D.Dense>,
                    MemoryBuffer2D<long, Stride2D.DenseX>, MemoryBuffer2D<long, Stride2D.DenseX>,
                    MemoryBuffer2D<long, Stride2D.DenseX>, MemoryBuffer1D<long, Stride1D.Dense>,
                    MemoryBuffer1D<long, Stride1D.Dense>, int>? _monteCarloKernel;

    #endregion

    #region Constructor

    public OptionPricingEngine(
        FinancialCalculationConfiguration configuration,
        IComplianceAuditor complianceAuditor,
        Dictionary<string, string>? metadata = null)
        : base("OptionPricingEngine", configuration, complianceAuditor, null, metadata)
    {
        _pricingCache = new ConcurrentDictionary<string, OptionPricingCache>();
        _random = new Random();
    }

    #endregion

    #region Lifecycle Management

    protected override async Task OnInitializeCalculationEngineAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            if (_gpuInitialized && _gpuAccelerator != null)
            {
                try
                {
                    // Load Black-Scholes kernel
                    _blackScholesKernel = _gpuAccelerator.LoadAutoGroupedStreamKernel<
                        Index1D, MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                        MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                        MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<byte, Stride1D.Dense>,
                        MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                        MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                        MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>>(
                        AdvancedFinancialKernels.BlackScholesKernel);

                    // Load Monte Carlo kernel
                    _monteCarloKernel = _gpuAccelerator.LoadAutoGroupedStreamKernel<
                        Index1D, MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                        MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<long, Stride1D.Dense>,
                        MemoryBuffer1D<long, Stride1D.Dense>, MemoryBuffer1D<byte, Stride1D.Dense>,
                        MemoryBuffer2D<long, Stride2D.DenseX>, MemoryBuffer2D<long, Stride2D.DenseX>,
                        MemoryBuffer2D<long, Stride2D.DenseX>, MemoryBuffer1D<long, Stride1D.Dense>,
                        MemoryBuffer1D<long, Stride1D.Dense>, int>(
                        AdvancedFinancialKernels.MonteCarloOptionKernel);

                    Logger.LogInfo("Option pricing GPU kernels loaded successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Failed to load GPU kernels, will use CPU fallback", ex);
                }
            }
        }, cancellationToken);
    }

    protected override Task OnStartCalculationEngineAsync(CancellationToken cancellationToken)
    {
        Logger.LogInfo("Option pricing engine started");
        return Task.CompletedTask;
    }

    protected override Task OnStopCalculationEngineAsync(CancellationToken cancellationToken)
    {
        _pricingCache.Clear();
        Logger.LogInfo("Option pricing engine stopped");
        return Task.CompletedTask;
    }

    protected override Task<Dictionary<string, HealthCheckEntry>> OnCheckCalculationEngineHealthAsync()
    {
        var checks = new Dictionary<string, HealthCheckEntry>
        {
            ["pricing_cache"] = new HealthCheckEntry
            {
                Status = HealthStatus.Healthy,
                Description = $"Pricing cache contains {_pricingCache.Count} entries"
            },
            ["gpu_kernels"] = new HealthCheckEntry
            {
                Status = _blackScholesKernel != null && _monteCarloKernel != null ? HealthStatus.Healthy : HealthStatus.Degraded,
                Description = _blackScholesKernel != null && _monteCarloKernel != null ? "GPU kernels loaded" : "Using CPU fallback"
            }
        };

        return Task.FromResult(checks);
    }

    protected override void OnValidateCalculationInput<T>(T input, string parameterName)
    {
        // Option-specific validation is done in individual methods
    }

    #endregion

    #region IOptionPricingService Implementation

    /// <summary>
    /// Calculate option price and Greeks using Black-Scholes model
    /// </summary>
    public async Task<TradingResult<OptionPricingResult>> CalculateBlackScholesAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        decimal dividendYield = 0,
        CancellationToken cancellationToken = default)
    {
        return await TrackOperationAsync("CalculateBlackScholes", async () =>
        {
            ValidateOptionInputs(symbol, strike, spotPrice, timeToExpiry, riskFreeRate, volatility);

            var cacheKey = GenerateBlackScholesCacheKey(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, volatility, dividendYield);

            return await GetOrCalculateAsync(cacheKey, async () =>
            {
                var sw = Stopwatch.StartNew();

                try
                {
                    var result = await ExecuteWithGpuAsync(
                        "BlackScholes",
                        async accelerator => await CalculateBlackScholesGpuAsync(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, volatility, dividendYield, accelerator),
                        async () => await CalculateBlackScholesCpuAsync(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, volatility, dividendYield),
                        cancellationToken);

                    sw.Stop();
                    RecordPerformanceMetric("CalculateBlackScholes", sw.Elapsed.TotalMilliseconds, _gpuInitialized);

                    return TradingResult<OptionPricingResult>.Success(result);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Black-Scholes calculation failed", ex);
                    return TradingResult<OptionPricingResult>.Failure(ex);
                }
            });
        });
    }

    /// <summary>
    /// Calculate option price using Monte Carlo simulation
    /// </summary>
    public async Task<TradingResult<OptionPricingResult>> CalculateMonteCarloAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        int simulations,
        decimal dividendYield = 0,
        CancellationToken cancellationToken = default)
    {
        return await TrackOperationAsync("CalculateMonteCarlo", async () =>
        {
            ValidateOptionInputs(symbol, strike, spotPrice, timeToExpiry, riskFreeRate, volatility);

            if (simulations <= 0 || simulations > Configuration.RiskConfiguration.MaxMonteCarloSimulations)
                return TradingResult<OptionPricingResult>.Failure(new ArgumentException($"Simulations must be between 1 and {Configuration.RiskConfiguration.MaxMonteCarloSimulations}"));

            var cacheKey = GenerateMonteCarloCacheKey(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, volatility, simulations, dividendYield);

            return await GetOrCalculateAsync(cacheKey, async () =>
            {
                var sw = Stopwatch.StartNew();

                try
                {
                    var result = await ExecuteWithGpuAsync(
                        "MonteCarlo",
                        async accelerator => await CalculateMonteCarloGpuAsync(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, volatility, simulations, dividendYield, accelerator),
                        async () => await CalculateMonteCarloCpuAsync(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, volatility, simulations, dividendYield),
                        cancellationToken);

                    sw.Stop();
                    RecordPerformanceMetric("CalculateMonteCarlo", sw.Elapsed.TotalMilliseconds, _gpuInitialized);

                    return TradingResult<OptionPricingResult>.Success(result);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Monte Carlo calculation failed", ex);
                    return TradingResult<OptionPricingResult>.Failure(ex);
                }
            });
        });
    }

    /// <summary>
    /// Calculate option price using Binomial Tree model
    /// </summary>
    public async Task<TradingResult<OptionPricingResult>> CalculateBinomialTreeAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        int steps,
        decimal dividendYield = 0,
        CancellationToken cancellationToken = default)
    {
        return await TrackOperationAsync("CalculateBinomialTree", async () =>
        {
            ValidateOptionInputs(symbol, strike, spotPrice, timeToExpiry, riskFreeRate, volatility);

            if (steps <= 0 || steps > 1000)
                return TradingResult<OptionPricingResult>.Failure(new ArgumentException("Steps must be between 1 and 1000"));

            var cacheKey = GenerateBinomialCacheKey(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, volatility, steps, dividendYield);

            return await GetOrCalculateAsync(cacheKey, async () =>
            {
                var sw = Stopwatch.StartNew();

                try
                {
                    // Binomial tree is CPU-only for now
                    var result = await CalculateBinomialTreeCpuAsync(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, volatility, steps, dividendYield);

                    sw.Stop();
                    RecordPerformanceMetric("CalculateBinomialTree", sw.Elapsed.TotalMilliseconds, false);

                    return TradingResult<OptionPricingResult>.Success(result);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Binomial tree calculation failed", ex);
                    return TradingResult<OptionPricingResult>.Failure(ex);
                }
            });
        });
    }

    /// <summary>
    /// Calculate implied volatility using Newton-Raphson method
    /// </summary>
    public async Task<TradingResult<decimal>> CalculateImpliedVolatilityAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal marketPrice,
        decimal dividendYield = 0,
        CancellationToken cancellationToken = default)
    {
        return await TrackOperationAsync("CalculateImpliedVolatility", async () =>
        {
            ValidateOptionInputs(symbol, strike, spotPrice, timeToExpiry, riskFreeRate, 0.20m);

            if (marketPrice <= 0)
                return TradingResult<decimal>.Failure(new ArgumentException("Market price must be positive"));

            var sw = Stopwatch.StartNew();

            try
            {
                var impliedVol = await CalculateImpliedVolatilityCpuAsync(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, marketPrice, dividendYield);

                sw.Stop();
                RecordPerformanceMetric("CalculateImpliedVolatility", sw.Elapsed.TotalMilliseconds, false);

                return TradingResult<decimal>.Success(impliedVol);
            }
            catch (Exception ex)
            {
                Logger.LogError("Implied volatility calculation failed", ex);
                return TradingResult<decimal>.Failure(ex);
            }
        });
    }

    #endregion

    #region GPU Implementations

    private async Task<OptionPricingResult> CalculateBlackScholesGpuAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        decimal dividendYield,
        Accelerator accelerator)
    {
        // Prepare input data (single option)
        var spotPrices = new[] { ToScaledInteger(spotPrice, 4) };
        var strikes = new[] { ToScaledInteger(strike, 4) };
        var timesToExpiry = new[] { ToScaledInteger(timeToExpiry, 8) };
        var riskFreeRates = new[] { ToScaledInteger(riskFreeRate, 8) };
        var volatilities = new[] { ToScaledInteger(volatility, 6) };
        var optionTypes = new byte[] { (byte)(optionType == OptionType.Call ? 0 : 1) };

        // Allocate GPU memory
        using var spotPricesBuffer = accelerator.Allocate1D<long>(spotPrices);
        using var strikesBuffer = accelerator.Allocate1D<long>(strikes);
        using var timesToExpiryBuffer = accelerator.Allocate1D<long>(timesToExpiry);
        using var riskFreeRatesBuffer = accelerator.Allocate1D<long>(riskFreeRates);
        using var volatilitiesBuffer = accelerator.Allocate1D<long>(volatilities);
        using var optionTypesBuffer = accelerator.Allocate1D<byte>(optionTypes);

        // Output buffers
        using var optionPricesBuffer = accelerator.Allocate1D<long>(1);
        using var deltaBuffer = accelerator.Allocate1D<long>(1);
        using var gammaBuffer = accelerator.Allocate1D<long>(1);
        using var thetaBuffer = accelerator.Allocate1D<long>(1);
        using var vegaBuffer = accelerator.Allocate1D<long>(1);
        using var rhoBuffer = accelerator.Allocate1D<long>(1);

        // Execute GPU kernel
        if (_blackScholesKernel != null)
        {
            _blackScholesKernel(1, spotPricesBuffer.View, strikesBuffer.View, timesToExpiryBuffer.View,
                              riskFreeRatesBuffer.View, volatilitiesBuffer.View, optionTypesBuffer.View,
                              optionPricesBuffer.View, deltaBuffer.View, gammaBuffer.View,
                              thetaBuffer.View, vegaBuffer.View, rhoBuffer.View);
        }

        accelerator.Synchronize();

        // Retrieve results
        var optionPrices = optionPricesBuffer.GetAsArray1D();
        var deltas = deltaBuffer.GetAsArray1D();
        var gammas = gammaBuffer.GetAsArray1D();
        var thetas = thetaBuffer.GetAsArray1D();
        var vegas = vegaBuffer.GetAsArray1D();
        var rhos = rhoBuffer.GetAsArray1D();

        return new OptionPricingResult
        {
            Symbol = symbol,
            OptionType = optionType,
            Strike = strike,
            SpotPrice = spotPrice,
            TimeToExpiry = timeToExpiry,
            RiskFreeRate = riskFreeRate,
            Volatility = volatility,
            DividendYield = dividendYield,
            TheoreticalPrice = RoundToRegulatory(FromScaledInteger(optionPrices[0], 4)),
            Delta = RoundToRegulatory(FromScaledInteger(deltas[0], 8), 6),
            Gamma = RoundToRegulatory(FromScaledInteger(gammas[0], 8), 6),
            Theta = RoundToRegulatory(FromScaledInteger(thetas[0], 8), 6),
            Vega = RoundToRegulatory(FromScaledInteger(vegas[0], 8), 6),
            Rho = RoundToRegulatory(FromScaledInteger(rhos[0], 8), 6),
            CalculationType = "BlackScholes_GPU",
            UsedGpuAcceleration = true,
            ServiceName = ServiceName
        };
    }

    private async Task<OptionPricingResult> CalculateMonteCarloGpuAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        int simulations,
        decimal dividendYield,
        Accelerator accelerator)
    {
        // Generate random numbers for Monte Carlo
        var randomNumbers = GenerateRandomNumbers(1, simulations);

        // Prepare input data
        var spotPrices = new[] { ToScaledInteger(spotPrice, 4) };
        var strikes = new[] { ToScaledInteger(strike, 4) };
        var timesToExpiry = new[] { ToScaledInteger(timeToExpiry, 8) };
        var riskFreeRates = new[] { ToScaledInteger(riskFreeRate, 8) };
        var volatilities = new[] { ToScaledInteger(volatility, 6) };
        var optionTypes = new byte[] { (byte)(optionType == OptionType.Call ? 0 : 1) };

        // Allocate GPU memory
        using var spotPricesBuffer = accelerator.Allocate1D<long>(spotPrices);
        using var strikesBuffer = accelerator.Allocate1D<long>(strikes);
        using var timesToExpiryBuffer = accelerator.Allocate1D<long>(timesToExpiry);
        using var riskFreeRatesBuffer = accelerator.Allocate1D<long>(riskFreeRates);
        using var volatilitiesBuffer = accelerator.Allocate1D<long>(volatilities);
        using var optionTypesBuffer = accelerator.Allocate1D<byte>(optionTypes);
        using var randomNumbersBuffer = accelerator.Allocate2DDenseX<long>(new Index2D(1, simulations));
        using var simulatedPricesBuffer = accelerator.Allocate2DDenseX<long>(new Index2D(1, simulations));
        using var payoffsBuffer = accelerator.Allocate2DDenseX<long>(new Index2D(1, simulations));
        using var optionPricesBuffer = accelerator.Allocate1D<long>(1);
        using var standardErrorsBuffer = accelerator.Allocate1D<long>(1);

        randomNumbersBuffer.CopyFromCPU(randomNumbers);

        // Execute GPU kernel
        if (_monteCarloKernel != null)
        {
            _monteCarloKernel(1, spotPricesBuffer.View, strikesBuffer.View, timesToExpiryBuffer.View,
                            riskFreeRatesBuffer.View, volatilitiesBuffer.View, optionTypesBuffer.View,
                            randomNumbersBuffer.View, simulatedPricesBuffer.View, payoffsBuffer.View,
                            optionPricesBuffer.View, standardErrorsBuffer.View, simulations);
        }

        accelerator.Synchronize();

        // Retrieve results
        var optionPrices = optionPricesBuffer.GetAsArray1D();
        var standardErrors = standardErrorsBuffer.GetAsArray1D();

        var theoreticalPrice = RoundToRegulatory(FromScaledInteger(optionPrices[0], 4));
        var standardError = RoundToRegulatory(FromScaledInteger(standardErrors[0], 4));

        return new OptionPricingResult
        {
            Symbol = symbol,
            OptionType = optionType,
            Strike = strike,
            SpotPrice = spotPrice,
            TimeToExpiry = timeToExpiry,
            RiskFreeRate = riskFreeRate,
            Volatility = volatility,
            DividendYield = dividendYield,
            TheoreticalPrice = theoreticalPrice,
            StandardError = standardError,
            MonteCarloSimulations = simulations,
            ConfidenceInterval95Low = theoreticalPrice - 1.96m * standardError,
            ConfidenceInterval95High = theoreticalPrice + 1.96m * standardError,
            CalculationType = "MonteCarlo_GPU",
            UsedGpuAcceleration = true,
            ServiceName = ServiceName
        };
    }

    #endregion

    #region CPU Implementations

    private async Task<OptionPricingResult> CalculateBlackScholesCpuAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        decimal dividendYield)
    {
        return await Task.Run(() =>
        {
            // Black-Scholes formula implementation
            var d1 = (Math.Log((double)(spotPrice / strike)) + (double)(riskFreeRate - dividendYield + 0.5m * volatility * volatility) * (double)timeToExpiry) /
                     ((double)volatility * Math.Sqrt((double)timeToExpiry));
            var d2 = d1 - (double)volatility * Math.Sqrt((double)timeToExpiry);

            var nd1 = CumulativeNormalDistribution(d1);
            var nd2 = CumulativeNormalDistribution(d2);
            var negNd1 = CumulativeNormalDistribution(-d1);
            var negNd2 = CumulativeNormalDistribution(-d2);

            var discountFactor = Math.Exp(-(double)riskFreeRate * (double)timeToExpiry);
            var dividendDiscountFactor = Math.Exp(-(double)dividendYield * (double)timeToExpiry);

            decimal theoreticalPrice;
            decimal delta;
            decimal rho;

            if (optionType == OptionType.Call)
            {
                theoreticalPrice = (decimal)(dividendDiscountFactor * (double)spotPrice * nd1 - (double)strike * discountFactor * nd2);
                delta = (decimal)(dividendDiscountFactor * nd1);
                rho = (decimal)((double)strike * (double)timeToExpiry * discountFactor * nd2 / 100);
            }
            else
            {
                theoreticalPrice = (decimal)((double)strike * discountFactor * negNd2 - dividendDiscountFactor * (double)spotPrice * negNd1);
                delta = (decimal)(dividendDiscountFactor * (nd1 - 1));
                rho = (decimal)(-(double)strike * (double)timeToExpiry * discountFactor * negNd2 / 100);
            }

            // Calculate Greeks
            var pdf = NormalPDF(d1);
            var gamma = (decimal)(dividendDiscountFactor * pdf / ((double)spotPrice * (double)volatility * Math.Sqrt((double)timeToExpiry)));
            var theta = (decimal)((-dividendDiscountFactor * (double)spotPrice * pdf * (double)volatility / (2 * Math.Sqrt((double)timeToExpiry))
                                 - (double)riskFreeRate * (double)strike * discountFactor * (optionType == OptionType.Call ? nd2 : negNd2)
                                 + (double)dividendYield * dividendDiscountFactor * (double)spotPrice * (optionType == OptionType.Call ? nd1 : negNd1)) / 365);
            var vega = (decimal)(dividendDiscountFactor * (double)spotPrice * pdf * Math.Sqrt((double)timeToExpiry) / 100);

            return new OptionPricingResult
            {
                Symbol = symbol,
                OptionType = optionType,
                Strike = strike,
                SpotPrice = spotPrice,
                TimeToExpiry = timeToExpiry,
                RiskFreeRate = riskFreeRate,
                Volatility = volatility,
                DividendYield = dividendYield,
                TheoreticalPrice = RoundToRegulatory(theoreticalPrice),
                Delta = RoundToRegulatory(delta, 6),
                Gamma = RoundToRegulatory(gamma, 6),
                Theta = RoundToRegulatory(theta, 6),
                Vega = RoundToRegulatory(vega, 6),
                Rho = RoundToRegulatory(rho, 6),
                CalculationType = "BlackScholes_CPU",
                UsedGpuAcceleration = false,
                ServiceName = ServiceName
            };
        });
    }

    private async Task<OptionPricingResult> CalculateMonteCarloCpuAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        int simulations,
        decimal dividendYield)
    {
        return await Task.Run(() =>
        {
            var payoffs = new decimal[simulations];
            var random = new Random();

            for (int i = 0; i < simulations; i++)
            {
                // Generate random number using Box-Muller transform
                var u1 = random.NextDouble();
                var u2 = random.NextDouble();
                var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

                // Calculate final stock price
                var drift = (double)(riskFreeRate - dividendYield - 0.5m * volatility * volatility) * (double)timeToExpiry;
                var diffusion = (double)volatility * Math.Sqrt((double)timeToExpiry) * z;
                var finalPrice = (decimal)((double)spotPrice * Math.Exp(drift + diffusion));

                // Calculate payoff
                if (optionType == OptionType.Call)
                {
                    payoffs[i] = Math.Max(finalPrice - strike, 0);
                }
                else
                {
                    payoffs[i] = Math.Max(strike - finalPrice, 0);
                }
            }

            var averagePayoff = payoffs.Average();
            var discountFactor = (decimal)Math.Exp(-(double)riskFreeRate * (double)timeToExpiry);
            var theoreticalPrice = averagePayoff * discountFactor;

            // Calculate standard error
            var variance = payoffs.Select(p => (p - averagePayoff) * (p - averagePayoff)).Average();
            var standardError = (decimal)(Math.Sqrt((double)variance) / Math.Sqrt(simulations)) * discountFactor;

            return new OptionPricingResult
            {
                Symbol = symbol,
                OptionType = optionType,
                Strike = strike,
                SpotPrice = spotPrice,
                TimeToExpiry = timeToExpiry,
                RiskFreeRate = riskFreeRate,
                Volatility = volatility,
                DividendYield = dividendYield,
                TheoreticalPrice = RoundToRegulatory(theoreticalPrice),
                StandardError = RoundToRegulatory(standardError),
                MonteCarloSimulations = simulations,
                ConfidenceInterval95Low = RoundToRegulatory(theoreticalPrice - 1.96m * standardError),
                ConfidenceInterval95High = RoundToRegulatory(theoreticalPrice + 1.96m * standardError),
                CalculationType = "MonteCarlo_CPU",
                UsedGpuAcceleration = false,
                ServiceName = ServiceName
            };
        });
    }

    private async Task<OptionPricingResult> CalculateBinomialTreeCpuAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal volatility,
        int steps,
        decimal dividendYield)
    {
        return await Task.Run(() =>
        {
            var dt = timeToExpiry / steps;
            var u = (decimal)Math.Exp((double)volatility * Math.Sqrt((double)dt));
            var d = 1 / u;
            var p = (decimal)((Math.Exp((double)(riskFreeRate - dividendYield) * (double)dt) - (double)d) / ((double)u - (double)d));

            // Build the stock price tree
            var stockPrices = new decimal[steps + 1, steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    stockPrices[i, j] = spotPrice * (decimal)Math.Pow((double)u, j) * (decimal)Math.Pow((double)d, i - j);
                }
            }

            // Calculate option values at expiration
            var optionValues = new decimal[steps + 1, steps + 1];
            for (int j = 0; j <= steps; j++)
            {
                if (optionType == OptionType.Call)
                {
                    optionValues[steps, j] = Math.Max(stockPrices[steps, j] - strike, 0);
                }
                else
                {
                    optionValues[steps, j] = Math.Max(strike - stockPrices[steps, j], 0);
                }
            }

            // Work backwards through the tree
            var discountFactor = (decimal)Math.Exp(-(double)riskFreeRate * (double)dt);
            for (int i = steps - 1; i >= 0; i--)
            {
                for (int j = 0; j <= i; j++)
                {
                    optionValues[i, j] = discountFactor * (p * optionValues[i + 1, j + 1] + (1 - p) * optionValues[i + 1, j]);
                }
            }

            var theoreticalPrice = optionValues[0, 0];

            // Calculate delta (approximate)
            var delta = steps > 0 ? (optionValues[1, 1] - optionValues[1, 0]) / (stockPrices[1, 1] - stockPrices[1, 0]) : 0;

            return new OptionPricingResult
            {
                Symbol = symbol,
                OptionType = optionType,
                Strike = strike,
                SpotPrice = spotPrice,
                TimeToExpiry = timeToExpiry,
                RiskFreeRate = riskFreeRate,
                Volatility = volatility,
                DividendYield = dividendYield,
                TheoreticalPrice = RoundToRegulatory(theoreticalPrice),
                Delta = RoundToRegulatory(delta, 6),
                CalculationType = "BinomialTree_CPU",
                UsedGpuAcceleration = false,
                ServiceName = ServiceName
            };
        });
    }

    private async Task<decimal> CalculateImpliedVolatilityCpuAsync(
        string symbol,
        OptionType optionType,
        decimal strike,
        decimal spotPrice,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal marketPrice,
        decimal dividendYield)
    {
        return await Task.Run(() =>
        {
            // Newton-Raphson method for implied volatility
            var tolerance = 0.0001m;
            var maxIterations = 100;
            var vol = 0.20m; // Initial guess

            for (int i = 0; i < maxIterations; i++)
            {
                var blackScholesResult = CalculateBlackScholesCpuAsync(symbol, optionType, strike, spotPrice, timeToExpiry, riskFreeRate, vol, dividendYield).Result;
                var priceError = blackScholesResult.TheoreticalPrice - marketPrice;

                if (Math.Abs(priceError) < tolerance)
                {
                    return RoundToRegulatory(vol, 6);
                }

                // Vega is the derivative of price with respect to volatility
                var vega = blackScholesResult.Vega;
                if (vega == 0) break;

                vol = vol - priceError / vega;

                // Ensure volatility stays within reasonable bounds
                vol = Math.Max(0.001m, Math.Min(vol, 5.0m));
            }

            return RoundToRegulatory(vol, 6);
        });
    }

    #endregion

    #region IFinancialCalculationService Implementation

    public async Task<TradingResult<T>> CalculateAsync<T>(string calculationType, object parameters, CancellationToken cancellationToken = default)
        where T : FinancialCalculationResult
    {
        return await TrackOperationAsync($"Calculate_{calculationType}", async () =>
        {
            return calculationType.ToUpperInvariant() switch
            {
                "BLACK_SCHOLES" when parameters is OptionPricingParameters bsParams =>
                    (TradingResult<T>)(object)await CalculateBlackScholesAsync(bsParams.Symbol, bsParams.OptionType, bsParams.Strike, bsParams.SpotPrice, bsParams.TimeToExpiry, bsParams.RiskFreeRate, bsParams.Volatility, bsParams.DividendYield, cancellationToken),

                "MONTE_CARLO" when parameters is MonteCarloOptionParameters mcParams =>
                    (TradingResult<T>)(object)await CalculateMonteCarloAsync(mcParams.Symbol, mcParams.OptionType, mcParams.Strike, mcParams.SpotPrice, mcParams.TimeToExpiry, mcParams.RiskFreeRate, mcParams.Volatility, mcParams.Simulations, mcParams.DividendYield, cancellationToken),

                _ => TradingResult<T>.Failure(new NotSupportedException($"Calculation type '{calculationType}' not supported"))
            };
        });
    }

    public async Task<TradingResult<bool>> ValidateInputAsync(string calculationType, object parameters, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                switch (calculationType.ToUpperInvariant())
                {
                    case "BLACK_SCHOLES":
                        if (parameters is OptionPricingParameters bsParams)
                        {
                            ValidateOptionInputs(bsParams.Symbol, bsParams.Strike, bsParams.SpotPrice, bsParams.TimeToExpiry, bsParams.RiskFreeRate, bsParams.Volatility);
                            return TradingResult<bool>.Success(true);
                        }
                        break;
                }

                return TradingResult<bool>.Failure(new ArgumentException("Invalid parameters for calculation type"));
            }
            catch (Exception ex)
            {
                return TradingResult<bool>.Failure(ex);
            }
        });
    }

    public async Task<TradingResult<List<CalculationAuditEntry>>> GetAuditTrailAsync(string calculationId, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var entries = AuditTrail.Values
                .Where(e => e.Id == calculationId || e.OperationName.Contains(calculationId))
                .OrderBy(e => e.StartedAt)
                .ToList();

            return TradingResult<List<CalculationAuditEntry>>.Success(entries);
        });
    }

    public async Task<TradingResult<Dictionary<string, CalculationPerformanceStats>>> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var metrics = _performanceStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return TradingResult<Dictionary<string, CalculationPerformanceStats>>.Success(metrics);
        });
    }

    #endregion

    #region Helper Methods

    private void ValidateOptionInputs(string symbol, decimal strike, decimal spotPrice, decimal timeToExpiry, decimal riskFreeRate, decimal volatility)
    {
        if (string.IsNullOrEmpty(symbol))
            throw new ArgumentException("Symbol cannot be null or empty");

        if (strike <= 0)
            throw new ArgumentException("Strike price must be positive");

        if (spotPrice <= 0)
            throw new ArgumentException("Spot price must be positive");

        if (timeToExpiry <= 0)
            throw new ArgumentException("Time to expiry must be positive");

        if (riskFreeRate < 0)
            throw new ArgumentException("Risk-free rate cannot be negative");

        if (volatility <= 0)
            throw new ArgumentException("Volatility must be positive");
    }

    private long[,] GenerateRandomNumbers(int numOptions, int numSimulations)
    {
        var randomNumbers = new long[numOptions, numSimulations];
        
        for (int i = 0; i < numOptions; i++)
        {
            for (int j = 0; j < numSimulations; j++)
            {
                // Generate standard normal random number using Box-Muller transform
                var u1 = _random.NextDouble();
                var u2 = _random.NextDouble();
                var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                randomNumbers[i, j] = ToScaledInteger((decimal)z, 6);
            }
        }
        
        return randomNumbers;
    }

    private static double CumulativeNormalDistribution(double x)
    {
        return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
    }

    private static double NormalPDF(double x)
    {
        return Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);
    }

    private static double Erf(double x)
    {
        // Abramowitz and Stegun approximation
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var sign = x >= 0 ? 1 : -1;
        x = Math.Abs(x);

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }

    private string GenerateBlackScholesCacheKey(string symbol, OptionType optionType, decimal strike, decimal spotPrice, decimal timeToExpiry, decimal riskFreeRate, decimal volatility, decimal dividendYield)
    {
        return $"BS_{symbol}_{optionType}_{strike}_{spotPrice}_{timeToExpiry}_{riskFreeRate}_{volatility}_{dividendYield}";
    }

    private string GenerateMonteCarloCacheKey(string symbol, OptionType optionType, decimal strike, decimal spotPrice, decimal timeToExpiry, decimal riskFreeRate, decimal volatility, int simulations, decimal dividendYield)
    {
        return $"MC_{symbol}_{optionType}_{strike}_{spotPrice}_{timeToExpiry}_{riskFreeRate}_{volatility}_{simulations}_{dividendYield}";
    }

    private string GenerateBinomialCacheKey(string symbol, OptionType optionType, decimal strike, decimal spotPrice, decimal timeToExpiry, decimal riskFreeRate, decimal volatility, int steps, decimal dividendYield)
    {
        return $"BT_{symbol}_{optionType}_{strike}_{spotPrice}_{timeToExpiry}_{riskFreeRate}_{volatility}_{steps}_{dividendYield}";
    }

    #endregion

    #region Cache Management

    private class OptionPricingCache
    {
        public OptionPricingResult? BlackScholesResult { get; set; }
        public OptionPricingResult? MonteCarloResult { get; set; }
        public OptionPricingResult? BinomialResult { get; set; }
        public DateTime LastUpdated { get; set; }
        public TimeSpan Expiry { get; set; } = TimeSpan.FromMinutes(10);

        public bool IsExpired => DateTime.UtcNow - LastUpdated > Expiry;
    }

    #endregion
}

#region Parameter Classes

public class OptionPricingParameters
{
    public string Symbol { get; set; } = string.Empty;
    public OptionType OptionType { get; set; }
    public decimal Strike { get; set; }
    public decimal SpotPrice { get; set; }
    public decimal TimeToExpiry { get; set; }
    public decimal RiskFreeRate { get; set; }
    public decimal Volatility { get; set; }
    public decimal DividendYield { get; set; }
}

public class MonteCarloOptionParameters : OptionPricingParameters
{
    public int Simulations { get; set; }
}

#endregion