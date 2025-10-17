using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.FSharp.Core;
using PolyType.Abstractions;
using PolyType.Examples.FSharp;
using PolyType.ReflectionProvider;
using PolyType.Tests.FSharp;
using Xunit;
using static PolyType.Tests.JsonTests;

namespace PolyType.Tests;

public abstract class PrettyPrinterFSharpTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(GetValues))]
    public void TestValue<T>(TestCase<T> testCase, string expectedEncoding)
    {
        var shape = providerUnderTest.ResolveShape(testCase);
        var prettyPrinter = PrettyPrinter.create(shape);
        #pragma warning disable CS8620 // Argument nullability mismatch
        string result = PrettyPrinter.print(prettyPrinter, testCase.Value);
        #pragma warning restore CS8620
        Assert.Equal(ReplaceLineEndings(expectedEncoding), result);
    }

    public static IEnumerable<object?[]> GetValues()
    {
        Witness p = new();
        
        // Basic types
        yield return [TestCase.Create(1, p), "1"];
        yield return [TestCase.Create((string?)null, p), "null"];
        yield return [TestCase.Create("str", p), "\"str\""];
        yield return [TestCase.Create(false, p), "false"];
        yield return [TestCase.Create(true, p), "true"];
        yield return [TestCase.Create((int?)null, p), "null"];
        yield return [TestCase.Create((int?)42, p), "42"];
        
        // Collections
        yield return [TestCase.Create((int[])[], p), "[]"];
        yield return [TestCase.Create((int[])[1, 2, 3], p), "[1, 2, 3]"];
        
        // F# option types
        yield return [TestCase.Create(FSharpOption<int>.None, p), "null"];
        yield return [TestCase.Create(FSharpOption<int>.Some(42), p), "42"];
        yield return [TestCase.Create(FSharpValueOption<int>.None, p), "null"];
        yield return [TestCase.Create(FSharpValueOption<int>.Some(42), p), "42"];
    }

    private static string ReplaceLineEndings(string value) => s_newLineRegex.Replace(value, Environment.NewLine);
    private static readonly Regex s_newLineRegex = new("\r?\n", RegexOptions.Compiled);
}

public sealed class PrettyPrinterFSharpTests_Reflection() : PrettyPrinterFSharpTests(ReflectionProviderUnderTest.NoEmit);
public sealed class PrettyPrinterFSharpTests_ReflectionEmit() : PrettyPrinterFSharpTests(ReflectionProviderUnderTest.Emit);
public sealed class PrettyPrinterFSharpTests_SourceGen() : PrettyPrinterFSharpTests(SourceGenProviderUnderTest.Default);
