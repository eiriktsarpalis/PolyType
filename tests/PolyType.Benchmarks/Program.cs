using BenchmarkDotNet.Running;
using PolyType.FastPathJson;
using System.Reflection;

if (args is ["--validate-json"])
{
    Validate<int, JsonBenchmarkWitness>();
    Validate<string, JsonBenchmarkWitness>();
    Validate<int[], JsonBenchmarkWitness>();
    Validate<List<int>, JsonBenchmarkWitness>();
    Validate<Dictionary<string, int>, JsonBenchmarkWitness>();
    Validate<MyPoco, JsonBenchmarkWitness>();
    Validate<LargePoco, JsonBenchmarkWitness>();
    Validate<CitmCatalog[], JsonBenchmarkWitness>();
    Validate<LargePoco[], JsonBenchmarkWitness>();
    return;
}

var assembly = Assembly.GetExecutingAssembly();
var switcher = new BenchmarkSwitcher(assembly);
var summaries = switcher.Run(args);
if (summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)))
{
    Environment.ExitCode = 1;
}

static void Validate<T, TConverter>() where TConverter : IJsonSerializable<T>
{
    var benchmark = new JsonSerializeBenchmark<T, TConverter>();
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