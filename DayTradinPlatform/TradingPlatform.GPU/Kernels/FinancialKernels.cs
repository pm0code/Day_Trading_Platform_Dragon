using ILGPU;
using ILGPU.Runtime;

namespace TradingPlatform.GPU.Kernels;

/// <summary>
/// GPU kernels for financial calculations
/// All calculations use scaled integers to maintain decimal precision
/// </summary>
public static class FinancialKernels
{
    // Scaling factor for decimal precision (4 decimal places)
    public const int DECIMAL_SCALE = 10000;

    /// <summary>
    /// Calculates Simple Moving Average (SMA) for multiple securities in parallel
    /// </summary>
    public static void CalculateSMAKernel(
        Index1D index,
        ArrayView2D<long, Stride2D.DenseX> prices,     // Scaled prices (rows=securities, cols=time)
        ArrayView2D<long, Stride2D.DenseX> output,     // Output SMA values
        int period)
    {
        var security = index.X;
        var numPrices = prices.IntExtent.Y;

        // Calculate SMA for each valid position
        for (int i = period - 1; i < numPrices; i++)
        {
            long sum = 0;
            for (int j = 0; j < period; j++)
            {
                sum += prices[security, i - j];
            }
            output[security, i] = sum / period;
        }
    }

    /// <summary>
    /// Calculates Exponential Moving Average (EMA) for multiple securities
    /// </summary>
    public static void CalculateEMAKernel(
        Index1D index,
        ArrayView2D<long, Stride2D.DenseX> prices,
        ArrayView2D<long, Stride2D.DenseX> output,
        int period)
    {
        var security = index.X;
        var numPrices = prices.IntExtent.Y;
        
        // EMA multiplier (2 / (period + 1)) scaled
        long multiplier = (2L * DECIMAL_SCALE) / (period + 1);
        long oneMinusMultiplier = DECIMAL_SCALE - multiplier;

        // Initialize with SMA
        long sum = 0;
        for (int i = 0; i < period && i < numPrices; i++)
        {
            sum += prices[security, i];
        }
        
        if (period <= numPrices)
        {
            output[security, period - 1] = sum / period;
            
            // Calculate EMA for remaining values
            for (int i = period; i < numPrices; i++)
            {
                long previousEMA = output[security, i - 1];
                long currentPrice = prices[security, i];
                
                // EMA = (Price * multiplier) + (PreviousEMA * (1 - multiplier))
                long ema = ((currentPrice * multiplier) + (previousEMA * oneMinusMultiplier)) / DECIMAL_SCALE;
                output[security, i] = ema;
            }
        }
    }

    /// <summary>
    /// Calculates Relative Strength Index (RSI) for multiple securities
    /// </summary>
    public static void CalculateRSIKernel(
        Index1D index,
        ArrayView2D<long, Stride2D.DenseX> prices,
        ArrayView2D<long, Stride2D.DenseX> output,
        int period)
    {
        var security = index.X;
        var numPrices = prices.IntExtent.Y;

        if (numPrices < period + 1) return;

        long avgGain = 0;
        long avgLoss = 0;

        // Calculate initial average gain/loss
        for (int i = 1; i <= period; i++)
        {
            long change = prices[security, i] - prices[security, i - 1];
            if (change > 0)
                avgGain += change;
            else
                avgLoss -= change; // Make positive
        }

        avgGain = avgGain / period;
        avgLoss = avgLoss / period;

        // Calculate RSI for each period
        for (int i = period; i < numPrices; i++)
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
                output[security, i] = 100L * DECIMAL_SCALE; // 100 scaled
            }
            else
            {
                long rs = (avgGain * DECIMAL_SCALE) / avgLoss;
                long rsi = 100L * DECIMAL_SCALE - ((100L * DECIMAL_SCALE * DECIMAL_SCALE) / (DECIMAL_SCALE + rs));
                output[security, i] = rsi;
            }
        }
    }

    /// <summary>
    /// Screens stocks based on price and volume criteria
    /// </summary>
    public static void ScreenStocksKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> prices,      // Current prices (scaled)
        ArrayView1D<long, Stride1D.Dense> volumes,     // Current volumes
        ArrayView1D<long, Stride1D.Dense> marketCaps,  // Market caps (scaled)
        ArrayView1D<byte, Stride1D.Dense> results,     // Pass/fail results
        long minPrice,
        long maxPrice,
        long minVolume,
        long minMarketCap)
    {
        var i = index.X;
        
        // Check all criteria
        bool passed = true;
        
        if (minPrice > 0 && prices[i] < minPrice) passed = false;
        if (maxPrice > 0 && prices[i] > maxPrice) passed = false;
        if (minVolume > 0 && volumes[i] < minVolume) passed = false;
        if (minMarketCap > 0 && marketCaps[i] < minMarketCap) passed = false;
        
        results[i] = passed ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Calculates portfolio Value at Risk (VaR) using historical simulation
    /// </summary>
    public static void CalculateVaRKernel(
        Index1D index,
        ArrayView2D<long, Stride2D.DenseX> returns,    // Historical returns (scaled)
        ArrayView1D<long, Stride1D.Dense> weights,     // Portfolio weights (scaled)
        ArrayView1D<long, Stride1D.Dense> portfolioReturns, // Output: portfolio returns
        int numAssets,
        int numReturns)
    {
        var returnIndex = index.X;
        
        // Calculate portfolio return for this historical scenario
        long portfolioReturn = 0;
        for (int asset = 0; asset < numAssets; asset++)
        {
            long assetReturn = returns[asset, returnIndex];
            long weight = weights[asset];
            portfolioReturn += (assetReturn * weight) / DECIMAL_SCALE;
        }
        
        portfolioReturns[returnIndex] = portfolioReturn;
    }

    /// <summary>
    /// Monte Carlo simulation kernel for option pricing
    /// </summary>
    public static void MonteCarloOptionKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> spotPrices,  // Current spot prices (scaled)
        ArrayView1D<long, Stride1D.Dense> strikes,     // Strike prices (scaled)
        ArrayView1D<int, Stride1D.Dense> daysToExpiry, // Days to expiration
        ArrayView1D<long, Stride1D.Dense> volatilities,// Volatilities (scaled)
        ArrayView1D<long, Stride1D.Dense> riskFreeRate,// Risk-free rate (scaled)
        ArrayView2D<long, Stride2D.DenseX> randomNumbers, // Pre-generated random numbers
        ArrayView2D<long, Stride2D.DenseX> payoffs,    // Output payoffs
        int numSimulations,
        int numOptions)
    {
        var optionIndex = index.X;
        
        long S0 = spotPrices[optionIndex];
        long K = strikes[optionIndex];
        int T = daysToExpiry[optionIndex];
        long vol = volatilities[optionIndex];
        long r = riskFreeRate[optionIndex];
        
        // Convert days to years (scaled)
        long timeToExpiry = (T * DECIMAL_SCALE) / 365;
        
        // Run simulations for this option
        for (int sim = 0; sim < numSimulations; sim++)
        {
            // Get random number for this simulation
            long z = randomNumbers[optionIndex, sim];
            
            // Calculate terminal price using Black-Scholes formula
            // ST = S0 * exp((r - 0.5 * vol^2) * T + vol * sqrt(T) * Z)
            
            // Simplified approximation for GPU (avoid complex math)
            long drift = (r - (vol * vol) / (2 * DECIMAL_SCALE)) * timeToExpiry / DECIMAL_SCALE;
            long diffusion = (vol * InlineMath.ISqrt(timeToExpiry) * z) / (DECIMAL_SCALE * 100); // Approximate sqrt
            
            // Approximate exponential using Taylor series
            long expTerm = DECIMAL_SCALE + drift + diffusion;
            long ST = (S0 * expTerm) / DECIMAL_SCALE;
            
            // Calculate payoff for call option
            long payoff = ST > K ? ST - K : 0;
            payoffs[optionIndex, sim] = payoff;
        }
    }

    /// <summary>
    /// Bollinger Bands calculation kernel
    /// </summary>
    public static void CalculateBollingerBandsKernel(
        Index1D index,
        ArrayView2D<long, Stride2D.DenseX> prices,
        ArrayView2D<long, Stride2D.DenseX> upperBands,
        ArrayView2D<long, Stride2D.DenseX> lowerBands,
        ArrayView2D<long, Stride2D.DenseX> middleBands,
        int period,
        int numStdDev) // Number of standard deviations (scaled by 1000, e.g., 2000 = 2.0)
    {
        var security = index.X;
        var numPrices = prices.IntExtent.Y;

        for (int i = period - 1; i < numPrices; i++)
        {
            // Calculate SMA (middle band)
            long sum = 0;
            for (int j = 0; j < period; j++)
            {
                sum += prices[security, i - j];
            }
            long sma = sum / period;
            middleBands[security, i] = sma;

            // Calculate standard deviation
            long sumSquares = 0;
            for (int j = 0; j < period; j++)
            {
                long diff = prices[security, i - j] - sma;
                sumSquares += (diff * diff) / DECIMAL_SCALE;
            }
            
            long variance = sumSquares / period;
            long stdDev = InlineMath.ISqrt(variance * DECIMAL_SCALE); // Approximate square root

            // Calculate bands
            long bandWidth = (stdDev * numStdDev) / 1000;
            upperBands[security, i] = sma + bandWidth;
            lowerBands[security, i] = sma - bandWidth;
        }
    }
}

/// <summary>
/// Inline math utilities for GPU kernels
/// </summary>
public static class InlineMath
{
    /// <summary>
    /// Integer square root approximation
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static long ISqrt(long n)
    {
        if (n < 0) return 0;
        if (n == 0) return 0;
        
        long x = n;
        long y = (x + 1) / 2;
        
        while (y < x)
        {
            x = y;
            y = (x + n / x) / 2;
        }
        
        return x;
    }
}