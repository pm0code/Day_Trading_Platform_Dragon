using System;
using System.Reflection;
using QuanTAlib;

namespace QuanTAlibApiTest
{
    /// <summary>
    /// Empirical testing program to discover QuanTAlib API patterns
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("QuanTAlib API Discovery Test");
            Console.WriteLine("============================");
            
            DiscoverAvailableClasses();
            TestKnownIndicators();
        }
        
        static void DiscoverAvailableClasses()
        {
            Console.WriteLine("\n1. Discovering Available QuanTAlib Classes:");
            
            var assembly = typeof(QuanTAlib.Rsi).Assembly;
            var types = assembly.GetExportedTypes();
            
            Console.WriteLine($"Found {types.Length} public types:");
            
            // Look for Bollinger Band related indicators
            Console.WriteLine("\nBollinger Bands related indicators:");
            foreach (var type in types)
            {
                if (type.Name.ToLower().Contains("band") || 
                    type.Name.ToLower().Contains("bb") ||
                    type.Name.ToLower().Contains("upper") ||
                    type.Name.ToLower().Contains("lower") ||
                    type.Name.ToLower().Contains("middle"))
                {
                    Console.WriteLine($"  *** {type.Name}");
                }
            }
            
            // Look for MACD related indicators
            Console.WriteLine("\nMACD related indicators:");
            foreach (var type in types)
            {
                if (type.Name.ToLower().Contains("macd") ||
                    type.Name.ToLower().Contains("signal") ||
                    type.Name.ToLower().Contains("histogram"))
                {
                    Console.WriteLine($"  *** {type.Name}");
                }
            }
            
            // Look for any indicator that might have multiple components
            Console.WriteLine("\nOther potential multi-component indicators:");
            foreach (var type in types)
            {
                var name = type.Name.ToLower();
                if (name.Contains("stoch") || name.Contains("aroon") || 
                    name.Contains("dmi") || name.Contains("adx"))
                {
                    Console.WriteLine($"  *** {type.Name}");
                }
            }
        }
        
        static void TestKnownIndicators()
        {
            Console.WriteLine("\n2. Testing Target Indicators:");
            
            // Test Bband (Bollinger Bands)
            try
            {
                var bb = new QuanTAlib.Bband(20, 2.0);
                var testValue = new QuanTAlib.TValue(DateTime.Now, 100.0);
                var result = bb.Calc(testValue);
                
                Console.WriteLine($"✓ Bband result type: {result.GetType().Name}");
                Console.WriteLine($"  Bband properties:");
                foreach (var prop in result.GetType().GetProperties())
                {
                    var value = prop.GetValue(result);
                    Console.WriteLine($"    - {prop.Name}: {value} ({prop.PropertyType.Name})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Bband test failed: {ex.Message}");
            }
            
            // Test MACD
            try
            {
                var macd = new QuanTAlib.Macd(12, 26, 9);
                var testValue = new QuanTAlib.TValue(DateTime.Now, 100.0);
                var result = macd.Calc(testValue);
                
                Console.WriteLine($"✓ MACD result type: {result.GetType().Name}");
                Console.WriteLine($"  MACD properties:");
                foreach (var prop in result.GetType().GetProperties())
                {
                    var value = prop.GetValue(result);
                    Console.WriteLine($"    - {prop.Name}: {value} ({prop.PropertyType.Name})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ MACD test failed: {ex.Message}");
            }
            
            // Test with multiple values to see pattern
            Console.WriteLine("\n3. Testing Bband with Multiple Values:");
            try
            {
                var bb = new QuanTAlib.Bband(3, 1.0); // Small period for quick test
                double[] testPrices = { 100.0, 101.0, 102.0, 101.5, 100.5 };
                
                foreach (var price in testPrices)
                {
                    var value = new QuanTAlib.TValue(DateTime.Now, price);
                    var result = bb.Calc(value);
                    Console.WriteLine($"  Price {price}: Value={result.Value:F4}, IsHot={result.IsHot}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Multi-value Bband test failed: {ex.Message}");
            }
        }
    }
}