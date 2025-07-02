using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical implementation for generating random decimal values with financial precision.
    /// This is the ONLY approved implementation for decimal random number generation
    /// across the entire trading platform.
    /// </summary>
    public class DecimalRandomCanonical
    {
        private readonly Random _random;
        private readonly object _lock = new object();
        
        // Thread-local storage for thread-safe random generation
        private static readonly ThreadLocal<DecimalRandomCanonical> _threadLocal = 
            new ThreadLocal<DecimalRandomCanonical>(() => new DecimalRandomCanonical());

        /// <summary>
        /// Gets a thread-safe instance of DecimalRandomCanonical
        /// </summary>
        public static DecimalRandomCanonical Instance => _threadLocal.Value!;

        /// <summary>
        /// Creates a new instance with optional seed
        /// </summary>
        public DecimalRandomCanonical(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Returns a random decimal value between 0.0 (inclusive) and 1.0 (exclusive)
        /// with full decimal precision
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal NextDecimal()
        {
            lock (_lock)
            {
                // Generate multiple random integers to fill decimal precision
                // Decimal has 96 bits of precision, we'll use 3 32-bit integers
                uint part1 = (uint)_random.Next();
                uint part2 = (uint)_random.Next();
                uint part3 = (uint)_random.Next();
                
                // Combine into a decimal between 0 and 1
                // Scale by 10^28 to get full precision
                decimal result = new decimal((int)part1, (int)part2, (int)part3, false, 28);
                
                // Ensure result is in [0, 1) range
                if (result >= 1m)
                    result = 0.99999999999999999999999999999m;
                
                return result;
            }
        }

        /// <summary>
        /// Returns a random decimal value between 0.0 (inclusive) and maxValue (exclusive)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal NextDecimal(decimal maxValue)
        {
            if (maxValue <= 0)
                throw new ArgumentException("Max value must be greater than zero", nameof(maxValue));
            
            return NextDecimal() * maxValue;
        }

        /// <summary>
        /// Returns a random decimal value between minValue (inclusive) and maxValue (exclusive)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal NextDecimal(decimal minValue, decimal maxValue)
        {
            if (maxValue <= minValue)
                throw new ArgumentException($"Max value ({maxValue}) must be greater than min value ({minValue})");
            
            decimal range = maxValue - minValue;
            return minValue + (NextDecimal() * range);
        }

        /// <summary>
        /// Returns a random integer between minValue (inclusive) and maxValue (exclusive)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt(int minValue, int maxValue)
        {
            lock (_lock)
            {
                return _random.Next(minValue, maxValue);
            }
        }

        /// <summary>
        /// Returns a random integer between 0 (inclusive) and maxValue (exclusive)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt(int maxValue)
        {
            lock (_lock)
            {
                return _random.Next(maxValue);
            }
        }

        /// <summary>
        /// Returns a random boolean value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBool()
        {
            lock (_lock)
            {
                return _random.Next(2) == 1;
            }
        }

        /// <summary>
        /// Returns a random boolean value with specified probability of being true
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBool(decimal probabilityTrue)
        {
            if (probabilityTrue < 0 || probabilityTrue > 1)
                throw new ArgumentException("Probability must be between 0 and 1", nameof(probabilityTrue));
            
            return NextDecimal() < probabilityTrue;
        }

        /// <summary>
        /// Generates a random decimal value following a normal (Gaussian) distribution
        /// using Box-Muller transform
        /// </summary>
        public decimal NextNormal(decimal mean = 0m, decimal standardDeviation = 1m)
        {
            if (standardDeviation < 0)
                throw new ArgumentException("Standard deviation must be non-negative", nameof(standardDeviation));

            lock (_lock)
            {
                // Box-Muller transform
                decimal u1 = NextDecimal();
                decimal u2 = NextDecimal();
                
                // Avoid log(0)
                if (u1 <= 0) u1 = 0.0000000000000000001m;
                
                // Calculate normal distribution
                decimal randStdNormal = DecimalMathCanonical.Sqrt(-2m * DecimalMathCanonical.Log(u1)) * 
                                       DecimalMathCanonical.Sin(2m * DecimalMathCanonical.PI * u2);
                
                return mean + standardDeviation * randStdNormal;
            }
        }

        /// <summary>
        /// Generates a random decimal value following an exponential distribution
        /// </summary>
        public decimal NextExponential(decimal lambda = 1m)
        {
            if (lambda <= 0)
                throw new ArgumentException("Lambda must be positive", nameof(lambda));

            decimal u = NextDecimal();
            
            // Avoid log(0)
            if (u <= 0) u = 0.0000000000000000001m;
            
            return -DecimalMathCanonical.Log(u) / lambda;
        }

        /// <summary>
        /// Shuffles an array in-place using Fisher-Yates algorithm
        /// </summary>
        public void Shuffle<T>(T[] array)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            lock (_lock)
            {
                for (int i = array.Length - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    (array[i], array[j]) = (array[j], array[i]);
                }
            }
        }

        /// <summary>
        /// Returns a random element from an array
        /// </summary>
        public T Choice<T>(T[] array)
        {
            if (array == null || array.Length == 0)
                throw new ArgumentException("Array cannot be null or empty", nameof(array));

            lock (_lock)
            {
                return array[_random.Next(array.Length)];
            }
        }

        /// <summary>
        /// Generates a random price change percentage within typical market ranges
        /// </summary>
        public decimal NextPriceChangePercent(decimal maxChangePercent = 0.05m)
        {
            if (maxChangePercent <= 0)
                throw new ArgumentException("Max change percent must be positive", nameof(maxChangePercent));
            
            // Generate change between -maxChangePercent and +maxChangePercent
            return NextDecimal(-maxChangePercent, maxChangePercent);
        }

        /// <summary>
        /// Generates a random volume based on average with some variation
        /// </summary>
        public decimal NextVolume(decimal averageVolume, decimal variationPercent = 0.3m)
        {
            if (averageVolume <= 0)
                throw new ArgumentException("Average volume must be positive", nameof(averageVolume));
            
            if (variationPercent < 0 || variationPercent > 1)
                throw new ArgumentException("Variation percent must be between 0 and 1", nameof(variationPercent));
            
            decimal minVolume = averageVolume * (1m - variationPercent);
            decimal maxVolume = averageVolume * (1m + variationPercent);
            
            return NextDecimal(minVolume, maxVolume);
        }
    }
}