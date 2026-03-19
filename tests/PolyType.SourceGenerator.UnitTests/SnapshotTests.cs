using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class SnapshotTests
{
    private static readonly string SnapshotsDirectory = GetSnapshotsDirectory();
    private static readonly bool UpdateSnapshots = Environment.GetEnvironmentVariable("POLYTYPE_UPDATE_SNAPSHOTS") is "true" or "1";

    [Fact]
    public static void SimplePoco() => VerifySourceGeneratorOutput("""
        using PolyType;
        using System.Collections.Generic;

        namespace TestNamespace
        {
            [GenerateShape]
            public partial class SimplePoco
            {
                public SimplePoco(int id, string name = "default")
                {
                    Id = id;
                    Name = name;
                }

                public int Id { get; }
                public string Name { get; }
                public bool? IsActive { get; set; }
            }
        }
        """);

    [Fact]
    public static void SimpleRecord() => VerifySourceGeneratorOutput("""
        using PolyType;

        namespace TestNamespace
        {
            [GenerateShape]
            public partial record SimpleRecord(int X, string Y, bool Z);
        }
        """);

    [Fact]
    public static void EnumType() => VerifySourceGeneratorOutput("""
        using PolyType;

        namespace TestNamespace
        {
            [GenerateShape]
            public partial class EnumHolder
            {
                public MyEnum Value { get; set; }
            }

            public enum MyEnum
            {
                None = 0,
                First = 1,
                Second = 2,
                Third = 3,
            }
        }
        """);

    [Fact]
    public static void NullableValueType() => VerifySourceGeneratorOutput("""
        using System;
        using PolyType;

        namespace TestNamespace
        {
            [GenerateShape]
            public partial class NullableHolder
            {
                public int? NullableInt { get; set; }
                public DateTime? NullableDate { get; set; }
            }
        }
        """);

    [Fact]
    public static void ListCollection() => VerifySourceGeneratorOutput("""
        using PolyType;
        using System.Collections.Generic;

        namespace TestNamespace
        {
            [GenerateShape]
            public partial class ListHolder
            {
                public List<int> Items { get; set; } = new();
            }
        }
        """);

    [Fact]
    public static void DictionaryType() => VerifySourceGeneratorOutput("""
        using PolyType;
        using System.Collections.Generic;

        namespace TestNamespace
        {
            [GenerateShape]
            public partial class DictionaryHolder
            {
                public Dictionary<string, int> Lookup { get; set; } = new();
            }
        }
        """);

    [Fact]
    public static void GenericRecord() => VerifySourceGeneratorOutput("""
        using PolyType;

        namespace TestNamespace
        {
            public record Pair<TFirst, TSecond>(TFirst First, TSecond Second);

            [GenerateShapeFor(typeof(Pair<int, string>))]
            public partial class Witness { }
        }
        """);

    [Fact]
    public static void StructType() => VerifySourceGeneratorOutput("""
        using PolyType;

        namespace TestNamespace
        {
            [GenerateShape]
            public partial struct Point
            {
                public double X { get; set; }
                public double Y { get; set; }
            }
        }
        """);

    [Fact]
    public static void MultiTypeProvider() => VerifySourceGeneratorOutput("""
        using PolyType;
        using System.Collections.Generic;

        namespace TestNamespace
        {
            public record Person(string Name, int Age);
            public record Address(string Street, string City);

            [GenerateShapeFor(typeof(Person))]
            [GenerateShapeFor(typeof(Address))]
            [GenerateShapeFor(typeof(List<Person>))]
            public partial class MultiProvider { }
        }
        """);

    [Fact]
    public static void ClassWithInitProperties() => VerifySourceGeneratorOutput("""
        using PolyType;

        namespace TestNamespace
        {
            [GenerateShape]
            public partial class Config
            {
                public string Host { get; init; } = "";
                public int Port { get; init; }
                public string? ConnectionString { get; init; }
            }
        }
        """);

    [Fact]
    public static void PrivateMembers() => VerifySourceGeneratorOutput("""
        using PolyType;

        namespace TestNamespace
        {
            [GenerateShape]
            public partial class Secrets
            {
                [ConstructorShape]
                private Secrets(string token, int retryCount)
                {
                    Token = token;
                    RetryCount = retryCount;
                }

                public Secrets() { }

                [PropertyShape]
                private string Token { get; set; } = "";

                [PropertyShape]
                private int RetryCount;

                public string Label { get; set; } = "";
            }
        }
        """);

    [Fact]
    public static void RefParameters() => VerifySourceGeneratorOutput("""
        using PolyType;

        namespace TestNamespace
        {
            [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
            public partial class RefParamDemo
            {
                public RefParamDemo(ref int id, in string name, out bool valid)
                {
                    Id = id;
                    Name = name;
                    valid = true;
                }

                public int Id { get; }
                public string Name { get; }

                public void Update(ref int value, in string label) { value = 0; }
                public bool TryGet(string key, out int value) { value = 0; return false; }
            }
        }
        """);

    private static void VerifySourceGeneratorOutput([StringSyntax("c#-test")] string source, [CallerMemberName] string testCaseName = "")
    {
        Compilation compilation = CompilationHelpers.CreateCompilation(source);
        CSharpGeneratorDriver driver = CompilationHelpers.CreatePolyTypeSourceGeneratorDriver(compilation);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outCompilation, out var diagnostics, TestContext.Current.CancellationToken);

        diagnostics.AssertMaxSeverity(DiagnosticSeverity.Info);
        outCompilation.GetDiagnostics(TestContext.Current.CancellationToken).AssertMaxSeverity(DiagnosticSeverity.Info);

        var generatedTrees = outCompilation.SyntaxTrees
            .Where(tree => tree.FilePath.Contains(PolyTypeGenerator.SourceGeneratorName))
            .OrderBy(tree => tree.FilePath, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(generatedTrees);

        string caseDirectory = Path.Combine(SnapshotsDirectory, $"{GetTargetFramework()}-{GetConfiguration()}", testCaseName);

        if (UpdateSnapshots)
        {
            UpdateSnapshotFiles(caseDirectory, generatedTrees);
            return;
        }

        AssertSnapshotsMatch(caseDirectory, generatedTrees, testCaseName);
    }

    private static void UpdateSnapshotFiles(string caseDirectory, SyntaxTree[] generatedTrees)
    {
        if (Directory.Exists(caseDirectory))
        {
            Directory.Delete(caseDirectory, recursive: true);
        }

        Directory.CreateDirectory(caseDirectory);

        foreach (SyntaxTree tree in generatedTrees)
        {
            string fileName = Path.GetFileName(tree.FilePath) + ".txt";
            string content = NormalizeGeneratedSource(tree.GetText().ToString());
            // Write with system-default line endings so git autocrlf doesn't conflict.
            content = content.Replace("\n", Environment.NewLine);
            File.WriteAllText(Path.Combine(caseDirectory, fileName), content);
        }
    }

    private static void AssertSnapshotsMatch(string caseDirectory, SyntaxTree[] generatedTrees, string testCaseName)
    {
        Assert.True(
            Directory.Exists(caseDirectory),
            $"Snapshot directory '{caseDirectory}' not found for test case '{testCaseName}'. " +
            "Run 'dotnet msbuild -t:UpdateSnapshots' to generate baseline files.");

        string[] expectedFiles = Directory.GetFiles(caseDirectory, "*.g.cs.txt")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        string[] actualFileNames = generatedTrees
            .Select(t => Path.GetFileName(t.FilePath) + ".txt")
            .ToArray();

        string[] expectedFileNames = expectedFiles
            .Select(f => Path.GetFileName(f)!)
            .ToArray();

        Assert.Equal(expectedFileNames, actualFileNames);

        for (int i = 0; i < generatedTrees.Length; i++)
        {
            string actualContent = NormalizeGeneratedSource(generatedTrees[i].GetText().ToString());
            string expectedContent = NormalizeGeneratedSource(File.ReadAllText(expectedFiles[i]));
            string fileName = Path.GetFileName(expectedFiles[i]);

            if (!string.Equals(expectedContent, actualContent, StringComparison.Ordinal))
            {
                int diffPos = FindFirstDifference(expectedContent, actualContent);
                int contextStart = Math.Max(0, diffPos - 50);
                string expectedContext = diffPos < expectedContent.Length
                    ? Escape(expectedContent[contextStart..Math.Min(diffPos + 50, expectedContent.Length)])
                    : "<past end>";
                string actualContext = diffPos < actualContent.Length
                    ? Escape(actualContent[contextStart..Math.Min(diffPos + 50, actualContent.Length)])
                    : "<past end>";

                Assert.Fail(
                    $"""
                    Snapshot mismatch in {testCaseName}/{fileName}.
                    Run 'dotnet msbuild -t:UpdateSnapshots' to update baselines.

                    First difference at position {diffPos} (expected length: {expectedContent.Length}, actual: {actualContent.Length})
                    Expected: [{expectedContext}]
                    Actual:   [{actualContext}]
                    """);
            }
        }
    }

    /// <summary>
    /// Normalizes generated source by replacing the volatile source generator version string
    /// with a stable placeholder, normalizing line endings, and sorting switch case blocks
    /// whose order is non-deterministic across source generator invocations.
    /// </summary>
    private static string NormalizeGeneratedSource(string source)
    {
        source = source.Replace("\r\n", "\n");
        source = Regex.Replace(
            source,
            @"\[global::System\.CodeDom\.Compiler\.GeneratedCodeAttribute\(""[^""]*"",\s*""[^""]*""\)\]",
            """[global::System.CodeDom.Compiler.GeneratedCodeAttribute("PolyType.SourceGenerator.PolyTypeGenerator", "0.0.0.0")]""");
        source = SortSwitchCaseBlocks(source);

        return source;
    }

    /// <summary>
    /// Sorts contiguous runs of "case ..." blocks within switch statements to produce
    /// a deterministic ordering. The source generator iterates over dictionary entries
    /// whose order can vary between runs; sorting by case label neutralizes this.
    /// </summary>
    private static string SortSwitchCaseBlocks(string source)
    {
        string[] lines = source.Split('\n');
        List<string> result = [];
        List<(string label, List<string> lines)> caseBlocks = [];
        List<string>? currentBlock = null;
        string? currentLabel = null;
        int braceDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();

            if (currentBlock is null && trimmed.StartsWith("case \"", StringComparison.Ordinal))
            {
                currentLabel = trimmed;
                currentBlock = [lines[i]];
                braceDepth = 0;
            }
            else if (currentBlock is not null)
            {
                currentBlock.Add(lines[i]);
                braceDepth += CountChar(lines[i], '{') - CountChar(lines[i], '}');

                if (braceDepth <= 0 && trimmed.StartsWith("}", StringComparison.Ordinal))
                {
                    caseBlocks.Add((currentLabel!, currentBlock));
                    currentBlock = null;
                    currentLabel = null;
                }
            }
            else
            {
                if (caseBlocks.Count > 1)
                {
                    foreach (var (_, blockLines) in caseBlocks.OrderBy(b => b.label, StringComparer.Ordinal))
                    {
                        result.AddRange(blockLines);
                    }
                }
                else if (caseBlocks.Count == 1)
                {
                    result.AddRange(caseBlocks[0].lines);
                }

                caseBlocks.Clear();
                result.Add(lines[i]);
            }
        }

        if (caseBlocks.Count > 0)
        {
            foreach (var (_, blockLines) in caseBlocks.OrderBy(b => b.label, StringComparer.Ordinal))
            {
                result.AddRange(blockLines);
            }
        }

        return string.Join("\n", result);

        static int CountChar(string s, char c)
        {
            int count = 0;
            foreach (char ch in s)
            {
                if (ch == c)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private static int FindFirstDifference(string a, string b)
    {
        int minLen = Math.Min(a.Length, b.Length);
        for (int i = 0; i < minLen; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return minLen;
    }

    private static string Escape(string s) =>
        s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

    private static string GetTargetFramework() =>
        typeof(SnapshotTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "TargetFramework")
            .Value!;

    private static string GetConfiguration() =>
        typeof(SnapshotTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "Configuration")
            .Value!;

    private static string GetSnapshotsDirectory()
    {
        string assemblyDir = Path.GetDirectoryName(typeof(SnapshotTests).Assembly.Location)!;
        string? dir = assemblyDir;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "PolyType.SourceGenerator.UnitTests.csproj")))
            {
                return Path.Combine(dir, "Snapshots");
            }

            dir = Path.GetDirectoryName(dir);
        }

        return Path.Combine(assemblyDir, "..", "..", "..", "..", "Snapshots");
    }
}
