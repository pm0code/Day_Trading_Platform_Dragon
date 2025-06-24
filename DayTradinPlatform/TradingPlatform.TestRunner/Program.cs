using TradingPlatform.Testing.TestHarnesses;

namespace TradingPlatform.TestRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "test-api-keys")
            {
                // Test API keys
                await ApiKeyTester.TestApiKeys();
            }
            else
            {
                // Use the canonical implementation
                await DataProviderTestHarness_Canonical.Main(args);
            }
        }
    }
}