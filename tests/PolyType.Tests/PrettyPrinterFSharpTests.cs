using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
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
        Assert.Same(prettyPrinter, PrettyPrinter.createFromProvider<T>(providerUnderTest.Provider));

        string result = PrettyPrinter.print(prettyPrinter, testCase.Value!);
        Assert.Equal(ReplaceLineEndings(expectedEncoding), result);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void Print_UsesInvariantCulture(string cultureName)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            Witness provider = new();
            TestValue(TestCase.Create(-1234.5f, provider), "-1234.5");
            TestValue(TestCase.Create(-1234.5d, provider), "-1234.5");
            TestValue(TestCase.Create(-1234.5m, provider), "-1234.5");
            TestValue(TestCase.Create(new DateTime(2024, 1, 2, 3, 4, 5), provider), "\"01/02/2024 03:04:05\"");
            TestValue(TestCase.Create(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)), provider),
                "\"01/02/2024 03:04:05 +02:00\"");
            Assert.Equal(cultureName, CultureInfo.CurrentCulture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    public static IEnumerable<object?[]> GetValues()
    {
        Witness p = new();
        
        // Basic types
        yield return [TestCase.Create(1, p), "1"];
        yield return [TestCase.Create(byte.MaxValue, p), "255"];
        yield return [TestCase.Create(ushort.MaxValue, p), "65535"];
        yield return [TestCase.Create(uint.MaxValue, p), "4294967295"];
        yield return [TestCase.Create(ulong.MaxValue, p), "18446744073709551615"];
        yield return [TestCase.Create(sbyte.MinValue, p), "-128"];
        yield return [TestCase.Create(short.MinValue, p), "-32768"];
        yield return [TestCase.Create(long.MinValue, p), "-9223372036854775808"];
        yield return [TestCase.Create(1.5f, p), "1.5"];
        yield return [TestCase.Create(-2.75d, p), "-2.75"];
        yield return [TestCase.Create(3.5m, p), "3.5"];
        yield return [TestCase.Create(new BigInteger(ulong.MaxValue), p), "18446744073709551615"];
        yield return [TestCase.Create('x', p), "'x'"];
        yield return [TestCase.Create(new DateTime(2024, 1, 2, 3, 4, 5), p), "\"01/02/2024 03:04:05\""];
        yield return [TestCase.Create(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)), p), "\"01/02/2024 03:04:05 +02:00\""];
        yield return [TestCase.Create(TimeSpan.FromSeconds(90), p), "\"00:01:30\""];
        yield return [TestCase.Create(Guid.Empty, p), "\"00000000-0000-0000-0000-000000000000\""];
        yield return [TestCase.Create((string?)null, p), "null"];
        yield return [TestCase.Create("str", p), "\"str\""];
        yield return [TestCase.Create(false, p), "false"];
        yield return [TestCase.Create(true, p), "true"];
        yield return [TestCase.Create((int?)null, p), "null"];
        yield return [TestCase.Create((int?)42, p), "42"];
        
        // Collections
        yield return [TestCase.Create((int[]?)null, p), "null"];
        yield return [TestCase.Create((int[])[], p), "[]"];
        yield return [TestCase.Create((int[])[1, 2, 3], p), "[1, 2, 3]"];
        yield return [TestCase.Create((int[][])[[1, 2], [], [3]], p),
            """
            [
              [1, 2],
              [],
              [3]
            ]
            """];
        yield return [TestCase.Create((int[][])[], p),
            """
            [
            ]
            """];
        yield return [TestCase.Create((Dictionary<string, int>?)null, p), "null"];
        yield return [TestCase.Create(new Dictionary<string, int>(), p), "new Dictionary<String, Int32>()"];
        yield return [TestCase.Create(new Dictionary<string, int> { ["first"] = 1, ["second"] = 2 }, p),
            """
            new Dictionary<String, Int32>
            {
              ["first"] = 1,
              ["second"] = 2
            }
            """];
        yield return [TestCase.Create(ImmutableDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), p),
            """
            new ImmutableDictionary<String, String>
            {
              ["key"] = "value"
            }
            """];
        
        // F# option types
        yield return [TestCase.Create(FSharpOption<int>.None, p), "null"];
        yield return [TestCase.Create(FSharpOption<int>.Some(42), p), "42"];
        yield return [TestCase.Create(FSharpValueOption<int>.None, p), "null"];
        yield return [TestCase.Create(FSharpValueOption<int>.Some(42), p), "42"];
        
        // POCOs
        yield return [TestCase.Create(new object(), p), "new Object()"];
        yield return [TestCase.Create(new SimplePoco { Value = 42 }), 
            """
            new SimplePoco
            {
              Value = 42
            }
            """];
        
        yield return [TestCase.Create(new BaseClass { X = 1 }), 
            """
            new BaseClass
            {
              X = 1
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
        
        // Records
        yield return [TestCase.Create(new SimpleRecord(42)), 
            """
            new SimpleRecord
            {
              value = 42
            }
            """];
        
        yield return [TestCase.Create(new GenericRecord<int>(42), p), 
            """
            new GenericRecord<Int32>
            {
              value = 42
            }
            """];
        
        yield return [TestCase.Create(new GenericRecord<string>("str"), p), 
            """
            new GenericRecord<String>
            {
              value = "str"
            }
            """];

        yield return [TestCase.Create(new TypeWithStringSurrogate("text")), "\"text\""];
        yield return [TestCase.Create(new TypeWithRecordSurrogate(42, "text")),
            """
            new Surrogate
            {
              Value1 = 42,
              Value2 = "text"
            }
            """];
        yield return [TestCase.Create<PolymorphicClass>(new PolymorphicClass(42), isUnion: true),
            """
            new PolymorphicClass
            {
              Int = 42
            }
            """];
        yield return [TestCase.Create<PolymorphicClass>(new PolymorphicClass.DerivedClass(42, "text"), isUnion: true),
            """
            new DerivedClass
            {
              String = "text",
              Int = 42
            }
            """];
        yield return [TestCase.Create(FSharpUnion.NewC(42), p, isUnion: true),
            """
            new C
            {
              foo = 42
            }
            """];
        yield return [TestCase.Create(new CSharpScalarUnion(42), isUnion: true), "42"];
        yield return [TestCase.Create(new CSharpScalarUnion("text"), isUnion: true), "\"text\""];
        yield return [TestCase.Create(default(CSharpScalarUnion), isUnion: true), "null"];
        yield return [TestCase.Create((CSharpClassUnion?)null, isUnion: true), "null"];
        
        // Recursive types
        yield return [TestCase.Create(new MyLinkedList<int>
        {
            Value = 1,
            Next = new()
            {
                Value = 2,
                Next = new()
                {
                    Value = 3,
                    Next = null,
                }
            }
        }, p),
            """
            new MyLinkedList<Int32>
            {
              Value = 1,
              Next = new MyLinkedList<Int32>
              {
                Value = 2,
                Next = new MyLinkedList<Int32>
                {
                  Value = 3,
                  Next = null
                }
              }
            }
            """];
    }

    private static string ReplaceLineEndings(string value) => s_newLineRegex.Replace(value, Environment.NewLine);
    private static readonly Regex s_newLineRegex = new("\r?\n", RegexOptions.Compiled);
}

public sealed class PrettyPrinterFSharpTests_Reflection() : PrettyPrinterFSharpTests(ReflectionProviderUnderTest.NoEmit);
public sealed class PrettyPrinterFSharpTests_ReflectionEmit() : PrettyPrinterFSharpTests(ReflectionProviderUnderTest.Emit);
public sealed class PrettyPrinterFSharpTests_SourceGen() : PrettyPrinterFSharpTests(SourceGenProviderUnderTest.Default);
