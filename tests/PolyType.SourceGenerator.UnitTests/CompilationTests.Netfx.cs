using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using System.Collections.Immutable;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    [Theory]
    [InlineData("public int Value { get; set; }")]
    [InlineData("public int? Value { get; set; }")]
    [InlineData("public System.Collections.Generic.List<int>? Value { get; set; }")]
    [InlineData("public System.Collections.Generic.Dictionary<string, int>? Value { get; set; }")]
    [InlineData("public (int, string) Value { get; set; }")]
    [InlineData("public System.Tuple<int> Value { get; set; } = new System.Tuple<int>(42);")]
    [InlineData("public System.ValueTuple<int> Value { get; set; }")]
    [InlineData("public Microsoft.FSharp.Core.FSharpOption<int>? Value { get; set; }")]
    [InlineData("public Microsoft.FSharp.Core.FSharpValueOption<int> Value { get; set; }")]
    [InlineData("public Microsoft.FSharp.Core.Unit? Value { get; set; }")]
    [InlineData("public Microsoft.FSharp.Collections.FSharpMap<string, int>? Value { get; set; }")]
    [InlineData("public System.ReadOnlyMemory<int> Value { get; set; }")]
    [InlineData("""
        [MethodShape]
        private int Increment(ref int value) => ++value;
        """)]
    [InlineData("""
        [MethodShape]
        public System.Threading.Tasks.Task<int> Fetch(int value) => System.Threading.Tasks.Task.FromResult(value);
        """)]
    [InlineData("""
        [PropertyShape(IsRequired = true)]
        public string? Name { get; set; }
        """)]
    [InlineData("""
        [PropertyShape]
        private int _value;
        [ConstructorShape]
        private Payload(int value) => _value = value;
        """)]
    [InlineData("""
        [EventShape]
        public event System.Action<int> Changed { add { } remove { } }
        """)]
    [InlineData("""
        [PropertyShape]
        private int Value { get; set; }
        [ConstructorShape]
        private Payload(int value) => Value = value;
        """)]
    public static async Task NetStandardConsumers_CompileAgainstThePublishedContract(string members)
    {
        await CompileConsumer(members, ReferenceAssemblies.NetStandard.NetStandard20);
    }

    [Theory]
    [InlineData("net8.0", "public int? Value { get; set; }")]
    [InlineData("net9.0", "public int? Value { get; set; }")]
    [InlineData("net8.0", "public System.Collections.Generic.Dictionary<string, int>? Value { get; set; }")]
    [InlineData("net9.0", "public System.Collections.Generic.Dictionary<string, int>? Value { get; set; }")]
    [InlineData("net8.0", "[PropertyShape] private int Value { get; set; } [ConstructorShape] private Payload(int value) => Value = value;")]
    [InlineData("net9.0", "[PropertyShape] private int Value { get; set; } [ConstructorShape] private Payload(int value) => Value = value;")]
    public static async Task OlderFrameworkConsumers_CompileUsingTheNetStandardAsset(string framework, string members)
    {
        ReferenceAssemblies references = framework is "net8.0"
            ? ReferenceAssemblies.Net.Net80
            : ReferenceAssemblies.Net.Net90;
        await CompileConsumer(members, references);
    }

    private static async Task CompileConsumer(string members, ReferenceAssemblies referenceAssemblies)
    {
        var references = referenceAssemblies.WithPackages(
            ImmutableArray.Create(
                new PackageIdentity("System.Memory", "4.5.4"),
                new PackageIdentity("System.Threading.Tasks.Extensions", "4.5.4"),
                new PackageIdentity("FSharp.Core", "10.1.401")));
        ImmutableArray<MetadataReference> frameworkReferences = await references.ResolveAsync(
            LanguageNames.CSharp, TestContext.Current.CancellationToken);
        string polyTypeAssembly = Path.Combine(AppContext.BaseDirectory, "LegacyReferences", "PolyType.dll");
        Assert.True(File.Exists(polyTypeAssembly), $"Missing netstandard2.0 reference assembly: {polyTypeAssembly}");
        var parseOptions = CompilationHelpers.CreateParseOptions(LanguageVersion.CSharp9);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "NetStandardConsumer",
            [CSharpSyntaxTree.ParseText($$"""
                using PolyType;

                [GenerateShape]
                public partial class Payload
                {
                    {{members}}
                }
                """, parseOptions, cancellationToken: TestContext.Current.CancellationToken)],
            frameworkReferences.Add(MetadataReference.CreateFromFile(polyTypeAssembly)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
        using var image = new MemoryStream();
        var emitted = result.NewCompilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
    }
}
