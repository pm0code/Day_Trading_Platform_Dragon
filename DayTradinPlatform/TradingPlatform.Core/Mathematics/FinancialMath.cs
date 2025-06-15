// d:\Projects\C#.Net\DayTradingPlatform-P\DayTradinPlatform\FinancialMath.cs
namespace TradingPlatform.Core.Mathematics
{
    public static class FinancialMath
    {
        private const int DEFAULT_PRECISION = 10;
        private const decimal EPSILON = 0.0000000001m;

        public static decimal Sqrt(decimal value)
        {
            if (value < 0m) throw new ArgumentException("Cannot calculate square root of negative number");
            if (value == 0m) return 0m;
            if (value == 1m) return 1m;

            decimal guess = value / 2m;
            decimal previousGuess;

            do
            {
                previousGuess = guess;
                guess = (guess + value / guess) / 2m;
            } while (Math.Abs(guess - previousGuess) > EPSILON);

            return Math.Round(guess, DEFAULT_PRECISION, MidpointRounding.ToEven);
        }

        public static decimal StandardDeviation(IEnumerable<decimal> values)
        {
            var variance = Variance(values);
            return Sqrt(variance);
        }

        public static decimal Variance(IEnumerable<decimal> values)
        {
            var valueList = values.ToList();
            if (!valueList.Any()) return 0m;

            var mean = valueList.Average();
            var sumOfSquaredDifferences = valueList.Sum(v => (v - mean) * (v - mean));
            var variance = sumOfSquaredDifferences / valueList.Count;

            return Math.Round(variance, DEFAULT_PRECISION, MidpointRounding.ToEven);
        }

        public static decimal RoundFinancial(decimal value, int decimals = 2)
        {
            return Math.Round(value, decimals, MidpointRounding.ToEven);
        }

        public static decimal CalculatePercentage(decimal value, decimal total)
        {
            if (total == 0m) return 0m;
            return RoundFinancial((value / total) * 100m, 4);
        }

        public static decimal CalculatePercentageChange(decimal oldValue, decimal newValue)
        {
            if (oldValue == 0m) return 0m;
            return RoundFinancial(((newValue - oldValue) / oldValue) * 100m, 4);
        }
    }

    public static class DecimalExtensions
    {
        public static decimal ToFinancialPrecision(this decimal value, int decimals = 2)
        {
            return FinancialMath.RoundFinancial(value, decimals);
        }

        public static decimal PercentageOf(this decimal value, decimal total)
        {
            return FinancialMath.CalculatePercentage(value, total);
        }

        public static decimal PercentageChangeTo(this decimal oldValue, decimal newValue)
        {
            return FinancialMath.CalculatePercentageChange(oldValue, newValue);
        }
    }
}
// 75 lines
