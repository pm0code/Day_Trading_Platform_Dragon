using System;

namespace TradingPlatform.Core.Utilities
{
    /// <summary>
    /// Mathematical functions for decimal type to maintain financial precision
    /// as mandated by FinancialCalculationStandards.md
    /// </summary>
    public static class DecimalMath
    {
        public const decimal PI = 3.1415926535897932384626433832795028841971693993751058209749445923m;
        public const decimal E = 2.7182818284590452353602874713526624977572470936999595749669676277m;

        /// <summary>
        /// Calculate square root of a decimal value using Newton-Raphson method
        /// </summary>
        public static decimal Sqrt(decimal value)
        {
            if (value < 0)
                throw new ArgumentException("Cannot calculate square root of negative number");
            
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
            while (Math.Abs(x - root) > 0.0000000000000000001m && count < 100);

            return x;
        }

        /// <summary>
        /// Calculate natural logarithm using series expansion
        /// </summary>
        public static decimal Log(decimal value)
        {
            if (value <= 0)
                throw new ArgumentException("Logarithm is undefined for non-positive values");

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
            
            for (int i = 1; i <= 100; i++)
            {
                result += term / i;
                term *= -y;
                
                if (Math.Abs(term) < 0.0000000000000000001m)
                    break;
            }

            // Add back the normalization factor
            result += exp * Log(2);

            return result;
        }

        /// <summary>
        /// Natural logarithm of 2 (precomputed for efficiency)
        /// </summary>
        private static readonly decimal Log2 = 0.6931471805599453094172321214581765680755001343602552541206800094m;

        /// <summary>
        /// Calculate exponential function e^x
        /// </summary>
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
                throw new OverflowException("Exponential function overflow");

            // Use Taylor series: e^x = 1 + x + x^2/2! + x^3/3! + ...
            decimal result = 1;
            decimal term = 1;
            
            for (int i = 1; i <= 100; i++)
            {
                term *= x / i;
                result += term;
                
                if (Math.Abs(term) < 0.0000000000000000001m)
                    break;
            }

            return result;
        }

        /// <summary>
        /// Calculate sine using Taylor series
        /// </summary>
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
                
                if (Math.Abs(term) < 0.0000000000000000001m)
                    break;
            }

            return result;
        }

        /// <summary>
        /// Calculate cosine using Taylor series
        /// </summary>
        public static decimal Cos(decimal x)
        {
            // cos(x) = sin(x + PI/2)
            return Sin(x + PI / 2);
        }

        /// <summary>
        /// Calculate power function x^y
        /// </summary>
        public static decimal Pow(decimal x, decimal y)
        {
            if (x == 0 && y == 0)
                throw new ArgumentException("0^0 is undefined");
            
            if (x == 0)
                return 0;
            
            if (y == 0)
                return 1;
            
            if (x < 0 && y != Math.Floor((double)y))
                throw new ArgumentException("Negative base with non-integer exponent is not supported");

            // For integer exponents, use repeated multiplication
            if (y == Math.Floor((double)y))
            {
                decimal result = 1;
                decimal absY = Math.Abs(y);
                
                for (int i = 0; i < (int)absY; i++)
                {
                    result *= x;
                }
                
                return y < 0 ? 1 / result : result;
            }

            // For non-integer exponents, use exp(y * ln(x))
            return Exp(y * Log(Math.Abs(x)));
        }

        /// <summary>
        /// Calculate absolute value
        /// </summary>
        public static decimal Abs(decimal value)
        {
            return Math.Abs(value);
        }

        /// <summary>
        /// Calculate maximum of two values
        /// </summary>
        public static decimal Max(decimal a, decimal b)
        {
            return Math.Max(a, b);
        }

        /// <summary>
        /// Calculate minimum of two values
        /// </summary>
        public static decimal Min(decimal a, decimal b)
        {
            return Math.Min(a, b);
        }

        /// <summary>
        /// Round to specified number of decimal places
        /// </summary>
        public static decimal Round(decimal value, int decimals)
        {
            return Math.Round(value, decimals);
        }

        /// <summary>
        /// Round using banker's rounding (to even)
        /// </summary>
        public static decimal RoundBankers(decimal value, int decimals)
        {
            return Math.Round(value, decimals, MidpointRounding.ToEven);
        }
    }
}