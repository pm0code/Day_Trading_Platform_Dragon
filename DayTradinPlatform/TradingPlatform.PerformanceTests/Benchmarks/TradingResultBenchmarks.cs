using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using TradingPlatform.Core.Models;
using TradingPlatform.PerformanceTests.Framework;

namespace TradingPlatform.PerformanceTests.Benchmarks
{
    /// <summary>
    /// Benchmarks for TradingResult operations to ensure ultra-low latency
    /// </summary>
    public class TradingResultBenchmarks : CanonicalBenchmarkBase
    {
        private TradingResult<string> _successResult = null!;
        private TradingResult<string> _failureResult = null!;
        private readonly string _testValue = "test value";
        private readonly TradingError _testError = new("ERR001", "Test error");

        [GlobalSetup]
        public override void GlobalSetup()
        {
            base.GlobalSetup();
            _successResult = TradingResult<string>.Success(_testValue);
            _failureResult = TradingResult<string>.Failure(_testError);
        }

        [Benchmark(Baseline = true)]
        public TradingResult<string> CreateSuccess()
        {
            return TradingResult<string>.Success(_testValue);
        }

        [Benchmark]
        public TradingResult<string> CreateFailure()
        {
            return TradingResult<string>.Failure(_testError);
        }

        [Benchmark]
        public string AccessSuccessValue()
        {
            return _successResult.Value;
        }

        [Benchmark]
        public bool CheckIsSuccess()
        {
            return _successResult.IsSuccess;
        }

        [Benchmark]
        public TradingResult<int> MapSuccess()
        {
            return _successResult.Map(s => s.Length);
        }

        [Benchmark]
        public TradingResult<int> MapFailure()
        {
            return _failureResult.Map(s => s.Length);
        }

        [Benchmark]
        public TradingResult<string> BindSuccess()
        {
            return _successResult.Bind(s => TradingResult<string>.Success(s.ToUpper()));
        }

        [Benchmark]
        public int MatchSuccess()
        {
            return _successResult.Match(
                onSuccess: s => s.Length,
                onFailure: e => -1);
        }

        [Benchmark]
        public string GetValueOrDefault()
        {
            return _failureResult.GetValueOrDefault("default");
        }

        [Benchmark]
        public void OnSuccessAction()
        {
            _successResult.OnSuccess(s => { var len = s.Length; });
        }

        [Benchmark]
        public void OnFailureAction()
        {
            _failureResult.OnFailure(e => { var code = e.Code; });
        }
    }

    /// <summary>
    /// Benchmarks for async TradingResult operations
    /// </summary>
    public class TradingResultAsyncBenchmarks : CanonicalBenchmarkBase
    {
        [Benchmark]
        public async Task<TradingResult<string>> CreateSuccessAsync()
        {
            await Task.Yield();
            return TradingResult<string>.Success("async result");
        }

        [Benchmark]
        public async Task<TradingResult<string>> ChainedOperationsAsync()
        {
            var result = await Task.FromResult(TradingResult<int>.Success(42));
            
            return result
                .Map(x => x * 2)
                .Map(x => x.ToString())
                .Bind(x => TradingResult<string>.Success($"Value: {x}"));
        }

        [Benchmark]
        public async Task<string> MatchAsyncSuccess()
        {
            var result = await Task.FromResult(TradingResult<string>.Success("test"));
            
            return await result.Match(
                onSuccess: async s => { await Task.Yield(); return s.ToUpper(); },
                onFailure: async e => { await Task.Yield(); return "ERROR"; });
        }
    }
}