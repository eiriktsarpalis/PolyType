using Microsoft.CodeAnalysis;
using PolyType.FastPathJson.SourceGenerator;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class DependencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void AssembliesDoNotReferencePolyTypeOrItsGenerator(bool generator)
    {
        var assembly = generator ? typeof(JsonSourceGenerator).Assembly : typeof(FastPathJsonSerializer).Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
            reference.Name is "PolyType" or "PolyType.SourceGenerator");
        Assert.DoesNotContain(assembly.GetTypes(), type =>
            type.Namespace?.StartsWith("PolyType.SourceGenerator", StringComparison.Ordinal) is true);
    }

    [Theory]
    [InlineData("[PolyType.TypeShape(Marshaler = typeof(InvalidMarshaler))]", "", "")]
    [InlineData("", "[PolyType.PropertyShape(Ignore = true)]", "")]
    [InlineData("", "", "[PolyType.ConstructorShape]")]
    public static void PolyTypeMetadataDoesNotConfigureFastPathJson(string typeAttribute, string propertyAttribute, string constructorAttribute)
    {
        string source = $$"""
            namespace PolyType
            {
                public sealed class TypeShapeAttribute : System.Attribute { public System.Type? Marshaler { get; set; } }
                public sealed class PropertyShapeAttribute : System.Attribute { public bool Ignore { get; set; } }
                public sealed class ConstructorShapeAttribute : System.Attribute;
            }

            namespace TestNamespace
            {
                public sealed class InvalidMarshaler;

                [GenerateJson]
                {{typeAttribute}}
                public partial class Payload
                {
                    public Payload() { }
                    {{constructorAttribute}}
                    private Payload(string unused) { }
                    {{propertyAttribute}}
                    public int Value { get; set; }
                }
            }
            """;
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        string generated = string.Join("\n", result.Driver.GetRunResult().Results.Single().GeneratedSources.Select(s => s.SourceText));
        Assert.Contains("WritePropertyName(\"Value\")", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[GenerateJson(Marshaler = typeof(CustomMarshaler))]")]
    [InlineData("[GenerateJsonFor<Payload>(Marshaler = typeof(CustomMarshaler))]")]
    [InlineData("[GenerateJsonFor(typeof(Payload), Marshaler = typeof(CustomMarshaler))]")]
    public static void GenerationAttributesDoNotExposeMarshalerOptions(string attribute)
    {
        string source = $$"""
            public record Payload(int Value);
            public sealed class CustomMarshaler;

            {{attribute}}
            public partial class Witness;
            """;
        var result = CompilationHelpers.Run(source);
        Assert.Contains(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken), diagnostic =>
            diagnostic.Id == "CS0246" && diagnostic.GetMessage().Contains("Marshaler", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
    }
}
