// TradingPlatform.FinancialCalculations.Kernels.AdvancedFinancialKernels
// Advanced GPU kernels for financial calculations with decimal precision

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Algorithms;
using System.Runtime.CompilerServices;

namespace TradingPlatform.FinancialCalculations.Kernels;

/// <summary>
/// Advanced GPU kernels for financial calculations
/// All calculations use scaled integers to maintain decimal precision
/// Features include portfolio analytics, risk metrics, and option pricing
/// </summary>
public static class AdvancedFinancialKernels
{
    // Enhanced scaling factors for different precision requirements
    public const long PRICE_SCALE = 10000L;        // 4 decimal places for prices
    public const long RATE_SCALE = 100000000L;     // 8 decimal places for rates
    public const long RATIO_SCALE = 1000000L;      // 6 decimal places for ratios
    public const long CURRENCY_SCALE = 100L;       // 2 decimal places for currencies
    
    // Mathematical constants (scaled)
    public const long PI_SCALED = 31415926L;       // PI with 7 decimal places
    public const long E_SCALED = 27182818L;        // E with 7 decimal places
    public const long SQRT_2PI_SCALED = 25066282L; // sqrt(2*PI) with 7 decimal places

    #region Portfolio Analytics Kernels

    /// <summary>
    /// Calculate portfolio value and PnL for multiple positions in parallel
    /// </summary>
    public static void CalculatePortfolioMetricsKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> quantities,        // Position quantities (scaled)
        ArrayView1D<long, Stride1D.Dense> averagePrices,     // Average cost prices (scaled)
        ArrayView1D<long, Stride1D.Dense> currentPrices,     // Current market prices (scaled)
        ArrayView1D<long, Stride1D.Dense> marketValues,      // Output: market values
        ArrayView1D<long, Stride1D.Dense> unrealizedPnL,     // Output: unrealized P&L
        ArrayView1D<long, Stride1D.Dense> costBasis,         // Output: cost basis
        ArrayView1D<long, Stride1D.Dense> positionWeights,   // Output: position weights
        long totalPortfolioValue)                             // Total portfolio value for weight calculation
    {
        var position = index.X;
        
        // Calculate market value
        long marketValue = (quantities[position] * currentPrices[position]) / PRICE_SCALE;
        marketValues[position] = marketValue;
        
        // Calculate cost basis
        long cost = (quantities[position] * averagePrices[position]) / PRICE_SCALE;
        costBasis[position] = cost;
        
        // Calculate unrealized P&L
        unrealizedPnL[position] = marketValue - cost;
        
        // Calculate position weight (as percentage of total portfolio)
        if (totalPortfolioValue > 0)
        {
            positionWeights[position] = (marketValue * 100L * RATIO_SCALE) / totalPortfolioValue;
        }
        else
        {
            positionWeights[position] = 0;
        }
    }

    /// <summary>
    /// Calculate portfolio risk metrics including volatility and correlation
    /// </summary>
    public static void CalculatePortfolioRiskKernel(
        Index1D index,
        ArrayView2D<long, Stride2D.DenseX> returns,          // Historical returns matrix (assets x time)
        ArrayView1D<long, Stride1D.Dense> weights,           // Portfolio weights
        ArrayView1D<long, Stride1D.Dense> portfolioReturns,  // Output: portfolio returns
        ArrayView1D<long, Stride1D.Dense> volatilities,      // Output: asset volatilities
        ArrayView2D<long, Stride2D.DenseX> correlationMatrix, // Output: correlation matrix
        int numAssets,
        int numReturns)
    {
        var asset = index.X;
        
        if (asset >= numAssets) return;
        
        // Calculate individual asset volatility
        long sumReturns = 0;
        long sumSquaredReturns = 0;
        
        for (int t = 0; t < numReturns; t++)
        {
            long returnValue = returns[asset, t];
            sumReturns += returnValue;
            sumSquaredReturns += (returnValue * returnValue) / RATIO_SCALE;
        }
        
        long meanReturn = sumReturns / numReturns;
        long variance = (sumSquaredReturns / numReturns) - (meanReturn * meanReturn) / RATIO_SCALE;
        volatilities[asset] = InlineMath.ISqrt(variance * RATIO_SCALE);
        
        // Calculate portfolio returns for this time period
        if (asset == 0) // Only calculate once
        {
            for (int t = 0; t < numReturns; t++)
            {
                long portfolioReturn = 0;
                for (int i = 0; i < numAssets; i++)
                {
                    portfolioReturn += (returns[i, t] * weights[i]) / RATIO_SCALE;
                }
                portfolioReturns[t] = portfolioReturn;
            }
        }
        
        // Calculate correlation with other assets
        for (int otherAsset = 0; otherAsset < numAssets; otherAsset++)
        {
            if (asset <= otherAsset) // Calculate upper triangle only
            {
                long covariance = 0;
                long asset1Mean = 0;
                long asset2Mean = 0;
                
                // Calculate means
                for (int t = 0; t < numReturns; t++)
                {
                    asset1Mean += returns[asset, t];
                    asset2Mean += returns[otherAsset, t];
                }
                asset1Mean /= numReturns;
                asset2Mean /= numReturns;
                
                // Calculate covariance
                for (int t = 0; t < numReturns; t++)
                {
                    long dev1 = returns[asset, t] - asset1Mean;
                    long dev2 = returns[otherAsset, t] - asset2Mean;
                    covariance += (dev1 * dev2) / RATIO_SCALE;
                }
                covariance /= numReturns;
                
                // Calculate correlation
                long vol1 = volatilities[asset];
                long vol2 = volatilities[otherAsset];
                if (vol1 > 0 && vol2 > 0)
                {
                    long correlation = (covariance * RATIO_SCALE) / (vol1 * vol2);
                    correlationMatrix[asset, otherAsset] = correlation;
                    correlationMatrix[otherAsset, asset] = correlation; // Symmetric
                }
                else
                {
                    correlationMatrix[asset, otherAsset] = 0;
                    correlationMatrix[otherAsset, asset] = 0;
                }
            }
        }
    }

    #endregion

    #region Value at Risk (VaR) Kernels

    /// <summary>
    /// Calculate Value at Risk using Monte Carlo simulation
    /// </summary>
    public static void CalculateVaRMonteCarloKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> portfolioReturns,  // Historical portfolio returns
        ArrayView1D<long, Stride1D.Dense> randomNumbers,     // Random numbers for simulation
        ArrayView1D<long, Stride1D.Dense> simulatedReturns,  // Output: simulated returns
        ArrayView1D<long, Stride1D.Dense> sortedReturns,     // Output: sorted returns for percentile
        long portfolioValue,                                  // Current portfolio value
        int numSimulations,
        int historyLength)
    {
        var simulation = index.X;
        
        if (simulation >= numSimulations) return;
        
        // Calculate mean and standard deviation of historical returns
        long meanReturn = 0;
        long variance = 0;
        
        for (int i = 0; i < historyLength; i++)
        {
            meanReturn += portfolioReturns[i];
        }
        meanReturn /= historyLength;
        
        for (int i = 0; i < historyLength; i++)
        {
            long deviation = portfolioReturns[i] - meanReturn;
            variance += (deviation * deviation) / RATIO_SCALE;
        }
        variance /= historyLength;
        long stdDev = InlineMath.ISqrt(variance * RATIO_SCALE);
        
        // Generate simulated return using Box-Muller transform approximation
        long randomValue = randomNumbers[simulation];
        long simulatedReturn = meanReturn + (stdDev * randomValue) / RATIO_SCALE;
        
        simulatedReturns[simulation] = simulatedReturn;
        
        // Convert to portfolio value change
        long portfolioChange = (portfolioValue * simulatedReturn) / RATIO_SCALE;
        sortedReturns[simulation] = portfolioChange;
    }

    /// <summary>
    /// Calculate Expected Shortfall (Conditional VaR)
    /// </summary>
    public static void CalculateExpectedShortfallKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> sortedLosses,      // Sorted losses (ascending)
        ArrayView1D<long, Stride1D.Dense> expectedShortfall, // Output: ES values
        int numSimulations,
        int confidenceLevel)                                  // 95, 99, etc.
    {
        var confidenceIndex = index.X;
        
        // Calculate the index corresponding to the confidence level
        int varIndex = (numSimulations * (100 - confidenceLevel)) / 100;
        
        // Calculate expected shortfall as average of losses beyond VaR
        long sum = 0;
        int count = 0;
        
        for (int i = 0; i < varIndex; i++)
        {
            sum += sortedLosses[i];
            count++;
        }
        
        expectedShortfall[confidenceIndex] = count > 0 ? sum / count : 0;
    }

    #endregion

    #region Option Pricing Kernels

    /// <summary>
    /// Black-Scholes option pricing kernel for multiple options
    /// </summary>
    public static void BlackScholesKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> spotPrices,        // Current stock prices
        ArrayView1D<long, Stride1D.Dense> strikes,           // Strike prices
        ArrayView1D<long, Stride1D.Dense> timeToExpiry,      // Time to expiration (scaled)
        ArrayView1D<long, Stride1D.Dense> riskFreeRates,     // Risk-free rates (scaled)
        ArrayView1D<long, Stride1D.Dense> volatilities,      // Volatilities (scaled)
        ArrayView1D<byte, Stride1D.Dense> optionTypes,       // 0 = Call, 1 = Put
        ArrayView1D<long, Stride1D.Dense> optionPrices,      // Output: option prices
        ArrayView1D<long, Stride1D.Dense> delta,             // Output: delta
        ArrayView1D<long, Stride1D.Dense> gamma,             // Output: gamma
        ArrayView1D<long, Stride1D.Dense> theta,             // Output: theta
        ArrayView1D<long, Stride1D.Dense> vega,              // Output: vega
        ArrayView1D<long, Stride1D.Dense> rho)               // Output: rho
    {
        var optionIndex = index.X;
        
        long S = spotPrices[optionIndex];
        long K = strikes[optionIndex];
        long T = timeToExpiry[optionIndex];
        long r = riskFreeRates[optionIndex];
        long v = volatilities[optionIndex];
        bool isCall = optionTypes[optionIndex] == 0;
        
        // Calculate d1 and d2
        long vT = (v * InlineMath.ISqrt(T * RATE_SCALE)) / RATE_SCALE;
        long d1Numerator = InlineMath.ILn((S * RATE_SCALE) / K) + ((r + (v * v) / (2 * RATE_SCALE)) * T) / RATE_SCALE;
        long d1 = d1Numerator / vT;
        long d2 = d1 - vT;
        
        // Calculate cumulative normal distribution values
        long Nd1 = CumulativeNormalDistribution(d1);
        long Nd2 = CumulativeNormalDistribution(d2);
        long NegNd1 = RATE_SCALE - Nd1;
        long NegNd2 = RATE_SCALE - Nd2;
        
        // Calculate option price
        long discountFactor = InlineMath.IExp((-r * T) / RATE_SCALE);
        
        if (isCall)
        {
            optionPrices[optionIndex] = (S * Nd1) / RATE_SCALE - (K * discountFactor * Nd2) / (RATE_SCALE * RATE_SCALE);
            delta[optionIndex] = Nd1;
            rho[optionIndex] = (K * T * discountFactor * Nd2) / (RATE_SCALE * RATE_SCALE);
        }
        else
        {
            optionPrices[optionIndex] = (K * discountFactor * NegNd2) / (RATE_SCALE * RATE_SCALE) - (S * NegNd1) / RATE_SCALE;
            delta[optionIndex] = NegNd1 - RATE_SCALE;
            rho[optionIndex] = -(K * T * discountFactor * NegNd2) / (RATE_SCALE * RATE_SCALE);
        }
        
        // Calculate Greeks
        long pdf = NormalPDF(d1);
        
        // Gamma (same for calls and puts)
        gamma[optionIndex] = (pdf * RATE_SCALE) / (S * vT);
        
        // Theta
        long theta1 = -(S * pdf * v) / (2 * InlineMath.ISqrt(T * RATE_SCALE));
        long theta2 = (r * K * discountFactor * (isCall ? Nd2 : NegNd2)) / RATE_SCALE;
        theta[optionIndex] = (theta1 - theta2) / 365; // Convert to daily theta
        
        // Vega (same for calls and puts)
        vega[optionIndex] = (S * pdf * InlineMath.ISqrt(T * RATE_SCALE)) / (RATE_SCALE * 100); // Convert to 1% volatility change
    }

    /// <summary>
    /// Monte Carlo option pricing kernel with variance reduction
    /// </summary>
    public static void MonteCarloOptionKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> spotPrices,
        ArrayView1D<long, Stride1D.Dense> strikes,
        ArrayView1D<long, Stride1D.Dense> timeToExpiry,
        ArrayView1D<long, Stride1D.Dense> riskFreeRates,
        ArrayView1D<long, Stride1D.Dense> volatilities,
        ArrayView1D<byte, Stride1D.Dense> optionTypes,
        ArrayView2D<long, Stride2D.DenseX> randomNumbers,    // Pre-generated random numbers
        ArrayView2D<long, Stride2D.DenseX> simulatedPrices,  // Output: simulated final prices
        ArrayView2D<long, Stride2D.DenseX> payoffs,          // Output: payoffs
        ArrayView1D<long, Stride1D.Dense> optionPrices,      // Output: option prices
        ArrayView1D<long, Stride1D.Dense> standardErrors,    // Output: standard errors
        int numSimulations)
    {
        var optionIndex = index.X;
        
        long S = spotPrices[optionIndex];
        long K = strikes[optionIndex];
        long T = timeToExpiry[optionIndex];
        long r = riskFreeRates[optionIndex];
        long v = volatilities[optionIndex];
        bool isCall = optionTypes[optionIndex] == 0;
        
        // Monte Carlo simulation
        long payoffSum = 0;
        long payoffSumSquared = 0;
        
        for (int sim = 0; sim < numSimulations; sim++)
        {
            // Generate final stock price using geometric Brownian motion
            long z = randomNumbers[optionIndex, sim];
            long drift = (r - (v * v) / (2 * RATE_SCALE)) * T / RATE_SCALE;
            long diffusion = (v * InlineMath.ISqrt(T * RATE_SCALE) * z) / RATE_SCALE;
            long logST = InlineMath.ILn(S) + drift + diffusion;
            long ST = InlineMath.IExp(logST);
            
            simulatedPrices[optionIndex, sim] = ST;
            
            // Calculate payoff
            long payoff = 0;
            if (isCall)
            {
                payoff = ST > K ? ST - K : 0;
            }
            else
            {
                payoff = K > ST ? K - ST : 0;
            }
            
            payoffs[optionIndex, sim] = payoff;
            payoffSum += payoff;
            payoffSumSquared += (payoff * payoff) / PRICE_SCALE;
        }
        
        // Calculate option price (discounted expected payoff)
        long avgPayoff = payoffSum / numSimulations;
        long discountFactor = InlineMath.IExp((-r * T) / RATE_SCALE);
        optionPrices[optionIndex] = (avgPayoff * discountFactor) / RATE_SCALE;
        
        // Calculate standard error
        long variance = (payoffSumSquared / numSimulations) - (avgPayoff * avgPayoff) / PRICE_SCALE;
        long stdError = InlineMath.ISqrt(variance * PRICE_SCALE) / InlineMath.ISqrt(numSimulations * PRICE_SCALE);
        standardErrors[optionIndex] = (stdError * discountFactor) / RATE_SCALE;
    }

    #endregion

    #region Fixed Income Kernels

    /// <summary>
    /// Bond pricing and duration calculation kernel
    /// </summary>
    public static void BondPricingKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> faceValues,        // Face values
        ArrayView1D<long, Stride1D.Dense> couponRates,       // Coupon rates (scaled)
        ArrayView1D<long, Stride1D.Dense> yieldsToMaturity,  // Yields to maturity (scaled)
        ArrayView1D<int, Stride1D.Dense> periodsToMaturity,  // Periods to maturity
        ArrayView1D<int, Stride1D.Dense> couponFrequencies,  // Coupon frequencies per year
        ArrayView1D<long, Stride1D.Dense> bondPrices,        // Output: bond prices
        ArrayView1D<long, Stride1D.Dense> durations,         // Output: modified durations
        ArrayView1D<long, Stride1D.Dense> convexities,       // Output: convexities
        ArrayView1D<long, Stride1D.Dense> accruedInterests)  // Output: accrued interests
    {
        var bondIndex = index.X;
        
        long faceValue = faceValues[bondIndex];
        long couponRate = couponRates[bondIndex];
        long ytm = yieldsToMaturity[bondIndex];
        int periods = periodsToMaturity[bondIndex];
        int frequency = couponFrequencies[bondIndex];
        
        // Calculate periodic coupon payment
        long couponPayment = (faceValue * couponRate) / (frequency * RATE_SCALE);
        long periodicYield = ytm / frequency;
        
        // Calculate bond price (present value of cash flows)
        long bondPrice = 0;
        long durationNumerator = 0;
        long convexityNumerator = 0;
        
        for (int period = 1; period <= periods; period++)
        {
            // Discount factor for this period
            long discountFactor = InlineMath.IPow(RATE_SCALE + periodicYield, period);
            
            // Cash flow for this period
            long cashFlow = period == periods ? faceValue + couponPayment : couponPayment;
            
            // Present value of cash flow
            long presentValue = (cashFlow * RATE_SCALE) / discountFactor;
            bondPrice += presentValue;
            
            // Duration calculation (weighted average time to receive cash flows)
            durationNumerator += (presentValue * period) / RATE_SCALE;
            
            // Convexity calculation
            convexityNumerator += (presentValue * period * (period + 1)) / RATE_SCALE;
        }
        
        bondPrices[bondIndex] = bondPrice;
        
        // Calculate modified duration
        if (bondPrice > 0)
        {
            long macaulayDuration = (durationNumerator * frequency * RATE_SCALE) / bondPrice;
            durations[bondIndex] = (macaulayDuration * RATE_SCALE) / (RATE_SCALE + periodicYield);
        }
        else
        {
            durations[bondIndex] = 0;
        }
        
        // Calculate convexity
        if (bondPrice > 0)
        {
            long convexity = (convexityNumerator * frequency * frequency * RATE_SCALE) / bondPrice;
            convexities[bondIndex] = convexity / ((RATE_SCALE + periodicYield) * (RATE_SCALE + periodicYield));
        }
        else
        {
            convexities[bondIndex] = 0;
        }
        
        // Calculate accrued interest (simplified - assuming mid-period)
        accruedInterests[bondIndex] = couponPayment / 2;
    }

    #endregion

    #region Technical Analysis Kernels

    /// <summary>
    /// Advanced technical indicators kernel (RSI, MACD, Stochastic)
    /// </summary>
    public static void AdvancedTechnicalIndicatorsKernel(
        Index1D index,
        ArrayView2D<long, Stride2D.DenseX> prices,           // Price data (securities x time)
        ArrayView2D<long, Stride2D.DenseX> highs,            // High prices
        ArrayView2D<long, Stride2D.DenseX> lows,             // Low prices
        ArrayView2D<long, Stride2D.DenseX> volumes,          // Volume data
        ArrayView2D<long, Stride2D.DenseX> rsi,              // Output: RSI
        ArrayView2D<long, Stride2D.DenseX> macd,             // Output: MACD line
        ArrayView2D<long, Stride2D.DenseX> macdSignal,       // Output: MACD signal line
        ArrayView2D<long, Stride2D.DenseX> stochasticK,      // Output: Stochastic %K
        ArrayView2D<long, Stride2D.DenseX> stochasticD,      // Output: Stochastic %D
        ArrayView2D<long, Stride2D.DenseX> obv,              // Output: On-Balance Volume
        int rsiPeriod,
        int macdFastPeriod,
        int macdSlowPeriod,
        int macdSignalPeriod,
        int stochasticKPeriod,
        int stochasticDPeriod)
    {
        var security = index.X;
        var numPrices = prices.IntExtent.Y;
        
        // Calculate RSI
        CalculateRSI(security, prices, rsi, rsiPeriod, numPrices);
        
        // Calculate MACD
        CalculateMACD(security, prices, macd, macdSignal, macdFastPeriod, macdSlowPeriod, macdSignalPeriod, numPrices);
        
        // Calculate Stochastic Oscillator
        CalculateStochastic(security, prices, highs, lows, stochasticK, stochasticD, stochasticKPeriod, stochasticDPeriod, numPrices);
        
        // Calculate On-Balance Volume
        CalculateOBV(security, prices, volumes, obv, numPrices);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Calculate RSI for a single security
    /// </summary>
    private static void CalculateRSI(
        int security,
        ArrayView2D<long, Stride2D.DenseX> prices,
        ArrayView2D<long, Stride2D.DenseX> rsi,
        int period,
        int numPrices)
    {
        if (numPrices <= period) return;
        
        long avgGain = 0;
        long avgLoss = 0;
        
        // Calculate initial average gain/loss
        for (int i = 1; i <= period; i++)
        {
            long change = prices[security, i] - prices[security, i - 1];
            if (change > 0)
                avgGain += change;
            else
                avgLoss -= change;
        }
        
        avgGain /= period;
        avgLoss /= period;
        
        // Calculate RSI for each subsequent period
        for (int i = period + 1; i < numPrices; i++)
        {
            long change = prices[security, i] - prices[security, i - 1];
            long gain = change > 0 ? change : 0;
            long loss = change < 0 ? -change : 0;
            
            // Smooth the averages
            avgGain = ((avgGain * (period - 1)) + gain) / period;
            avgLoss = ((avgLoss * (period - 1)) + loss) / period;
            
            // Calculate RSI
            if (avgLoss == 0)
            {
                rsi[security, i] = 100 * RATIO_SCALE;
            }
            else
            {
                long rs = (avgGain * RATIO_SCALE) / avgLoss;
                rsi[security, i] = 100 * RATIO_SCALE - (100 * RATIO_SCALE * RATIO_SCALE) / (RATIO_SCALE + rs);
            }
        }
    }

    /// <summary>
    /// Calculate MACD for a single security
    /// </summary>
    private static void CalculateMACD(
        int security,
        ArrayView2D<long, Stride2D.DenseX> prices,
        ArrayView2D<long, Stride2D.DenseX> macd,
        ArrayView2D<long, Stride2D.DenseX> macdSignal,
        int fastPeriod,
        int slowPeriod,
        int signalPeriod,
        int numPrices)
    {
        if (numPrices <= slowPeriod) return;
        
        // Calculate EMAs
        long fastEMA = 0;
        long slowEMA = 0;
        long fastMultiplier = (2 * RATIO_SCALE) / (fastPeriod + 1);
        long slowMultiplier = (2 * RATIO_SCALE) / (slowPeriod + 1);
        long signalMultiplier = (2 * RATIO_SCALE) / (signalPeriod + 1);
        
        // Initialize EMAs with SMAs
        for (int i = 0; i < fastPeriod; i++)
        {
            fastEMA += prices[security, i];
        }
        fastEMA /= fastPeriod;
        
        for (int i = 0; i < slowPeriod; i++)
        {
            slowEMA += prices[security, i];
        }
        slowEMA /= slowPeriod;
        
        // Calculate MACD line
        long signalEMA = 0;
        bool signalInitialized = false;
        
        for (int i = slowPeriod; i < numPrices; i++)
        {
            // Update EMAs
            fastEMA = ((prices[security, i] - fastEMA) * fastMultiplier) / RATIO_SCALE + fastEMA;
            slowEMA = ((prices[security, i] - slowEMA) * slowMultiplier) / RATIO_SCALE + slowEMA;
            
            // Calculate MACD line
            long macdValue = fastEMA - slowEMA;
            macd[security, i] = macdValue;
            
            // Calculate signal line
            if (!signalInitialized)
            {
                if (i >= slowPeriod + signalPeriod - 1)
                {
                    // Initialize signal EMA
                    signalEMA = 0;
                    for (int j = 0; j < signalPeriod; j++)
                    {
                        signalEMA += macd[security, slowPeriod + j];
                    }
                    signalEMA /= signalPeriod;
                    signalInitialized = true;
                }
            }
            else
            {
                signalEMA = ((macdValue - signalEMA) * signalMultiplier) / RATIO_SCALE + signalEMA;
            }
            
            macdSignal[security, i] = signalEMA;
        }
    }

    /// <summary>
    /// Calculate Stochastic Oscillator for a single security
    /// </summary>
    private static void CalculateStochastic(
        int security,
        ArrayView2D<long, Stride2D.DenseX> prices,
        ArrayView2D<long, Stride2D.DenseX> highs,
        ArrayView2D<long, Stride2D.DenseX> lows,
        ArrayView2D<long, Stride2D.DenseX> stochasticK,
        ArrayView2D<long, Stride2D.DenseX> stochasticD,
        int kPeriod,
        int dPeriod,
        int numPrices)
    {
        if (numPrices <= kPeriod) return;
        
        for (int i = kPeriod - 1; i < numPrices; i++)
        {
            // Find highest high and lowest low in the period
            long highestHigh = highs[security, i];
            long lowestLow = lows[security, i];
            
            for (int j = 1; j < kPeriod; j++)
            {
                if (highs[security, i - j] > highestHigh)
                    highestHigh = highs[security, i - j];
                if (lows[security, i - j] < lowestLow)
                    lowestLow = lows[security, i - j];
            }
            
            // Calculate %K
            if (highestHigh > lowestLow)
            {
                stochasticK[security, i] = ((prices[security, i] - lowestLow) * 100 * RATIO_SCALE) / (highestHigh - lowestLow);
            }
            else
            {
                stochasticK[security, i] = 50 * RATIO_SCALE; // Neutral
            }
        }
        
        // Calculate %D (moving average of %K)
        for (int i = kPeriod + dPeriod - 2; i < numPrices; i++)
        {
            long sum = 0;
            for (int j = 0; j < dPeriod; j++)
            {
                sum += stochasticK[security, i - j];
            }
            stochasticD[security, i] = sum / dPeriod;
        }
    }

    /// <summary>
    /// Calculate On-Balance Volume for a single security
    /// </summary>
    private static void CalculateOBV(
        int security,
        ArrayView2D<long, Stride2D.DenseX> prices,
        ArrayView2D<long, Stride2D.DenseX> volumes,
        ArrayView2D<long, Stride2D.DenseX> obv,
        int numPrices)
    {
        if (numPrices < 2) return;
        
        obv[security, 0] = volumes[security, 0];
        
        for (int i = 1; i < numPrices; i++)
        {
            if (prices[security, i] > prices[security, i - 1])
            {
                obv[security, i] = obv[security, i - 1] + volumes[security, i];
            }
            else if (prices[security, i] < prices[security, i - 1])
            {
                obv[security, i] = obv[security, i - 1] - volumes[security, i];
            }
            else
            {
                obv[security, i] = obv[security, i - 1];
            }
        }
    }

    /// <summary>
    /// Cumulative normal distribution approximation
    /// </summary>
    private static long CumulativeNormalDistribution(long x)
    {
        // Abramowitz and Stegun approximation
        if (x < 0)
        {
            return RATE_SCALE - CumulativeNormalDistribution(-x);
        }
        
        long t = RATE_SCALE / (RATE_SCALE + (2316419L * x) / 10000000L);
        long poly = ((((1330274L * t) / 10000000L - 1821256L) * t / 10000000L + 1781478L) * t / 10000000L - 356538L) * t / 10000000L + 319382L;
        long result = RATE_SCALE - (NormalPDF(x) * poly * t) / RATE_SCALE;
        
        return result;
    }

    /// <summary>
    /// Normal probability density function
    /// </summary>
    private static long NormalPDF(long x)
    {
        // PDF = (1/sqrt(2*pi)) * exp(-x^2/2)
        long exponent = -(x * x) / (2 * RATE_SCALE);
        long expValue = InlineMath.IExp(exponent);
        return (expValue * RATE_SCALE) / SQRT_2PI_SCALED;
    }

    #endregion
}

/// <summary>
/// Enhanced inline math utilities for financial calculations
/// </summary>
public static class InlineMath
{
    /// <summary>
    /// Integer square root using Newton's method
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ISqrt(long n)
    {
        if (n < 0) return 0;
        if (n == 0) return 0;
        if (n == 1) return 1;
        
        long x = n;
        long y = (x + 1) / 2;
        
        while (y < x)
        {
            x = y;
            y = (x + n / x) / 2;
        }
        
        return x;
    }

    /// <summary>
    /// Integer natural logarithm approximation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ILn(long x)
    {
        if (x <= 0) return long.MinValue;
        if (x == AdvancedFinancialKernels.RATE_SCALE) return 0;
        
        // Use Taylor series approximation for ln(1+x)
        long result = 0;
        long power = x - AdvancedFinancialKernels.RATE_SCALE;
        long term = power;
        
        for (int i = 1; i <= 10; i++)
        {
            if (i % 2 == 1)
                result += term / i;
            else
                result -= term / i;
            
            term = (term * power) / AdvancedFinancialKernels.RATE_SCALE;
        }
        
        return result;
    }

    /// <summary>
    /// Integer exponential function approximation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long IExp(long x)
    {
        if (x == 0) return AdvancedFinancialKernels.RATE_SCALE;
        if (x < -20 * AdvancedFinancialKernels.RATE_SCALE) return 0;
        if (x > 20 * AdvancedFinancialKernels.RATE_SCALE) return long.MaxValue;
        
        // Use Taylor series approximation
        long result = AdvancedFinancialKernels.RATE_SCALE;
        long term = x;
        
        for (int i = 1; i <= 20; i++)
        {
            result += term / Factorial(i);
            term = (term * x) / AdvancedFinancialKernels.RATE_SCALE;
        }
        
        return result;
    }

    /// <summary>
    /// Integer power function
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long IPow(long baseValue, int exponent)
    {
        if (exponent == 0) return AdvancedFinancialKernels.RATE_SCALE;
        if (exponent == 1) return baseValue;
        if (exponent < 0) return AdvancedFinancialKernels.RATE_SCALE / IPow(baseValue, -exponent);
        
        long result = AdvancedFinancialKernels.RATE_SCALE;
        long base = baseValue;
        
        while (exponent > 0)
        {
            if (exponent % 2 == 1)
            {
                result = (result * base) / AdvancedFinancialKernels.RATE_SCALE;
            }
            base = (base * base) / AdvancedFinancialKernels.RATE_SCALE;
            exponent /= 2;
        }
        
        return result;
    }

    /// <summary>
    /// Factorial function for small integers
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Factorial(int n)
    {
        if (n <= 1) return 1;
        if (n == 2) return 2;
        if (n == 3) return 6;
        if (n == 4) return 24;
        if (n == 5) return 120;
        if (n == 6) return 720;
        if (n == 7) return 5040;
        if (n == 8) return 40320;
        if (n == 9) return 362880;
        if (n == 10) return 3628800;
        
        long result = 3628800;
        for (int i = 11; i <= n; i++)
        {
            result *= i;
        }
        return result;
    }
}