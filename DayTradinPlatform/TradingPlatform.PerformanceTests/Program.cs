using System;
using System.Reflection;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Columns;

namespace TradingPlatform.PerformanceTests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Trading Platform Performance Tests");
            Console.WriteLine("==================================");
            Console.WriteLine();
            Console.WriteLine("Select test suite to run:");
            Console.WriteLine("1. All Benchmarks");
            Console.WriteLine("2. TradingResult Benchmarks");
            Console.WriteLine("3. Order Execution Benchmarks (Ultra-Low Latency)");
            Console.WriteLine("4. Golden Rules Benchmarks");
            Console.WriteLine("5. Load Tests (NBomber)");
            Console.WriteLine("0. Exit");
            Console.WriteLine();
            Console.Write("Enter your choice: ");

            var choice = Console.ReadLine();

            var config = DefaultConfig.Instance
                .AddExporter(MarkdownExporter.GitHub)
                .AddExporter(HtmlExporter.Default)
                .AddLogger(ConsoleLogger.Default)
                .AddColumn(TargetMethodColumn.Method)
                .AddColumn(StatisticColumn.Mean)
                .AddColumn(StatisticColumn.Error)
                .AddColumn(StatisticColumn.StdDev)
                .AddColumn(StatisticColumn.P95)
                .AddColumn(StatisticColumn.P99);

            switch (choice)
            {
                case "1":
                    BenchmarkRunner.Run(Assembly.GetExecutingAssembly(), config);
                    break;
                case "2":
                    BenchmarkRunner.Run<Benchmarks.TradingResultBenchmarks>(config);
                    BenchmarkRunner.Run<Benchmarks.TradingResultAsyncBenchmarks>(config);
                    break;
                case "3":
                    BenchmarkRunner.Run<Benchmarks.OrderExecutionBenchmarks>(config);
                    BenchmarkRunner.Run<Benchmarks.HighFrequencyOrderBenchmarks>(config);
                    break;
                case "4":
                    BenchmarkRunner.Run<Benchmarks.GoldenRulesBenchmarks>(config);
                    BenchmarkRunner.Run<Benchmarks.GoldenRulesOptimizationBenchmarks>(config);
                    break;
                case "5":
                    RunLoadTests();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Invalid choice!");
                    break;
            }

            Console.WriteLine();
            Console.WriteLine("Performance tests completed. Results saved in BenchmarkDotNet.Artifacts folder.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void RunLoadTests()
        {
            Console.WriteLine("Starting load tests...");
            var loadTestRunner = new LoadTests.TradingPlatformLoadTests();
            loadTestRunner.RunAllLoadTests();
        }
    }
}