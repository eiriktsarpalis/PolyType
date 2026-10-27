using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.Loader;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class InterfaceCompositionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void WitnessInterfacesAreLimitedToExplicitRegistrations(bool includeString, bool genericAttribute)
    {
        string rootAttribute = genericAttribute ? "[GenerateJsonFor<Payload>]" : "[GenerateJsonFor(typeof(Payload))]";
        string stringAttribute = genericAttribute ? "[GenerateJsonFor<string>]" : "[GenerateJsonFor(typeof(string))]";
        string source = $$"""
            using System.Collections.Generic;
            namespace TestNamespace;

            public record Payload(string? Text, List<string?>? Items, int? Count, Dictionary<string, string?>? Lookup);

            {{rootAttribute}}
            {{(includeString ? stringAttribute : "")}}
            public partial class Witness;

            public static class Consumer
            {
                public static string Roundtrip(string json) =>
                    FastPathJsonSerializer.Serialize<Payload, Witness>(
                        FastPathJsonSerializer.Deserialize<Payload, Witness>(json));
            }
            """;
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));

        INamedTypeSymbol? witness = result.Compilation.GetTypeByMetadataName("TestNamespace.Witness");
        Assert.NotNull(witness);
        Assert.Equal(includeString ? 2 : 1, witness.AllInterfaces.Length);
        Assert.Equal(includeString, witness.AllInterfaces.Any(i => i.TypeArguments[0].SpecialType is SpecialType.System_String));
        Assert.Contains(witness.AllInterfaces,
            i => SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], result.Compilation.GetTypeByMetadataName("TestNamespace.Payload")));

        var generatedSources = result.Driver.GetRunResult().Results.Single().GeneratedSources;
        Assert.Equal(includeString,
            generatedSources.Any(s => s.HintName == "FastPathJson.Witness.String.g.cs"));
        GeneratedSourceResult payload = Assert.Single(generatedSources,
            s => s.HintName == "FastPathJson.Witness.Payload.g.cs");
        GenericNameSyntax[] names = payload.SyntaxTree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes().OfType<GenericNameSyntax>()
            .ToArray();
        string expectedWitness = includeString
            ? "global::TestNamespace.Witness"
            : "global::PolyType.FastPathJson.Converters.StringConverter";
        GenericNameSyntax[] scalarCalls = names
            .Where(n => n.Identifier.ValueText is "Read" or "Write" && n.TypeArgumentList.Arguments.Count == 2 &&
                n.TypeArgumentList.Arguments[0].ToString() == "string").ToArray();
        Assert.NotEmpty(scalarCalls);
        Assert.All(scalarCalls, call => Assert.Equal(expectedWitness, call.TypeArgumentList.Arguments[1].ToString()));
        GenericNameSyntax[] lists = names.Where(n => n.Identifier.ValueText == "ListConverter").ToArray();
        Assert.NotEmpty(lists);
        Assert.All(lists, list => Assert.Equal(expectedWitness, list.TypeArgumentList.Arguments[1].ToString()));

        const string json = """{"Text":"text","Items":["a",null],"Count":42,"Lookup":{"key":null}}""";
        VerifyRoundtrips(result.Compilation, json, "Roundtrip");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void RootsOwnBodiesAndComposeThroughInterfaces(bool targetOwnsShape, bool witnessesOwnNode)
    {
        string source = $$"""
            using System.Collections.Generic;
            namespace TestNamespace;

            {{(targetOwnsShape ? "[GenerateJson]" : "")}}
            public partial record Node(int Value, Node? Next = null, List<Node>? Children = null);

            [GenerateJson]
            public partial record Envelope(Node Root, Dictionary<int, Node> Lookup);

            [GenerateJsonFor<Envelope>]
            {{(witnessesOwnNode ? "[GenerateJsonFor<Node>]" : "")}}
            public partial class FirstWitness;

            [GenerateJsonFor<Envelope>]
            {{(witnessesOwnNode ? "[GenerateJsonFor<Node>]" : "")}}
            public partial class SecondWitness;

            public static class Consumer
            {
                public static string Roundtrip(string json) =>
                    FastPathJsonSerializer.Serialize(FastPathJsonSerializer.Deserialize<Envelope>(json));

                public static string RoundtripFirst(string json) =>
                    FastPathJsonSerializer.Serialize<Envelope, FirstWitness>(
                        FastPathJsonSerializer.Deserialize<Envelope, FirstWitness>(json));

                public static string RoundtripSecond(string json) =>
                    FastPathJsonSerializer.Serialize<Envelope, SecondWitness>(
                        FastPathJsonSerializer.Deserialize<Envelope, SecondWitness>(json));
            }
            """;
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));

        SyntaxNode[] roots = result.Driver.GetRunResult().Results.Single().GeneratedSources
            .Select(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken)).ToArray();
        string[] converters = roots.SelectMany(r => r.DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(c => c.Parent is ClassDeclarationSyntax { Identifier.ValueText: "GeneratedJsonConverters" })
            .Select(c => c.Identifier.ValueText).ToArray();
        Assert.DoesNotContain("EnvelopeConverter", converters);
        Assert.Equal(!targetOwnsShape && !witnessesOwnNode, converters.Contains("NodeConverter"));

        MethodDeclarationSyntax[] implementations = roots.SelectMany(r => r.DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(m => m.Identifier.ValueText is "Read" or "Write" &&
                m.Parent is not ClassDeclarationSyntax { Identifier.ValueText: "GeneratedJsonConverters" }).ToArray();
        Assert.NotEmpty(implementations);
        Assert.All(implementations, method =>
        {
            Assert.NotNull(method.ExplicitInterfaceSpecifier);
            Assert.NotNull(method.Body);
            Assert.DoesNotContain(method.Modifiers, modifier => modifier.IsKind(SyntaxKind.PublicKeyword));
        });

        string expectedWitness = targetOwnsShape
            ? "global::TestNamespace.Node"
            : witnessesOwnNode
                ? "global::TestNamespace.FirstWitness"
                : "global::PolyType.FastPathJson.Generated.GeneratedJsonConverters.NodeConverter";
        GenericNameSyntax[] nodeCalls = roots.SelectMany(r => r.DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Select(i => i.Expression).OfType<MemberAccessExpressionSyntax>()
            .Select(m => m.Name).OfType<GenericNameSyntax>()
            .Where(n => n.Identifier.ValueText is "Read" or "Write" && n.TypeArgumentList.Arguments.Count == 2 &&
                n.TypeArgumentList.Arguments[0].ToString() == "global::TestNamespace.Node").ToArray();
        Assert.NotEmpty(nodeCalls);
        Assert.All(nodeCalls, call => Assert.Equal(expectedWitness, call.TypeArgumentList.Arguments[1].ToString()));

        Assert.Equal(witnessesOwnNode ? 2 : 1,
            result.Compilation.GetTypeByMetadataName("TestNamespace.FirstWitness")!.AllInterfaces.Length);
        const string json = """
            {"Root":{"Value":1,"Next":{"Value":2,"Next":null,"Children":null},"Children":[{"Value":3,"Next":null,"Children":[]}]},"Lookup":{"7":{"Value":4,"Next":null,"Children":null}}}
            """;
        VerifyRoundtrips(result.Compilation, json, "Roundtrip", "RoundtripFirst", "RoundtripSecond");
    }

    [Theory]
    [InlineData("""{"First":{"Value":"one"},"Second":{"Value":"two"},"Matrix":[[1,2],[3,4]],"Cube":[[[5],[6]]]}""")]
    [InlineData("""{"First":null,"Second":null,"Matrix":[],"Cube":[]}""")]
    public static void MultipleShapeKindsShareWitnessWithoutHelperCollisions(string json)
    {
        const string source = """
            namespace TestNamespace;

            public record First(string Value);
            public record Second(string Value);

            public record Envelope(First? First, Second? Second, int[,]? Matrix, int[,,]? Cube);

            [GenerateJsonFor<Envelope>]
            [GenerateJsonFor(typeof(First))]
            [GenerateJsonFor<Second>]
            [GenerateJsonFor<int[,]>]
            [GenerateJsonFor<int[,,]>]
            public partial class Witness;

            public static class Consumer
            {
                public static string Roundtrip(string json) =>
                    FastPathJsonSerializer.Serialize<Envelope, Witness>(
                        FastPathJsonSerializer.Deserialize<Envelope, Witness>(json));
            }
            """;
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.DoesNotContain(result.Driver.GetRunResult().Results.Single().GeneratedSources,
            s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes()
                .OfType<ClassDeclarationSyntax>().Any(c =>
                    c.Parent is ClassDeclarationSyntax { Identifier.ValueText: "GeneratedJsonConverters" }));
        VerifyRoundtrips(result.Compilation, json, "Roundtrip");
    }

    [Theory]
    [InlineData(false, """{"$type":"base","Value":1}""")]
    [InlineData(true, """{"$type":"base","Value":1}""")]
    [InlineData(false, """{"$type":"derived","Value":2}""")]
    [InlineData(true, """{"$type":"derived","Value":2}""")]
    public static void HierarchyCasesComposeAcrossRootImplementations(bool targetOwnsShape, string json)
    {
        string source = $$"""
            using System.Text.Json.Serialization;
            namespace TestNamespace;

            {{(targetOwnsShape ? "[GenerateJson]" : "")}}
            [JsonDerivedType(typeof(Base), "base")]
            [JsonDerivedType(typeof(Derived), "derived")]
            public partial class Base
            {
                public int Value { get; set; }
            }

            public sealed class Derived : Base;

            [GenerateJsonFor<Base>]
            [GenerateJsonFor<Derived>]
            public partial class FirstWitness;

            [GenerateJsonFor<Base>]
            public partial class SecondWitness;

            public static class Consumer
            {
                public static string RoundtripFirst(string json) =>
                    FastPathJsonSerializer.Serialize<Base, FirstWitness>(
                        FastPathJsonSerializer.Deserialize<Base, FirstWitness>(json));

                public static string RoundtripSecond(string json) =>
                    FastPathJsonSerializer.Serialize<Base, SecondWitness>(
                        FastPathJsonSerializer.Deserialize<Base, SecondWitness>(json));
            }
            """;
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        VerifyRoundtrips(result.Compilation, json, "RoundtripFirst", "RoundtripSecond");
    }

    private static void VerifyRoundtrips(Compilation compilation, string json, params string[] methods)
    {
        using MemoryStream stream = new();
        var emitted = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        stream.Position = 0;
        AssemblyLoadContext context = new(nameof(InterfaceCompositionTests), isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(stream);
            var consumer = assembly.GetType("TestNamespace.Consumer");
            Assert.NotNull(consumer);
            foreach (string name in methods)
            {
                var method = consumer.GetMethod(name);
                Assert.NotNull(method);
                Assert.Equal(json, method.CreateDelegate<Func<string, string>>()(json));
            }
        }
        finally
        {
            context.Unload();
        }
    }
}
