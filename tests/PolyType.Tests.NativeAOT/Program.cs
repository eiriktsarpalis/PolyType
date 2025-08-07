using PolyType.Tests.NativeAOT;
using System.Diagnostics;

namespace PolyType.Tests.NativeAOT;

// Console application entry point for Native AOT smoke tests
internal class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine("PolyType Native AOT Smoke Tests");
        Console.WriteLine("================================");
        
        int failedTests = 0;
        int totalTests = 0;

        // Run JSON serialization tests
        totalTests++;
        if (!RunTest("JSON Serialization", JsonSerializationSmokeTests.RunAllTests))
            failedTests++;

        // Run CBOR serialization tests
        totalTests++;
        if (!RunTest("CBOR Serialization", CborSerializationSmokeTests.RunAllTests))
            failedTests++;

        // Run XML serialization tests
        totalTests++;
        if (!RunTest("XML Serialization", XmlSerializationSmokeTests.RunAllTests))
            failedTests++;

        // Run structural equality tests
        totalTests++;
        if (!RunTest("Structural Equality", StructuralEqualitySmokeTests.RunAllTests))
            failedTests++;

        // Run random generation tests
        totalTests++;
        if (!RunTest("Random Generation", RandomGenerationSmokeTests.RunAllTests))
            failedTests++;

        // Run validation tests
        totalTests++;
        if (!RunTest("Validation", ValidationSmokeTests.RunAllTests))
            failedTests++;

        // Run pretty printer tests
        totalTests++;
        if (!RunTest("Pretty Printer", PrettyPrinterSmokeTests.RunAllTests))
            failedTests++;

        Console.WriteLine();
        Console.WriteLine($"Tests completed: {totalTests - failedTests}/{totalTests} passed");
        
        if (failedTests == 0)
        {
            Console.WriteLine("All tests passed! ✅");
            return 0;
        }
        else
        {
            Console.WriteLine($"{failedTests} test(s) failed! ❌");
            return 1;
        }
    }

    private static bool RunTest(string testName, Func<bool> testAction)
    {
        Console.Write($"Running {testName} tests... ");
        try
        {
            var stopwatch = Stopwatch.StartNew();
            bool result = testAction();
            stopwatch.Stop();
            
            if (result)
            {
                Console.WriteLine($"PASSED ({stopwatch.ElapsedMilliseconds}ms)");
                return true;
            }
            else
            {
                Console.WriteLine($"FAILED ({stopwatch.ElapsedMilliseconds}ms)");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            return false;
        }
    }
}