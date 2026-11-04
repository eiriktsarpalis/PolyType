using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.Loader;
using System.Text.Json;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class PresenceTrackingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(129)]
    public static void RequiredMembersUseTheCorrectBitset(int count)
    {
        VerifyGeneratedReader(CreateMembersSource(count, required: true), count, roundtrip =>
        {
            Dictionary<string, int> values = Enumerable.Range(0, count).ToDictionary(i => $"P{i}", i => i + 1);
            string json = JsonSerializer.Serialize(values);
            Assert.Equal(json, roundtrip(json));

            foreach (int missingIndex in new[] { 0, 63, count - 1 }.Where(i => i >= 0 && i < count).Distinct())
            {
                Dictionary<string, int> incomplete = new(values);
                incomplete.Remove($"P{missingIndex}");
                var exception = Assert.Throws<KeyNotFoundException>(() => roundtrip(JsonSerializer.Serialize(incomplete)));
                Assert.Equal($"Missing required constructor parameter 'P{missingIndex}'.", exception.Message);
            }

            if (count > 0)
            {
                var allMissing = Assert.Throws<KeyNotFoundException>(() => roundtrip("{}"));
                Assert.Equal(MissingRequiredMessage(Enumerable.Range(0, count).Select(i => $"P{i}")), allMissing.Message);

                int[] missingIndices = MissingIndices(count);
                Dictionary<string, int> incomplete = new(values);
                foreach (int index in missingIndices)
                {
                    incomplete.Remove($"P{index}");
                }

                var multipleMissing = Assert.Throws<KeyNotFoundException>(() => roundtrip(JsonSerializer.Serialize(incomplete)));
                Assert.Equal(MissingRequiredMessage(missingIndices.Select(i => $"P{i}")), multipleMissing.Message);

                values["P0"] = 999;
                Assert.Equal(JsonSerializer.Serialize(values), roundtrip(json[..^1] + ",\"P0\":999}"));
            }
        }, expectedRequiredMembers: count);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(65)]
    public static void OptionalMemberPresencePreservesInitializers(int count)
    {
        VerifyGeneratedReader(CreateMembersSource(count, required: false), count, roundtrip =>
        {
            Dictionary<string, int> values = Enumerable.Range(0, count).ToDictionary(i => $"P{i}", _ => -1);
            Assert.Equal(JsonSerializer.Serialize(values), roundtrip("{}"));

            values[$"P{count - 1}"] = 42;
            string json = $"{{\"P{count - 1}\":41,\"P{count - 1}\":42}}";
            Assert.Equal(JsonSerializer.Serialize(values), roundtrip(json));
        });
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(1, 65)]
    [InlineData(7, 60)]
    [InlineData(8, 57)]
    [InlineData(9, 56)]
    [InlineData(63, 2)]
    [InlineData(64, 1)]
    [InlineData(65, 1)]
    [InlineData(66, 2)]
    [InlineData(129, 3)]
    public static void RequiredValidationIgnoresOptionalPresence(int requiredCount, int optionalCount)
    {
        VerifyGeneratedReader(CreateMixedMembersSource(requiredCount, optionalCount), requiredCount + optionalCount, roundtrip =>
        {
            Dictionary<string, int> requiredValues = Enumerable.Range(0, requiredCount).ToDictionary(i => $"R{i}", i => i + 1);
            Dictionary<string, int> values = Enumerable.Range(0, optionalCount).ToDictionary(i => $"O{i}", _ => -1);
            foreach (var entry in requiredValues)
            {
                values.Add(entry.Key, entry.Value);
            }

            Assert.Equal(JsonSerializer.Serialize(values), roundtrip(JsonSerializer.Serialize(requiredValues)));
            foreach (int missingIndex in new[] { 0, 7, 8, 63, 64, requiredCount - 1 }
                .Where(i => i < requiredCount).Distinct())
            {
                Dictionary<string, int> incomplete = new(values);
                incomplete.Remove($"R{missingIndex}");
                var exception = Assert.Throws<KeyNotFoundException>(() => roundtrip(JsonSerializer.Serialize(incomplete)));
                Assert.Equal($"Missing required constructor parameter 'R{missingIndex}'.", exception.Message);
            }

            var allMissing = Assert.Throws<KeyNotFoundException>(() => roundtrip("{}"));
            Assert.Equal(MissingRequiredMessage(Enumerable.Range(0, requiredCount).Select(i => $"R{i}")), allMissing.Message);

            int[] missingIndices = MissingIndices(requiredCount);
            Dictionary<string, int> partial = new(requiredValues);
            foreach (int index in missingIndices)
            {
                partial.Remove($"R{index}");
            }

            var multipleMissing = Assert.Throws<KeyNotFoundException>(() => roundtrip(JsonSerializer.Serialize(partial)));
            Assert.Equal(MissingRequiredMessage(missingIndices.Select(i => $"R{i}")), multipleMissing.Message);
        }, expectedRequiredMembers: requiredCount);
    }

    [Theory]
    [InlineData(65)]
    public static void UntrackedConstructorParametersDoNotConsumeBits(int optionalParameters)
    {
        string parameters = string.Join(", ", Enumerable.Range(0, optionalParameters).Select(i => $"int p{i} = {i}"));
        string assignments = string.Join("\n", Enumerable.Range(0, optionalParameters).Select(i => $"P{i} = p{i};"));
        string properties = string.Join("\n", Enumerable.Range(0, optionalParameters).Select(i => $"public int P{i} {{ get; }}"));
        string source = $$"""
            namespace TestNamespace;

            [GenerateJson]
            public partial class Payload
            {
                public Payload({{parameters}})
                {
                    {{assignments}}
                }
                {{properties}}
                public required int Required { get; set; }
                public required int AlsoRequired { get; set; }
                public int Optional { get; set; } = -1;
            }
            {{ConsumerSource}}
            """;

        VerifyGeneratedReader(source, expectedTrackedMembers: 3, roundtrip =>
        {
            var exception = Assert.Throws<KeyNotFoundException>(() => roundtrip("{}"));
            Assert.Equal("Missing required constructor parameters: 'Required', 'AlsoRequired'.", exception.Message);
            Dictionary<string, int> values = Enumerable.Range(0, optionalParameters).ToDictionary(i => $"P{i}", i => i);
            values["Required"] = 7;
            values["AlsoRequired"] = 8;
            values["Optional"] = -1;
            Assert.Equal(JsonSerializer.Serialize(values), roundtrip("""{"Required":7,"AlsoRequired":8}"""));

            values["Optional"] = 42;
            Assert.Equal(JsonSerializer.Serialize(values), roundtrip("""{"Required":7,"AlsoRequired":8,"Optional":42}"""));
        }, expectedRequiredMembers: 2);
    }

    [Theory]
    [InlineData("""{"id":7,"Name":"value","included":2}""")]
    [InlineData("""{"id":0,"Name":"","included":0}""")]
    public static void JsonMetadataControlsRequiredAndIncludedMembers(string json)
    {
        string source = (string)SnapshotTests.GetTestCases().Single(row => (string)row[0] == "JsonMetadata")[1];
        source += """

            public static class Consumer
            {
                public static string Roundtrip(string json) =>
                    FastPathJsonSerializer.Serialize(FastPathJsonSerializer.Deserialize<Person>(json));
            }
            """;
        VerifyGeneratedReader(source, expectedTrackedMembers: 3, roundtrip =>
        {
            Assert.Equal(json, roundtrip(json));
            var exception = Assert.Throws<KeyNotFoundException>(() => roundtrip("""{"id":7}"""));
            Assert.Equal("Missing required constructor parameter 'Name'.", exception.Message);
            Assert.Throws<KeyNotFoundException>(() => roundtrip("""{"Name":"value"}"""));
            var allMissing = Assert.Throws<KeyNotFoundException>(() => roundtrip("{}"));
            Assert.Equal("Missing required constructor parameters: 'id', 'Name'.", allMissing.Message);
        }, expectedRequiredMembers: 2);
    }

    private static int[] MissingIndices(int count) =>
        new[] { 0, 7, 8, 31, 32, 63, 64, 127, 128, count - 1 }
            .Where(i => i >= 0 && i < count).Distinct().Order().ToArray();

    private static string MissingRequiredMessage(IEnumerable<string> names)
    {
        string[] missing = names.ToArray();

        return missing.Length == 1
            ? $"Missing required constructor parameter '{missing[0]}'."
            : $"Missing required constructor parameters: '{string.Join("', '", missing)}'.";
    }

    internal static string CreateMembersSource(int count, bool required)
    {
        string properties = string.Join("\n", Enumerable.Range(0, count).Select(i => required
            ? $"public required int P{i} {{ get; set; }}"
            : $"public int P{i} {{ get; set; }} = -1;"));
        return $$"""
            namespace TestNamespace;

            [GenerateJson]
            public partial class Payload
            {
                {{properties}}
            }
            {{ConsumerSource}}
            """;
    }

    internal static string CreateMixedMembersSource(int requiredCount, int optionalCount)
    {
        string optional = string.Join("\n", Enumerable.Range(0, optionalCount).Select(i => $"public int O{i} {{ get; set; }} = -1;"));
        string required = string.Join("\n", Enumerable.Range(0, requiredCount).Select(i => $"public required int R{i} {{ get; set; }}"));
        return $$"""
            namespace TestNamespace;

            [GenerateJson]
            public partial class Payload
            {
                {{optional}}
                {{required}}
            }
            {{ConsumerSource}}
            """;
    }

    private const string ConsumerSource = """
        public static class Consumer
        {
            public static string Roundtrip(string json) =>
                FastPathJsonSerializer.Serialize(FastPathJsonSerializer.Deserialize<Payload>(json));
        }
        """;

    private static void VerifyGeneratedReader(string source, int expectedTrackedMembers, Action<Func<string, string>> verify,
        int expectedRequiredMembers = 0)
    {
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));

        SyntaxNode[] roots = result.Driver.GetRunResult().Results.Single().GeneratedSources
            .Select(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken)).ToArray();
        InvocationExpressionSyntax[] requiredChecks = roots.SelectMany(r => r.DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Where(i => i.Expression is IdentifierNameSyntax name &&
                name.Identifier.ValueText == "ThrowMissingRequiredParameters").ToArray();
        Assert.Equal(expectedRequiredMembers > 0 ? 1 : 0, requiredChecks.Length);
        VariableDeclarationSyntax[] tracking = roots.SelectMany(r => r.DescendantNodes().OfType<VariableDeclarationSyntax>())
            .Where(d => d.Variables.Any(v => v.Identifier.ValueText.StartsWith("seen", StringComparison.Ordinal))).ToArray();
        if (expectedTrackedMembers == 0)
        {
            Assert.Empty(tracking);
        }
        else
        {
            VariableDeclarationSyntax declaration = Assert.Single(tracking);
            VariableDeclaratorSyntax variable = Assert.Single(declaration.Variables);
            Assert.Equal("seen", variable.Identifier.ValueText);
            if (expectedTrackedMembers <= 64)
            {
                Assert.Equal("ulong", declaration.Type.ToString());
                foreach (BinaryExpressionSyntax shift in roots.SelectMany(r => r.DescendantNodes().OfType<BinaryExpressionSyntax>())
                    .Where(n => n.IsKind(SyntaxKind.LeftShiftExpression) &&
                        n.Left is LiteralExpressionSyntax { Token.Value: ulong }))
                {
                    LiteralExpressionSyntax bit = Assert.IsType<LiteralExpressionSyntax>(shift.Right);
                    Assert.InRange(Assert.IsType<int>(bit.Token.Value), 0, expectedTrackedMembers - 1);
                }
            }
            else
            {
                ObjectCreationExpressionSyntax creation = Assert.IsType<ObjectCreationExpressionSyntax>(variable.Initializer?.Value);
                Assert.Equal("global::System.Collections.BitArray", creation.Type.ToString());
            }
        }

        using MemoryStream stream = new();
        var emitted = result.Compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        stream.Position = 0;
        AssemblyLoadContext context = new(nameof(PresenceTrackingTests), isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(stream);
            var consumer = assembly.GetType("TestNamespace.Consumer");
            Assert.NotNull(consumer);
            var method = consumer.GetMethod("Roundtrip");
            Assert.NotNull(method);
            verify(method.CreateDelegate<Func<string, string>>());
        }
        finally
        {
            context.Unload();
        }
    }
}
