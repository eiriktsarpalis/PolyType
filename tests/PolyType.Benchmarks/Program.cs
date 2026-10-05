using BenchmarkDotNet.Running;
using System.Reflection;

if (args is ["--validate-json"])
{
    Validate<int>();
    Validate<string>();
    Validate<int[]>();
    Validate<List<int>>();
    Validate<Dictionary<string, int>>();
    Validate<MyPoco>();
    Validate<LargePoco>();
    Validate<CitmCatalog[]>();
    Validate<LargePoco[]>();
    return;
}

var assembly = Assembly.GetExecutingAssembly();
var switcher = new BenchmarkSwitcher(assembly);
var summaries = switcher.Run(args);
if (summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)))
{
    Environment.ExitCode = 1;
}

static void Validate<T>()
{
    var benchmark = new JsonSerializeBenchmark<T>();
    try
    {
        benchmark.Setup();
        Console.WriteLine($"{typeof(T)}: {benchmark.PayloadBytes:N0} UTF-8 bytes; all serializers and round trips agree.");
    }
    finally
    {
        benchmark.Cleanup();
    }
}