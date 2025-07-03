using System;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Utilities;
using TradingPlatform.Tests.Core.Canonical;

namespace TradingPlatform.Tests.Unit.Core.Mathematics
{
    /// <summary>
    /// Comprehensive unit tests for DecimalMath financial calculations
    /// Tests precision, edge cases, and compliance with financial standards
    /// </summary>
    public class DecimalMathTests : CanonicalTestBase<DecimalMath>
    {
        // Precision tolerance for financial calculations (8 decimal places)
        private const decimal PRECISION_TOLERANCE = 0.00000001m;
        
        public DecimalMathTests(ITestOutputHelper output) : base(output)
        {
        }
        
        protected override void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            // DecimalMath is static, no services needed
        }
        
        protected override DecimalMath CreateSystemUnderTest()
        {
            // Static class, return null
            return null;
        }
        
        #region Square Root Tests
        
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(4, 2)]
        [InlineData(9, 3)]
        [InlineData(16, 4)]
        [InlineData(25, 5)]
        [InlineData(100, 10)]
        [InlineData(144, 12)]
        [InlineData(0.25, 0.5)]
        [InlineData(0.0625, 0.25)]
        public void Sqrt_KnownValues_ReturnsCorrectResult(decimal input, decimal expected)
        {
            // Act
            var result = DecimalMath.Sqrt(input);
            
            // Assert
            AssertFinancialPrecision(expected, result);
        }
        
        [Theory]
        [InlineData(2, 1.41421356)]
        [InlineData(3, 1.73205081)]
        [InlineData(5, 2.23606798)]
        [InlineData(7, 2.64575131)]
        [InlineData(10, 3.16227766)]
        [InlineData(0.5, 0.70710678)]
        [InlineData(0.1, 0.31622777)]
        public void Sqrt_IrrationalNumbers_MaintainsPrecision(decimal input, decimal expected)
        {
            // Act
            var result = DecimalMath.Sqrt(input);
            
            // Assert
            AssertFinancialPrecision(expected, result);
            
            // Verify by squaring back
            var squared = result * result;
            AssertFinancialPrecision(input, squared);
        }
        
        [Fact]
        public void Sqrt_NegativeNumber_ThrowsArgumentException()
        {
            // Assert
            var ex = Assert.Throws<ArgumentException>(() => DecimalMath.Sqrt(-1));
            Assert.Contains("negative", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        
        [Theory]
        [InlineData(0.000000001)]
        [InlineData(1000000)]
        [InlineData(999999.99)]
        public void Sqrt_ExtremePrecisionValues_MaintainsAccuracy(decimal input)
        {
            // Act
            var result = DecimalMath.Sqrt(input);
            var squared = result * result;
            
            // Assert
            AssertFinancialPrecision(input, squared);
        }
        
        [Fact]
        public void Sqrt_PerformanceTest()
        {
            // Act & Assert - Should complete within 1ms for 1000 calculations
            AssertCompletesWithinAsync(100, async () =>
            {
                for (int i = 1; i <= 1000; i++)
                {
                    DecimalMath.Sqrt(i);
                }
                await Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }
        
        #endregion
        
        #region Logarithm Tests
        
        [Theory]
        [InlineData(1, 0)]
        [InlineData(2.71828183, 1)] // e
        [InlineData(10, 2.30258509)]
        [InlineData(100, 4.60517019)]
        [InlineData(0.5, -0.69314718)]
        [InlineData(0.1, -2.30258509)]
        public void Log_KnownValues_ReturnsCorrectResult(decimal input, decimal expected)
        {
            // Act
            var result = DecimalMath.Log(input);
            
            // Assert
            AssertFinancialPrecision(expected, result, 6); // Log is less precise
        }
        
        [Fact]
        public void Log_E_ReturnsOne()
        {
            // Act
            var result = DecimalMath.Log(DecimalMath.E);
            
            // Assert
            AssertFinancialPrecision(1, result, 6);
        }
        
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Log_NonPositiveValues_ThrowsArgumentException(decimal input)
        {
            // Assert
            var ex = Assert.Throws<ArgumentException>(() => DecimalMath.Log(input));
            Assert.Contains("non-positive", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void Log_InverseOfExp_ReturnsOriginal()
        {
            // Arrange
            decimal[] testValues = { 0.5m, 1m, 2m, 3m, 5m };
            
            foreach (var value in testValues)
            {
                // Act
                var log = DecimalMath.Log(value);
                var exp = DecimalMath.Exp(log);
                
                // Assert
                AssertFinancialPrecision(value, exp, 6);
            }
        }
        
        #endregion
        
        #region Exponential Tests
        
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2.71828183)]
        [InlineData(2, 7.38905610)]
        [InlineData(-1, 0.36787944)]
        [InlineData(0.5, 1.64872127)]
        public void Exp_KnownValues_ReturnsCorrectResult(decimal input, decimal expected)
        {
            // Act
            var result = DecimalMath.Exp(input);
            
            // Assert
            AssertFinancialPrecision(expected, result, 6);
        }
        
        [Fact]
        public void Exp_LargePositiveValue_ThrowsOverflowException()
        {
            // Assert
            Assert.Throws<OverflowException>(() => DecimalMath.Exp(100));
        }
        
        [Fact]
        public void Exp_LargeNegativeValue_ReturnsZero()
        {
            // Act
            var result = DecimalMath.Exp(-100);
            
            // Assert
            Assert.Equal(0, result);
        }
        
        #endregion
        
        #region Trigonometric Tests
        
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1.57079633, 1)] // PI/2
        [InlineData(3.14159265, 0)] // PI
        [InlineData(4.71238898, -1)] // 3*PI/2
        [InlineData(6.28318531, 0)] // 2*PI
        public void Sin_KeyAngles_ReturnsCorrectResult(decimal radians, decimal expected)
        {
            // Act
            var result = DecimalMath.Sin(radians);
            
            // Assert
            AssertFinancialPrecision(expected, result, 6);
        }
        
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1.57079633, 0)] // PI/2
        [InlineData(3.14159265, -1)] // PI
        [InlineData(4.71238898, 0)] // 3*PI/2
        [InlineData(6.28318531, 1)] // 2*PI
        public void Cos_KeyAngles_ReturnsCorrectResult(decimal radians, decimal expected)
        {
            // Act
            var result = DecimalMath.Cos(radians);
            
            // Assert
            AssertFinancialPrecision(expected, result, 6);
        }
        
        [Fact]
        public void Sin_Cos_PythagoreanIdentity()
        {
            // Test sin²(x) + cos²(x) = 1
            decimal[] angles = { 0.5m, 1m, 1.5m, 2m, 2.5m };
            
            foreach (var angle in angles)
            {
                // Act
                var sin = DecimalMath.Sin(angle);
                var cos = DecimalMath.Cos(angle);
                var identity = sin * sin + cos * cos;
                
                // Assert
                AssertFinancialPrecision(1, identity, 6);
            }
        }
        
        #endregion
        
        #region Power Function Tests
        
        [Theory]
        [InlineData(2, 0, 1)]
        [InlineData(2, 1, 2)]
        [InlineData(2, 2, 4)]
        [InlineData(2, 3, 8)]
        [InlineData(2, -1, 0.5)]
        [InlineData(10, 2, 100)]
        [InlineData(10, -2, 0.01)]
        public void Pow_IntegerExponents_ReturnsCorrectResult(decimal baseValue, decimal exponent, decimal expected)
        {
            // Act
            var result = DecimalMath.Pow(baseValue, exponent);
            
            // Assert
            AssertFinancialPrecision(expected, result);
        }
        
        [Theory]
        [InlineData(4, 0.5, 2)]
        [InlineData(9, 0.5, 3)]
        [InlineData(8, 0.33333333, 2)] // Cube root
        public void Pow_FractionalExponents_ReturnsCorrectResult(decimal baseValue, decimal exponent, decimal expected)
        {
            // Act
            var result = DecimalMath.Pow(baseValue, exponent);
            
            // Assert
            AssertFinancialPrecision(expected, result, 6);
        }
        
        [Fact]
        public void Pow_ZeroToZero_ThrowsArgumentException()
        {
            // Assert
            var ex = Assert.Throws<ArgumentException>(() => DecimalMath.Pow(0, 0));
            Assert.Contains("0^0", ex.Message);
        }
        
        [Fact]
        public void Pow_NegativeBaseFractionalExponent_ThrowsArgumentException()
        {
            // Assert
            var ex = Assert.Throws<ArgumentException>(() => DecimalMath.Pow(-2, 0.5m));
            Assert.Contains("Negative base", ex.Message);
        }
        
        #endregion
        
        #region Utility Function Tests
        
        [Theory]
        [InlineData(-5, 5)]
        [InlineData(5, 5)]
        [InlineData(0, 0)]
        [InlineData(-0.123, 0.123)]
        public void Abs_VariousInputs_ReturnsAbsoluteValue(decimal input, decimal expected)
        {
            // Act
            var result = DecimalMath.Abs(input);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        [Theory]
        [InlineData(1, 2, 2)]
        [InlineData(5, 3, 5)]
        [InlineData(-1, -2, -1)]
        [InlineData(0, 0, 0)]
        public void Max_TwoValues_ReturnsMaximum(decimal a, decimal b, decimal expected)
        {
            // Act
            var result = DecimalMath.Max(a, b);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        [Theory]
        [InlineData(1, 2, 1)]
        [InlineData(5, 3, 3)]
        [InlineData(-1, -2, -2)]
        [InlineData(0, 0, 0)]
        public void Min_TwoValues_ReturnsMinimum(decimal a, decimal b, decimal expected)
        {
            // Act
            var result = DecimalMath.Min(a, b);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        [Theory]
        [InlineData(1.2345, 2, 1.23)]
        [InlineData(1.2355, 2, 1.24)]
        [InlineData(1.2345, 3, 1.235)]
        [InlineData(-1.2345, 2, -1.23)]
        public void Round_VariousInputs_RoundsCorrectly(decimal input, int decimals, decimal expected)
        {
            // Act
            var result = DecimalMath.Round(input, decimals);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        [Theory]
        [InlineData(1.25, 1, 1.2)]  // Round to even (down)
        [InlineData(1.35, 1, 1.4)]  // Round to even (up)
        [InlineData(1.225, 2, 1.22)] // Round to even (down)
        [InlineData(1.235, 2, 1.24)] // Round to even (up)
        public void RoundBankers_MidpointValues_RoundsToEven(decimal input, int decimals, decimal expected)
        {
            // Act
            var result = DecimalMath.RoundBankers(input, decimals);
            
            // Assert
            Assert.Equal(expected, result);
        }
        
        #endregion
        
        #region Edge Case Tests
        
        [Fact]
        public void AllFunctions_MaxDecimalValue_HandlesGracefully()
        {
            // Arrange
            decimal nearMax = decimal.MaxValue / 10; // Avoid overflow
            
            // Act & Assert - Should not throw
            AssertNoExceptions(() =>
            {
                DecimalMath.Abs(nearMax);
                DecimalMath.Round(nearMax, 2);
                DecimalMath.RoundBankers(nearMax, 2);
                DecimalMath.Max(nearMax, 0);
                DecimalMath.Min(nearMax, 0);
            });
        }
        
        [Fact]
        public void AllFunctions_MinDecimalValue_HandlesGracefully()
        {
            // Arrange
            decimal nearMin = decimal.MinValue / 10; // Avoid underflow
            
            // Act & Assert - Should not throw
            AssertNoExceptions(() =>
            {
                DecimalMath.Abs(nearMin);
                DecimalMath.Round(nearMin, 2);
                DecimalMath.RoundBankers(nearMin, 2);
                DecimalMath.Max(nearMin, 0);
                DecimalMath.Min(nearMin, 0);
            });
        }
        
        #endregion
        
        #region Constants Validation
        
        [Fact]
        public void PI_Constant_HasCorrectPrecision()
        {
            // Assert
            Assert.Equal(3.1415926535897932384626433832795028841971693993751058209749445923m, DecimalMath.PI);
        }
        
        [Fact]
        public void E_Constant_HasCorrectPrecision()
        {
            // Assert
            Assert.Equal(2.7182818284590452353602874713526624977572470936999595749669676277m, DecimalMath.E);
        }
        
        #endregion
    }
}