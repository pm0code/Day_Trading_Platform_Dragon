using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingPlatform.CodeAnalysis.Examples
{
    /// <summary>
    /// Example code demonstrating various analyzer violations.
    /// This file is for documentation purposes only.
    /// </summary>
    public class AnalyzerExamples
    {
        // TP0001: Use decimal for monetary values
        public class BadFinancialCalculation
        {
            public double Price { get; set; }         // ❌ TP0001: Should use decimal
            public float TotalAmount { get; set; }    // ❌ TP0001: Should use decimal
            public decimal CorrectPrice { get; set; } // ✅ Correct usage
        }

        // TP0101: Extend canonical base class
        public class BadService // ❌ TP0101: Should extend CanonicalServiceBase
        {
            private readonly ILogger _logger; // ❌ Direct logger usage

            public BadService(ILogger logger)
            {
                _logger = logger;
            }
        }

        // TP0102: Use TradingResult for operations
        public class BadOperations
        {
            public string GetData() // ❌ TP0102: Should return TradingResult<string>
            {
                throw new Exception("Error"); // ❌ TP0501: No silent failures
            }

            public TradingResult<string> GoodGetData() // ✅ Correct
            {
                try
                {
                    return TradingResult<string>.Success("data");
                }
                catch (Exception ex)
                {
                    LogError("Failed to get data", ex); // ✅ Proper error handling
                    return TradingResult<string>.Failure("GET_DATA_FAILED", ex.Message, ex);
                }
            }
        }

        // TP0201-TP0204: Performance issues
        [PerformanceCritical]
        public class BadPerformance
        {
            public void ProcessInHotPath()
            {
                // ❌ TP0203: Allocation in hot path
                var list = new List<int>();
                
                // ❌ TP0201: Boxing operation
                object boxed = 42;
                
                // ❌ TP0203: LINQ in performance-critical code
                var result = list.Where(x => x > 0).ToList();
                
                // ❌ String concatenation in loop
                string text = "";
                for (int i = 0; i < 100; i++)
                {
                    text += i.ToString(); // Allocates new string each time
                }
            }

            public void GoodPerformance()
            {
                // ✅ Use ArrayPool for temporary arrays
                var pool = ArrayPool<int>.Shared;
                var array = pool.Rent(100);
                try
                {
                    // Use array
                }
                finally
                {
                    pool.Return(array);
                }

                // ✅ Use Span<T> for stack allocation
                Span<int> span = stackalloc int[100];
                
                // ✅ Use StringBuilder for string concatenation
                var sb = new StringBuilder();
                for (int i = 0; i < 100; i++)
                {
                    sb.Append(i);
                }
            }
        }

        // TP0301: Security issues
        public class BadSecurity
        {
            // ❌ TP0301: Hardcoded secret
            private const string ApiKey = "sk-1234567890abcdef";
            
            // ❌ TP0302: SQL injection vulnerability
            public void ExecuteQuery(string userInput)
            {
                string query = "SELECT * FROM Users WHERE Name = '" + userInput + "'";
                // Execute query...
            }
        }

        // Example of proper canonical service
        public class GoodService : CanonicalServiceBase
        {
            public GoodService(ITradingLogger logger) 
                : base(logger, nameof(GoodService))
            {
            }

            protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
            {
                LogInfo("Initializing service");
                await base.OnInitializeAsync(cancellationToken);
            }

            protected override async Task OnStartAsync(CancellationToken cancellationToken)
            {
                LogInfo("Starting service");
                await base.OnStartAsync(cancellationToken);
            }

            protected override async Task OnStopAsync(CancellationToken cancellationToken)
            {
                LogInfo("Stopping service");
                await base.OnStopAsync(cancellationToken);
            }

            public async Task<TradingResult<decimal>> CalculatePriceAsync(decimal basePrice, decimal margin)
            {
                try
                {
                    if (basePrice <= 0)
                    {
                        return TradingResult<decimal>.Failure("INVALID_PRICE", "Base price must be positive");
                    }

                    var finalPrice = basePrice * (1 + margin);
                    return TradingResult<decimal>.Success(finalPrice);
                }
                catch (Exception ex)
                {
                    LogError("Failed to calculate price", ex);
                    return TradingResult<decimal>.Failure("CALCULATION_FAILED", ex.Message, ex);
                }
            }
        }
    }

    // Stub types for examples
    public interface ILogger { }
    public interface ITradingLogger : ICanonicalLogger { }
    public interface ICanonicalLogger { }
    
    public class CanonicalServiceBase 
    {
        protected CanonicalServiceBase(ITradingLogger logger, string serviceName) { }
        protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected void LogInfo(string message) { }
        protected void LogError(string message, Exception ex) { }
    }
    
    public struct TradingResult<T>
    {
        public static TradingResult<T> Success(T value) => default;
        public static TradingResult<T> Failure(string code, string message, Exception ex = null) => default;
    }
    
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class PerformanceCriticalAttribute : Attribute { }
}