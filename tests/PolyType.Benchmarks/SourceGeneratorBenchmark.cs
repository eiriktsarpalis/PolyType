extern alias SourceGenerator;

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator::PolyType.SourceGenerator;
using SourceGenerator::PolyType.SourceGenerator.Helpers;
using SourceGenerator::PolyType.SourceGenerator.Model;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace PolyType.Benchmarks;

[MemoryDiagnoser]
public class SourceGeneratorBenchmark
{
    private CSharpCompilation _compilation = null!;
    private CSharpParseOptions _parseOptions = null!;
    private ImmutableArray<TypeWithAttributeDeclarationContext> _declarations;

    [Params(1, 100)]
    public int TypeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        string[] platformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The runtime assembly list is unavailable."))
            .Split(Path.PathSeparator);

        _compilation = CSharpCompilation.Create(
            "SourceGeneratorBenchmarkInput",
            syntaxTrees: [CSharpSyntaxTree.ParseText(CreateSource(TypeCount), _parseOptions)],
            references: platformAssemblies
                .Where(path => Path.GetDirectoryName(path) == Path.GetDirectoryName(typeof(object).Assembly.Location))
                .Append(typeof(ITypeShape).Assembly.Location)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => MetadataReference.CreateFromFile(path)),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: true));

        SyntaxTree syntaxTree = _compilation.SyntaxTrees.Single();
        SemanticModel semanticModel = _compilation.GetSemanticModel(syntaxTree);
        var knownSymbols = new PolyTypeKnownSymbols(_compilation);
        _declarations = syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Select(declaration => (Syntax: declaration, Symbol: semanticModel.GetDeclaredSymbol(declaration)!))
            .Where(declaration => declaration.Symbol.GetAttributes().Any(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.GenerateShapeAttribute)))
            .Select(declaration => new TypeWithAttributeDeclarationContext
            {
                TypeSymbol = declaration.Symbol,
                Declarations = [(declaration.Syntax, semanticModel)],
            })
            .ToImmutableArray();
        if (_declarations.Length != TypeCount)
        {
            throw new InvalidOperationException("The parser input does not contain the expected annotated types.");
        }

        TypeShapeProviderModel? generatedModel = null;
        GeneratorDriver driver = CreateDriver(new PolyTypeGenerator { OnGeneratingSource = model => generatedModel = model })
            .RunGeneratorsAndUpdateCompilation(
            _compilation, out Compilation output, out var diagnostics);
        Diagnostic[] errors = diagnostics.Concat(output.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
        }

        var sources = driver.GetRunResult().Results.Single().GeneratedSources;
        if (sources.Length <= TypeCount)
        {
            throw new InvalidOperationException("The generator did not emit the expected type shapes.");
        }

        var parsed = ParserOnly() ?? throw new InvalidOperationException("The parser did not produce a model.");
        if (!parsed.Model.Equals(generatedModel) ||
            parsed.Diagnostics.Any(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning))
        {
            throw new InvalidOperationException("The isolated parser result differs from the full generator result.");
        }

        string generatedSource = string.Join("\n", sources.OrderBy(source => source.HintName, StringComparer.Ordinal)
            .Select(source => $"{source.HintName}\n{source.SourceText}"));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(generatedSource)));
        Console.WriteLine($"Struct collections: array={typeof(SourceGenerator::PolyType.Roslyn.ImmutableEquatableArray<int>).IsValueType}; " +
            $"dictionary={typeof(SourceGenerator::PolyType.Roslyn.ImmutableEquatableDictionary<int, int>).IsValueType}; " +
            $"set={typeof(SourceGenerator::PolyType.Roslyn.ImmutableEquatableSet<int>).IsValueType}");
        Console.WriteLine($"Types: {TypeCount}; generated files: {sources.Length}; characters: {generatedSource.Length}; SHA256: {hash}");
    }

    // A fresh driver prevents incremental caches from turning this into a no-op benchmark.
    [Benchmark]
    public GeneratorDriver FullRun()
    {
        return CreateDriver().RunGeneratorsAndUpdateCompilation(_compilation, out _, out _);
    }

    [Benchmark]
    public (TypeShapeProviderModel Model, ImmutableArray<Diagnostic> Diagnostics)? ParserOnly()
    {
        return Parser.ParseFromGenerateShapeAttributes(
            _declarations, new PolyTypeKnownSymbols(_compilation), CancellationToken.None);
    }

    private GeneratorDriver CreateDriver(PolyTypeGenerator? generator = null) => CSharpGeneratorDriver.Create(
        generators: [(generator ?? new PolyTypeGenerator()).AsSourceGenerator()],
        parseOptions: _parseOptions);

    private static string CreateSource(int typeCount)
    {
        var source = new StringBuilder("""
            using System;
            using System.Collections.Generic;
            using PolyType;

            namespace BenchmarkInput;

            public enum Status { Pending, Active, Completed }
            public record Address(string Street, string City, int? PostalCode);

            [DerivedTypeShape(typeof(TextMessage), Tag = 0)]
            [DerivedTypeShape(typeof(ImageMessage), Tag = 1)]
            public abstract record Message;
            public record TextMessage(string Text) : Message;
            public record ImageMessage(byte[] Data) : Message;

            """);

        for (int i = 0; i < typeCount; i++)
        {
            source.AppendLine($$"""
                [GenerateShape]
                public partial class Model{{i}}
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public Status Status { get; set; }
                    public DateTimeOffset Created { get; set; }
                    public Address? Address { get; set; }
                    public List<Message> Messages { get; set; } = [];
                    public Dictionary<string, Model{{i}}> Children { get; set; } = [];
                    public HashSet<int> Tags { get; set; } = [];
                    public (string Name, int Value)[] Values { get; set; } = [];
                    public Model{{i}}? Parent { get; set; }
                }
                """);
        }

        return source.ToString();
    }
}
