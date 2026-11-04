using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class SnapshotTests
{
    private static readonly string SnapshotsDirectory = GetSnapshotsDirectory();
    private static readonly bool UpdateSnapshots = Environment.GetEnvironmentVariable("POLYTYPE_UPDATE_SNAPSHOTS") is "true" or "1";
    private const string UpdateCommand = @"dotnet msbuild tests\PolyType.FastPathJson.Tests\PolyType.FastPathJson.Tests.csproj -t:UpdateSnapshots";

    public static IEnumerable<object[]> GetTestCases()
    {
        yield return ["ScalarWitness", """
            namespace TestNamespace;

            [GenerateJsonFor<int>]
            [GenerateJsonFor<string>]
            [GenerateJsonFor<byte[]>]
            public partial class Witness;
            """];
        yield return ["SimplePoco", """
            namespace TestNamespace;

            [GenerateJson]
            public partial class Person
            {
                public Person(int id, string name = "default") => (Id, Name) = (id, name);
                public int Id { get; }
                public string Name { get; }
                public bool? IsActive { get; set; }
            }
            """];
        yield return ["SimpleRecord", """
            namespace TestNamespace;

            [GenerateJson]
            public partial record Person(int Id, string Name, bool IsActive);
            """];
        yield return ["EscapedPropertyNameDiscriminators", """
            using System.Text.Json.Serialization;
            namespace TestNamespace;

            [GenerateJson]
            public partial record Payload(
                [property: JsonPropertyName("'a")] int Quote,
                [property: JsonPropertyName("\\a")] int Backslash,
                [property: JsonPropertyName("\na")] int Newline,
                [property: JsonPropertyName("\0a")] int Null,
                [property: JsonPropertyName("\u00E9")] int Unicode);
            """];
        foreach (int nameLength in new[] { 42, 43 })
        {
            yield return [$"PropertyNameBuffer{nameLength}", $$"""
                namespace TestNamespace;

                [GenerateJson]
                public partial class Payload
                {
                    [System.Text.Json.Serialization.JsonPropertyName("{{new string('\u00E9', nameLength)}}")]
                    public int Value { get; set; }
                }
                """];
        }

        yield return ["JsonMetadata", """
            using System;
            using System.Text.Json.Serialization;
            namespace TestNamespace;

            [GenerateJson]
            public partial class Person
            {
                public Person() => throw new InvalidOperationException();
                [JsonConstructor]
                public Person(int id) => Id = id;

                [JsonPropertyName("id"), JsonPropertyOrder(-1)]
                public int Id { get; }
                [JsonRequired]
                public string Name { get; set; } = "";
                [JsonInclude, JsonPropertyName("included")]
                internal int Included { get; set; }
                [JsonIgnore]
                public Action? Notification { get; set; }
            }
            """];
        yield return ["StructType", """
            namespace TestNamespace;

            [GenerateJson]
            public partial struct Point
            {
                public double X { get; set; }
                public double Y { get; set; }
            }
            """];
        yield return ["FusedScalarProperties", """
            using System;
            namespace TestNamespace;

            [GenerateJson]
            public partial record Payload(
                bool Flag, int Number, double Ratio, decimal Amount, string? Text,
                Guid Id, DateTime Date, DateTimeOffset Timestamp, Uri? Link,
                char Character, byte[]? Bytes, int? Optional);
            """];
        yield return ["KeywordMembers", """
            namespace TestNamespace;

            [GenerateJson]
            public partial class Payload
            {
                public int Ordered { get; set; }
                public int @class { get; set; }
                public int @event;
                public required int @required { get; init; }
            }
            """];
        yield return ["EnumType", """
            namespace TestNamespace;

            [GenerateJson]
            public partial record StatusHolder(Status Value);
            public enum Status { None, Active, Complete }
            """];
        yield return ["NullableValueType", """
            namespace TestNamespace;

            [GenerateJson]
            public partial class NullableHolder
            {
                public int? Number { get; set; }
                public System.DateTime? Date { get; set; }
            }
            """];
        yield return ["RequiredProperties64", PresenceTrackingTests.CreateMembersSource(64, required: true)];
        yield return ["RequiredProperties65", PresenceTrackingTests.CreateMembersSource(65, required: true)];
        yield return ["MixedRequiredMembers", PresenceTrackingTests.CreateMixedMembersSource(requiredCount: 65, optionalCount: 1)];
        yield return ["ListCollection", """
            using System.Collections.Generic;
            namespace TestNamespace;

            [GenerateJson]
            public partial class ListHolder
            {
                public List<int>? Items { get; set; }
                public int[]? Values { get; set; }
            }
            """];
        yield return ["BuiltInCollectionRoots", """
            using System.Collections.Generic;
            namespace TestNamespace;

            [GenerateJsonFor<int[]>]
            [GenerateJsonFor<List<int>>]
            [GenerateJsonFor<Dictionary<string, int>>]
            public partial class Witness;
            """];
        yield return ["TransitiveCollections", """
            using System.Collections.Generic;
            namespace TestNamespace;

            public record Entry(
                string[]? Tags, List<Dictionary<string, int[]>>? Batches, byte[]? Bytes, Entry[]? Children);

            [GenerateJsonFor<Entry>]
            [GenerateJsonFor<string>]
            public readonly partial struct Witness;
            """];
        yield return ["CustomCollection", """
            using System;
            using System.Collections.Generic;
            namespace TestNamespace;

            public class Numbers : List<int>
            {
                public Numbers(ReadOnlySpan<int> values) : base(values.ToArray()) { }
            }

            [GenerateJsonFor(typeof(Numbers))]
            public partial class Witness;
            """];
        yield return ["DictionaryType", """
            using System.Collections.Generic;
            namespace TestNamespace;

            [GenerateJson]
            public partial class DictionaryHolder
            {
                public Dictionary<string, int>? Lookup { get; set; }
                public SortedList<int, string>? Ordered { get; set; }
            }
            """];
        yield return ["TupleType", """
            namespace TestNamespace;

            [GenerateJsonFor(typeof((int, string)))]
            [GenerateJsonFor(typeof((int, int, int, int, int, int, int, int, int)))]
            public partial class Witness;
            """];
        yield return ["MultiDimensionalArray", """
            namespace TestNamespace;

            [GenerateJsonFor(typeof(int[,]))]
            [GenerateJsonFor(typeof(int[,,]))]
            public partial class Witness;
            """];
        yield return ["RecursiveObject", """
            using System.Collections.Generic;
            namespace TestNamespace;

            [GenerateJson]
            public partial class Node
            {
                public int Value { get; set; }
                public Node? Next { get; set; }
                public List<Node>? Children { get; set; }
            }
            """];
        yield return ["TypeHierarchy", """
            using System.Text.Json.Serialization;
            namespace TestNamespace;

            [GenerateJson]
            [JsonDerivedType(typeof(Leaf), "leaf")]
            [JsonDerivedType(typeof(Branch), "branch")]
            public abstract partial record Tree;
            public sealed record Leaf(int Value) : Tree;
            public sealed record Branch(Tree Left, Tree Right) : Tree;
            """];
        yield return ["FoldedWitness", """
            using System.Collections.Generic;
            namespace TestNamespace;

            public record Person(string Name, int Age);

            [GenerateJsonFor<Dictionary<string, (Person, double[])>>]
            [GenerateJsonFor<List<List<List<int>>>>]
            public partial class Witness;
            """];
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public static void SourceGeneratorOutputMatchesBaseline(string testCaseName, string source)
    {
        var result = CompilationHelpers.Run(source);
        Assert.DoesNotContain(result.Input.ReferencedAssemblyNames, assembly =>
            assembly.Name is "PolyType" or "PolyType.SourceGenerator");
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));

        var generatedSources = result.Driver.GetRunResult().Results.Single().GeneratedSources;
        foreach (GeneratedSourceResult generated in generatedSources)
        {
            SyntaxNode root = generated.SyntaxTree.GetRoot(TestContext.Current.CancellationToken);
            BaseTypeSyntax[] contracts = root.DescendantNodes().OfType<BaseListSyntax>()
                .SelectMany(list => list.Types)
                .Where(type => type.Type is QualifiedNameSyntax
                {
                    Right: GenericNameSyntax { Identifier.ValueText: "IJsonSerializable" },
                })
                .ToArray();
            Assert.InRange(contracts.Length, 0, 1);

            ClassDeclarationSyntax[] containers = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(c => c.Identifier.ValueText == "GeneratedJsonConverters").ToArray();
            Assert.All(containers, container =>
                Assert.Contains(container.Modifiers, modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));

            ClassDeclarationSyntax[] converters = containers.SelectMany(c => c.Members.OfType<ClassDeclarationSyntax>()).ToArray();
            Assert.InRange(converters.Length, 0, 1);
            if (converters.Length == 1)
            {
                Assert.Equal($"FastPathJson.Converters.{converters[0].Identifier.ValueText}.g.cs", generated.HintName);
            }
        }

        SyntaxTree[] generatedTrees = generatedSources
            .Select(s => s.SyntaxTree)
            .OrderBy(t => t.FilePath, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(generatedTrees);
        foreach (SyntaxTree tree in generatedTrees)
        {
            AssertThrowsAreOutlined(tree);
            AssertControlFlowUsesBraces(tree);
            AssertMembersSeparatedByBlankLines(tree);
        }

        string caseDirectory = Path.Combine(SnapshotsDirectory, $"{GetMetadata("TargetFramework")}-{GetMetadata("Configuration")}", testCaseName);

        if (UpdateSnapshots)
        {
            UpdateSnapshotFiles(caseDirectory, generatedTrees);
            return;
        }

        AssertSnapshotsMatch(caseDirectory, generatedTrees, testCaseName);
    }

    private static void AssertThrowsAreOutlined(SyntaxTree tree)
    {
        foreach (SyntaxNode node in tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes()
            .Where(n => n is ThrowStatementSyntax or ThrowExpressionSyntax))
        {
            SyntaxNode? function = node.Ancestors().FirstOrDefault(n => n is MethodDeclarationSyntax or LocalFunctionStatementSyntax);
            Assert.NotNull(function);
            var (identifier, modifiers, attributes) = function switch
            {
                MethodDeclarationSyntax method => (method.Identifier, method.Modifiers, method.AttributeLists),
                LocalFunctionStatementSyntax local => (local.Identifier, local.Modifiers, local.AttributeLists),
                _ => throw new InvalidOperationException(),
            };
            Assert.StartsWith("Throw", identifier.ValueText, StringComparison.Ordinal);
            Assert.Contains(modifiers, modifier => modifier.IsKind(SyntaxKind.StaticKeyword));
            Assert.Contains("MethodImplOptions.NoInlining", attributes.ToString(), StringComparison.Ordinal);
        }
    }

    private static void AssertControlFlowUsesBraces(SyntaxTree tree)
    {
        foreach (SyntaxNode node in tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes())
        {
            StatementSyntax? body = node switch
            {
                IfStatementSyntax statement => statement.Statement,
                ElseClauseSyntax clause => clause.Statement,
                ForStatementSyntax statement => statement.Statement,
                CommonForEachStatementSyntax statement => statement.Statement,
                WhileStatementSyntax statement => statement.Statement,
                DoStatementSyntax statement => statement.Statement,
                _ => null,
            };
            if (body is not null)
            {
                Assert.IsType<BlockSyntax>(body);
            }
        }
    }

    private static void AssertMembersSeparatedByBlankLines(SyntaxTree tree)
    {
        string source = tree.GetText(TestContext.Current.CancellationToken).ToString();
        foreach (TypeDeclarationSyntax type in tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            for (int i = 1; i < type.Members.Count; i++)
            {
                string separator = source[type.Members[i - 1].Span.End..type.Members[i].SpanStart];
                Assert.Matches(@"\r?\n[ \t]*\r?\n", separator);
            }
        }
    }

    private static void UpdateSnapshotFiles(string caseDirectory, SyntaxTree[] generatedTrees)
    {
        Directory.CreateDirectory(caseDirectory);
        foreach (string file in Directory.GetFiles(caseDirectory, "*.g.cs.txt"))
        {
            File.Delete(file);
        }

        foreach (SyntaxTree tree in generatedTrees)
        {
            string fileName = Path.GetFileName(tree.FilePath) + ".txt";
            string content = NormalizeGeneratedSource(tree.GetText(TestContext.Current.CancellationToken).ToString())
                .Replace("\n", Environment.NewLine);
            File.WriteAllText(Path.Combine(caseDirectory, fileName), content);
        }
    }

    private static void AssertSnapshotsMatch(string caseDirectory, SyntaxTree[] generatedTrees, string testCaseName)
    {
        Assert.True(Directory.Exists(caseDirectory),
            $"Snapshot directory '{caseDirectory}' not found for test case '{testCaseName}'. Run '{UpdateCommand}' to generate baseline files.");
        string[] expectedFiles = Directory.GetFiles(caseDirectory, "*.g.cs.txt").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedFiles.Select(Path.GetFileName),
            generatedTrees.Select(t => Path.GetFileName(t.FilePath) + ".txt"));

        for (int i = 0; i < generatedTrees.Length; i++)
        {
            string actual = NormalizeGeneratedSource(generatedTrees[i].GetText(TestContext.Current.CancellationToken).ToString());
            string expected = NormalizeGeneratedSource(File.ReadAllText(expectedFiles[i]));
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                continue;
            }

            int diffPos = FindFirstDifference(expected, actual);
            int contextStart = Math.Max(0, diffPos - 50);
            string expectedContext = diffPos < expected.Length
                ? Escape(expected[contextStart..Math.Min(diffPos + 50, expected.Length)])
                : "<past end>";
            string actualContext = diffPos < actual.Length
                ? Escape(actual[contextStart..Math.Min(diffPos + 50, actual.Length)])
                : "<past end>";
            Assert.Fail($"""
                Snapshot mismatch in {testCaseName}/{Path.GetFileName(expectedFiles[i])}.
                Run '{UpdateCommand}' to update baselines.

                First difference at position {diffPos} (expected length: {expected.Length}, actual: {actual.Length})
                Expected: [{expectedContext}]
                Actual:   [{actualContext}]
                """);
        }
    }

    private static string NormalizeGeneratedSource(string source) => source.Replace("\r\n", "\n");

    private static int FindFirstDifference(string expected, string actual)
    {
        int length = Math.Min(expected.Length, actual.Length);
        for (int i = 0; i < length; i++)
        {
            if (expected[i] != actual[i])
            {
                return i;
            }
        }

        return length;
    }

    private static string Escape(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

    private static string GetMetadata(string key) => typeof(SnapshotTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value!;

    private static string GetSnapshotsDirectory()
    {
        string assemblyDirectory = Path.GetDirectoryName(typeof(SnapshotTests).Assembly.Location)!;
        for (string? directory = assemblyDirectory; directory is not null; directory = Path.GetDirectoryName(directory))
        {
            if (File.Exists(Path.Combine(directory, "PolyType.FastPathJson.Tests.csproj")))
            {
                return Path.Combine(directory, "Snapshots");
            }
        }

        return Path.Combine(assemblyDirectory, "..", "..", "..", "..", "Snapshots");
    }
}
