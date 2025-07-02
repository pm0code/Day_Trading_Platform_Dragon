using System;
using System.Runtime.CompilerServices;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Canonical
{
    /// <summary>
    /// Canonical implementation of mathematical functions for decimal type to maintain financial precision
    /// as mandated by FinancialCalculationStandards.md. This is the ONLY approved implementation
    /// for decimal math operations across the entire trading platform.
    /// </summary>
    public static class DecimalMathCanonical
    {
        public const decimal PI = 3.1415926535897932384626433832795028841971693993751058209749445923m;
        public const decimal E = 2.7182818284590452353602874713526624977572470936999595749669676277m;
        
        private const decimal EPSILON = 0.0000000000000000001m;
        private const int MAX_ITERATIONS = 100;

        /// <summary>
        /// Calculate square root of a decimal value using Newton-Raphson method
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Sqrt(decimal value)
        {
            if (value < 0)
                throw new ArgumentException($"Cannot calculate square root of negative number: {value}", nameof(value));
            
            if (value == 0)
                return 0;

            decimal x = value;
            decimal root;
            int count = 0;

            do
            {
                root = x;
                x = (x + value / x) / 2;
                count++;
            }
            while (Abs(x - root) > EPSILON && count < MAX_ITERATIONS);

            if (count >= MAX_ITERATIONS)
                throw new InvalidOperationException($"Square root calculation did not converge for value: {value}");

            return x;
        }

        /// <summary>
        /// Calculate natural logarithm using series expansion
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Log(decimal value)
        {
            if (value <= 0)
                throw new ArgumentException($"Logarithm is undefined for non-positive values: {value}", nameof(value));

            // For better convergence, normalize the input
            int exp = 0;
            decimal x = value;
            
            while (x > 2)
            {
                x /= 2;
                exp++;
            }
            
            while (x < 1)
            {
                x *= 2;
                exp--;
            }

            // Use Taylor series for ln(1+y) where y = x-1
            decimal y = x - 1;
            decimal result = 0;
            decimal term = y;
            
            for (int i = 1; i <= MAX_ITERATIONS; i++)
            {
                result += term / i;
                term *= -y;
                
                if (Abs(term) < EPSILON)
                    break;
            }

            // Add back the normalization factor
            result += exp * Log2;

            return result;
        }

        /// <summary>
        /// Natural logarithm of 2 (precomputed for efficiency)
        /// </summary>
        private static readonly decimal Log2 = 0.6931471805599453094172321214581765680755001343602552541206800094m;

        /// <summary>
        /// Calculate logarithm base 10
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Log10(decimal value)
        {
            return Log(value) / Log(10);
        }

        /// <summary>
        /// Calculate exponential function e^x
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Exp(decimal x)
        {
            // Handle special cases
            if (x == 0)
                return 1;

            // For large negative values, return 0 to avoid underflow
            if (x < -50)
                return 0;

            // For large positive values, throw overflow exception
            if (x > 50)
                throw new OverflowException($"Exponential function overflow for value: {x}");

            // Use Taylor series: e^x = 1 + x + x^2/2! + x^3/3! + ...
            decimal result = 1;
            decimal term = 1;
            
            for (int i = 1; i <= MAX_ITERATIONS; i++)
            {
                term *= x / i;
                result += term;
                
                if (Abs(term) < EPSILON)
                    break;
            }

            return result;
        }

        /// <summary>
        /// Calculate sine using Taylor series
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Sin(decimal x)
        {
            // Normalize angle to [-PI, PI]
            x = x % (2 * PI);
            if (x > PI)
                x -= 2 * PI;
            else if (x < -PI)
                x += 2 * PI;

            decimal result = 0;
            decimal term = x;
            
            for (int i = 1; i <= 20; i++)
            {
                result += term;
                term *= -x * x / ((2 * i) * (2 * i + 1));
                
                if (Abs(term) < EPSILON)
                    break;
            }

            return result;
        }

        /// <summary>
        /// Calculate cosine using Taylor series
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Cos(decimal x)
        {
            // cos(x) = sin(x + PI/2)
            return Sin(x + PI / 2);
        }

        /// <summary>
        /// Calculate tangent
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Tan(decimal x)
        {
            var cosX = Cos(x);
            if (Abs(cosX) < EPSILON)
                throw new ArgumentException($"Tangent undefined at x = {x} (cosine is zero)", nameof(x));
            
            return Sin(x) / cosX;
        }

        /// <summary>
        /// Calculate power function x^y for decimal values
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Pow(decimal x, decimal y)
        {
            if (x == 0 && y == 0)
                throw new ArgumentException("0^0 is undefined");
            
            if (x == 0)
                return y > 0 ? 0 : throw new ArgumentException("0 raised to negative power is undefined");
            
            if (y == 0)
                return 1;
            
            // For integer exponents, use repeated multiplication for precision
            if (y == Floor(y))
            {
                decimal result = 1;
                decimal absY = Abs(y);
                decimal baseValue = x;
                
                // Use binary exponentiation for efficiency
                int exp = (int)absY;
                while (exp > 0)
                {
                    if ((exp & 1) == 1)
                        result *= baseValue;
                    baseValue *= baseValue;
                    exp >>= 1;
                }
                
                return y < 0 ? 1 / result : result;
            }

            // For negative base with non-integer exponent
            if (x < 0)
                throw new ArgumentException($"Negative base ({x}) with non-integer exponent ({y}) is not supported");

            // For non-integer exponents, use exp(y * ln(x))
            return Exp(y * Log(x));
        }

        /// <summary>
        /// Calculate power function x^y where y is an integer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Pow(decimal x, int y)
        {
            if (x == 0 && y == 0)
                throw new ArgumentException("0^0 is undefined");
            
            if (x == 0)
                return y > 0 ? 0 : throw new ArgumentException("0 raised to negative power is undefined");
            
            if (y == 0)
                return 1;

            decimal result = 1;
            int absY = Math.Abs(y);
            decimal baseValue = x;
            
            // Use binary exponentiation for efficiency
            while (absY > 0)
            {
                if ((absY & 1) == 1)
                    result *= baseValue;
                baseValue *= baseValue;
                absY >>= 1;
            }
            
            return y < 0 ? 1 / result : result;
        }

        /// <summary>
        /// Calculate absolute value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Abs(decimal value)
        {
            return value < 0 ? -value : value;
        }

        /// <summary>
        /// Calculate maximum of two values
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Max(decimal a, decimal b)
        {
            return a > b ? a : b;
        }

        /// <summary>
        /// Calculate minimum of two values
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Min(decimal a, decimal b)
        {
            return a < b ? a : b;
        }

        /// <summary>
        /// Calculate maximum of array of values
        /// </summary>
        public static decimal Max(params decimal[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values array cannot be null or empty", nameof(values));
            
            decimal max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > max)
                    max = values[i];
            }
            return max;
        }

        /// <summary>
        /// Calculate minimum of array of values
        /// </summary>
        public static decimal Min(params decimal[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values array cannot be null or empty", nameof(values));
            
            decimal min = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < min)
                    min = values[i];
            }
            return min;
        }

        /// <summary>
        /// Round to specified number of decimal places
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Round(decimal value, int decimals)
        {
            return Math.Round(value, decimals);
        }

        /// <summary>
        /// Round using banker's rounding (to even)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal RoundBankers(decimal value, int decimals)
        {
            return Math.Round(value, decimals, MidpointRounding.ToEven);
        }

        /// <summary>
        /// Get the floor (largest integer less than or equal to value)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Floor(decimal value)
        {
            return Math.Floor(value);
        }

        /// <summary>
        /// Get the ceiling (smallest integer greater than or equal to value)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Ceiling(decimal value)
        {
            return Math.Ceiling(value);
        }

        /// <summary>
        /// Truncate decimal to integer part
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Truncate(decimal value)
        {
            return Math.Truncate(value);
        }

        /// <summary>
        /// Get the sign of a decimal value (-1, 0, or 1)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(decimal value)
        {
            return Math.Sign(value);
        }

        /// <summary>
        /// Clamp a value between min and max
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Clamp(decimal value, decimal min, decimal max)
        {
            if (min > max)
                throw new ArgumentException($"Min value ({min}) cannot be greater than max value ({max})");
            
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}