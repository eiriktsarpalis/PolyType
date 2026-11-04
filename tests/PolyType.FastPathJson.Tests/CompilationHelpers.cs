using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PolyType.FastPathJson.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace PolyType.FastPathJson.Tests;

internal static class CompilationHelpers
{
    public static CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.Preview);

    public static (Compilation Input, Compilation Compilation, GeneratorDriver Driver, ImmutableArray<Diagnostic> Diagnostics) Run(string source)
    {
        string[] paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        IEnumerable<string> references = paths.Where(p =>
            !Path.GetFileName(p).StartsWith("PolyType", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Path.GetFileName(p), "System.Text.Json.dll", StringComparison.OrdinalIgnoreCase))
            .Append(typeof(System.Text.Json.Utf8JsonWriter).Assembly.Location)
            .Append(typeof(FastPathJsonSerializer).Assembly.Location);
        Compilation input = CSharpCompilation.Create("FastPathJsonGeneratedTests",
            [CSharpSyntaxTree.ParseText("using PolyType.FastPathJson;\n" + source, ParseOptions, cancellationToken: TestContext.Current.CancellationToken)],
            references.Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new JsonSourceGenerator().AsSourceGenerator()], parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out Compilation output, out ImmutableArray<Diagnostic> diagnostics,
            TestContext.Current.CancellationToken);
        return (input, output, driver, diagnostics);
    }
}
