using Microsoft.CodeAnalysis;
using System.Runtime.Loader;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class GeneratorContractTests
{
    [Theory]
    [InlineData("[GenerateJson] public static partial class Payload;", "FJ0001")]
    [InlineData("[GenerateJson] public partial interface Payload;", "FJ0001")]
    [InlineData("[GenerateJson] file partial class Payload;", "FJ0001")]
    [InlineData("[GenerateJsonFor(null)] public partial class Witness;", "FJ0001")]
    [InlineData("[GenerateJsonFor<System.Memory<int>>] public partial class Witness;", "FJ0002")]
    [InlineData("[GenerateJsonFor<System.Span<int>>] public partial class Witness;", "FJ0002")]
    [InlineData("[GenerateJsonFor<System.Collections.Hashtable>] public partial class Witness;", "FJ0002")]
    [InlineData("[GenerateJsonFor<System.Collections.Generic.IAsyncEnumerable<int>>] public partial class Witness;", "FJ0002")]
    [InlineData("[GenerateJson] public partial class Payload { private Payload() { } }", "FJ0002")]
    [InlineData("""
        [GenerateJson] public partial class Payload
        {
            [System.Text.Json.Serialization.JsonPropertyName("same")] public int First { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("same")] public int Second { get; set; }
        }
        """, "FJ0002")]
    [InlineData("""
        [GenerateJson] public partial class Payload
        {
            [System.Text.Json.Serialization.JsonConstructor] public Payload() { }
            [System.Text.Json.Serialization.JsonConstructor] public Payload(int value) { }
        }
        """, "FJ0002")]
    [InlineData("""
        [GenerateJson] public partial class Payload
        {
            [System.Text.Json.Serialization.JsonConstructor] public Payload(ref int value) { }
        }
        """, "FJ0002")]
    [InlineData("""
        [GenerateJson] public partial class Payload
        {
            public System.Span<int> Value => default;
        }
        """, "FJ0002")]
    [InlineData("""
        [GenerateJsonFor<Payload>] public partial class Witness;
        public class Payload : System.Collections.Generic.List<int>
        {
            public Payload(string unsupported) { }
        }
        """, "FJ0002")]
    [InlineData("""
        [GenerateJsonFor<Payload>] public partial class Witness;
        [System.Text.Json.Serialization.JsonDerivedType(typeof(Case), "case")]
        public interface Payload;
        public struct Case : Payload;
        """, "FJ0002")]
    [InlineData("""
        [GenerateJsonFor<Payload>] public partial class Witness;
        [System.Text.Json.Serialization.JsonDerivedType(typeof(Case), "case")]
        public interface Payload;
        public class Case : System.Collections.Generic.List<int>, Payload;
        """, "FJ0002")]
    public static void UnsupportedDeclarationsProduceGeneratorDiagnostics(string source, string diagnosticId)
    {
        var result = CompilationHelpers.Run(source);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
        Assert.Empty(result.Driver.GetRunResult().Results.Single().GeneratedSources);
    }

    [Theory]
    [InlineData("""[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]""")]
    [InlineData("[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true)]")]
    [InlineData("[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]")]
    [InlineData("""[JsonDerivedType(typeof(string), "invalid")]""")]
    [InlineData("""[JsonDerivedType(typeof(Derived), "same"), JsonDerivedType(typeof(Other), "same")]""")]
    [InlineData("""[JsonDerivedType(typeof(Derived), "first"), JsonDerivedType(typeof(Derived), "second")]""")]
    public static void UnsupportedHierarchyMetadataIsDiagnosed(string attributes)
    {
        var result = CompilationHelpers.Run($$"""
            using System.Text.Json.Serialization;
            [GenerateJson]
            {{attributes}}
            public partial class Payload;
            public sealed class Derived : Payload;
            public sealed class Other : Payload;
            """);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "FJ0002");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
        Assert.Empty(result.Driver.GetRunResult().Results.Single().GeneratedSources);
    }

    [Theory]
    [InlineData("System.Tuple<int, string>", """{"Item1":42,"Item2":"text"}""")]
    [InlineData("System.Tuple<int, int, int, int, int, int, int, System.Tuple<int, int>>",
        """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9}""")]
    [InlineData("System.Collections.Immutable.ImmutableList<int>", "[1,2,3]")]
    [InlineData("System.Collections.Immutable.ImmutableDictionary<string, int>", """{"key":42}""")]
    [InlineData("System.Collections.Frozen.FrozenSet<int>", "[1,2,3]")]
    [InlineData("System.Collections.Frozen.FrozenDictionary<string, int>", """{"key":42}""")]
    [InlineData("System.Collections.Generic.LinkedList<int>", "[1,2,3]")]
    public static void TupleAndImmutableCollectionRootsRoundtrip(string type, string json) =>
        VerifyRoundtrip($$"""
            [GenerateJsonFor<{{type}}>]
            public partial class Witness;
            """, type, json);

    [Theory]
    [InlineData("List<int>", "List<int> items", "base(items)", "[1,2,3]")]
    [InlineData("List<int>", "HashSet<int> items", "base(items)", "[1,2,3]")]
    [InlineData("List<int>", "IEqualityComparer<int> comparer", "base()", "[1,2,3]")]
    [InlineData("List<int>", "int capacity = 0", "base(capacity)", "[1,2,3]")]
    [InlineData("Dictionary<string, int>", "Dictionary<string, int> items", "base(items)", """{"key":42}""")]
    [InlineData("Dictionary<string, int>", "IEqualityComparer<string> comparer", "base(comparer)", """{"key":42}""")]
    public static void CollectionFactorySignaturesRoundtrip(string baseType, string parameter, string initializer, string json) =>
        VerifyRoundtrip($$"""
            using System.Collections.Generic;
            public class Collection : {{baseType}}
            {
                public Collection({{parameter}}) : {{initializer}} { }
            }
            [GenerateJsonFor<Collection>]
            public partial class Witness;
            """, "Collection", json);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public static void IncludedAndRequiredFieldsRoundtrip(bool required, bool optionalConstructorArgument)
    {
        string attributes = required ? "[JsonInclude, JsonRequired]" : "[JsonInclude]";
        VerifyRoundtrip($$"""
            using System.Text.Json.Serialization;
            public class Payload
            {
                {{attributes}}
                public int Value;

                    {{(optionalConstructorArgument ? "public Payload(int unused = 17) { }" : "")}}
            }
            [GenerateJsonFor<Payload>]
            public partial class Witness;
            """, "Payload", """{"Value":42}""");
    }

    private static void VerifyRoundtrip(string declaration, string type, string json)
    {
        var result = CompilationHelpers.Run(declaration + $$"""

            public static class Consumer
            {
                public static string Roundtrip(string json) =>
                    FastPathJsonSerializer.Serialize<{{type}}, Witness>(
                        FastPathJsonSerializer.Deserialize<{{type}}, Witness>(json));
            }
            """);
        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        using MemoryStream stream = new();
        var emitted = result.Compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        stream.Position = 0;
        AssemblyLoadContext context = new(nameof(GeneratorContractTests), isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(stream);
            var consumer = assembly.GetType("Consumer");
            Assert.NotNull(consumer);
            var roundtrip = consumer.GetMethod("Roundtrip");
            Assert.NotNull(roundtrip);
            Assert.Equal(json, roundtrip.CreateDelegate<Func<string, string>>()(json));
        }
        finally
        {
            context.Unload();
        }
    }
}
