using TradingPlatform.Testing.TestHarnesses;

namespace TradingPlatform.TestRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Use the canonical implementation
            await DataProviderTestHarness_Canonical.Main(args);
        }
    }
}