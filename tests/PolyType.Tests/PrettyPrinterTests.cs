using System.Collections.Immutable;
using System.Text.RegularExpressions;
using PolyType.Abstractions;
using PolyType.Examples.PrettyPrinter;
using PolyType.ReflectionProvider;
using Xunit;
using static PolyType.Tests.JsonTests;

namespace PolyType.Tests;

public abstract class PrettyPrinterTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(GetValues))]
    public void TestValue<T>(TestCase<T> testCase, string expectedEncoding)
    {
        PrettyPrinter<T> prettyPrinter = GetPrettyPrinterUnderTest(testCase);
        Assert.Equal(ReplaceLineEndings(expectedEncoding), prettyPrinter.Print(testCase.Value));
    }

    public static IEnumerable<object?[]> GetValues()
    {
        Witness p = new();
        yield return [TestCase.Create(1, p), "1"];
        yield return [TestCase.Create((string?)null, p), "null"];
        yield return [TestCase.Create("str", p), "\"str\""];
        yield return [TestCase.Create(false, p), "false"];
        yield return [TestCase.Create(true, p), "true"];
        yield return [TestCase.Create((int?)null, p), "null"];
        yield return [TestCase.Create((int?)42, p), "42"];
        yield return [TestCase.Create(MyEnum.A, p), "MyEnum.A"];
        yield return [TestCase.Create((int[])[], p), "[]"];
        yield return [TestCase.Create((int[])[1, 2, 3], p), "[1, 2, 3]"];
        yield return [TestCase.Create((int[][])[[1, 0, 0], [0, 1, 0], [0, 0, 1]], p),
            """
            [
              [1, 0, 0],
              [0, 1, 0],
              [0, 0, 1]
            ]
            """];
        
        yield return [TestCase.Create(new object(), p), "new Object()"];
        yield return [TestCase.Create(new SimpleRecord(42)),
            """
            new SimpleRecord
            {
              value = 42
            }
            """];
        yield return [TestCase.Create(new DerivedClass { X = 1, Y = 2 }),
            """
            new DerivedClass
            {
              Y = 2,
              X = 1
            }
            """];
        yield return [TestCase.Create(new Dictionary<string, string>(), p), "new Dictionary<String, String>()"];
        yield return [TestCase.Create(
            new Dictionary<string, string> { ["key"] = "value" }, p),
            """
            new Dictionary<String, String>
            {
              ["key"] = "value"
            }
            """];
        
        yield return [TestCase.Create(ImmutableArray.Create(1,2,3), p), """[1, 2, 3]"""];
        yield return [TestCase.Create(ImmutableList.Create("1", "2", "3"), p), """["1", "2", "3"]"""];
        yield return [TestCase.Create(ImmutableQueue.Create(1, 2, 3), p), """[1, 2, 3]"""];
        yield return [TestCase.Create(
            ImmutableDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), p),
            """
            new ImmutableDictionary<String, String>
            {
              ["key"] = "value"
            }
            """];

        yield return [TestCase.Create(
            ImmutableSortedDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), p),
            """
            new ImmutableSortedDictionary<String, String>
            {
              ["key"] = "value"
            }
            """];

        yield return [TestCase.Create<PolymorphicClass>(new PolymorphicClass(42)),
            """
            new PolymorphicClass
            {
              Int = 42
            }
            """];

        yield return [TestCase.Create<PolymorphicClass>(new PolymorphicClass.DerivedClass(42, "str")),
            """
            new DerivedClass
            {
              String = "str",
              Int = 42
            }
            """];

        yield return [TestCase.Create<IPolymorphicInterface>(new IPolymorphicInterface.IDerived1.Impl { X = 1, Y = 2 }),
            """
            new IDerived1
            {
              Y = 2,
              X = 1
            }
            """];
    }

    protected PrettyPrinter<T> GetPrettyPrinterUnderTest<T>(TestCase<T> testCase) =>
        PrettyPrinter.Create(providerUnderTest.ResolveShape(testCase));

    private static string ReplaceLineEndings(string value) => s_newLineRegex.Replace(value, Environment.NewLine);
    private static readonly Regex s_newLineRegex = new("\r?\n", RegexOptions.Compiled);
}

public sealed class PrettyPrinterTests_Reflection() : PrettyPrinterTests(ReflectionProviderUnderTest.NoEmit);
public sealed class PrettyPrinterTests_ReflectionEmit() : PrettyPrinterTests(ReflectionProviderUnderTest.Emit);
public sealed class PrettyPrinterTests_SourceGen() : PrettyPrinterTests(SourceGenProviderUnderTest.Default);