using Microsoft.FSharp.Core;
using PolyType;
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.JsonSerializer.Converters;
using PolyType.Tests.FSharp;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Tests;

public abstract partial class JsonTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void Roundtrip_PointByTypeExtensionSurrogate()
    {
        Point original = new(1, 2);
        JsonConverter<Point> converter = JsonSerializerTS.CreateConverter<Point>(providerUnderTest.Provider);
        string json = converter.Serialize(original);

        // Verify that the surrogate is used in serialization
        // by checking that the value of X is 2 instead of 1.
        Assert.Equal("""{"X":2,"Y":2}""", json);

        Point deserialized = converter.Deserialize(json);

        // Verify that the surrogate is used in deserialization
        // by checking that the value matches the original,
        // since the surrogate subtracts 1 from X.
        Assert.Equal(original, deserialized);
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public async Task Roundtrip_Value<T>(TestCase<T> testCase)
    {
        if (IsUnsupportedBySTJ(testCase))
        {
            return;
        }

        JsonConverter<T> converter = GetConverterUnderTest(testCase);

        string json = converter.Serialize(testCase.Value);
        Assert.Equal(await ToJsonBaseline(testCase.Value), json);

        if (!providerUnderTest.HasConstructor(testCase) && testCase.Value is not null)
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(json));
        }
        else
        {
            T? deserializedValue = converter.Deserialize(json);
            
            if (testCase.IsEquatable)
            {
                Assert.Equal(testCase.Value, deserializedValue);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.Deserialize(converter.Serialize(deserializedValue));
                }
                
                Assert.Equal(json, await ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public async Task Roundtrip_Property<T>(TestCase<T> testCase)
    {
        if (providerUnderTest.Kind is ProviderKind.SourceGen)
        {
            return;
        }

        if (IsUnsupportedBySTJ(testCase))
        {
            return;
        }

        JsonConverter<PocoWithGenericProperty<T>> converter = JsonSerializerTS.CreateConverter<PocoWithGenericProperty<T>>(providerUnderTest.Provider);
        PocoWithGenericProperty<T> poco = new() { Value = testCase.Value };

        string json = converter.Serialize(poco);
        Assert.Equal(await ToJsonBaseline(poco), json);

        if (testCase.Value is not null && !providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(json));
        }
        else
        {
            PocoWithGenericProperty<T>? deserializedValue = converter.Deserialize(json);
            Assert.NotNull(deserializedValue);

            if (testCase.IsEquatable)
            {
                Assert.Equal(testCase.Value, deserializedValue.Value);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.Deserialize(converter.Serialize(deserializedValue));
                }
                
                Assert.Equal(json, await ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public async Task Roundtrip_CollectionElement<T>(TestCase<T> testCase)
    {
        if (providerUnderTest.Kind is ProviderKind.SourceGen)
        {
            return;
        }

        if (IsUnsupportedBySTJ(testCase))
        {
            return;
        }

        var converter = JsonSerializerTS.CreateConverter<List<T?>>(providerUnderTest.Provider);
        var list = new List<T?> { testCase.Value, testCase.Value, testCase.Value };

        string json = converter.Serialize(list);
        Assert.Equal(await ToJsonBaseline(list), json);

        if (testCase.Value is not null && !providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(json));
        }
        else
        {
            List<T?>? deserializedValue = converter.Deserialize(json)!;
            Assert.NotEmpty(deserializedValue);

            if (testCase.IsEquatable)
            {
                Assert.Equal<T?>(list, deserializedValue);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.Deserialize(converter.Serialize(deserializedValue));
                }
                
                Assert.Equal(json, await ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public async Task Roundtrip_DictionaryEntry<T>(TestCase<T> testCase)
    {
        if (providerUnderTest.Kind is ProviderKind.SourceGen)
        {
            return;
        }

        if (IsUnsupportedBySTJ(testCase))
        {
            return;
        }

        var converter = JsonSerializerTS.CreateConverter<Dictionary<string, T?>>(providerUnderTest.Provider);
        var dict = new Dictionary<string, T?> { ["key1"] = testCase.Value, ["key2"] = testCase.Value, ["key3"] = testCase.Value };

        string json = converter.Serialize(dict);
        Assert.Equal(await ToJsonBaseline(dict), json);

        if (testCase.Value is not null && !providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => converter.Deserialize(json));
        }
        else
        {
            Dictionary<string, T?>? deserializedValue = converter.Deserialize(json)!;
            Assert.NotEmpty(deserializedValue);

            if (testCase.IsEquatable)
            {
                Assert.Equal<KeyValuePair<string, T?>>(dict, deserializedValue);
            }
            else
            {
                if (testCase.IsStack)
                {
                    deserializedValue = converter.Deserialize(converter.Serialize(deserializedValue));
                }
                
                Assert.Equal(json, await ToJsonBaseline(deserializedValue));
            }
        }
    }

    [Fact]
    public void Serialize_NonNullablePropertyWithNullValue_ThrowsJsonException()
    {
        var invalidValue = new NonNullStringRecord(null!);
        var converter = JsonSerializerTS.CreateConverter<NonNullStringRecord>(providerUnderTest.Provider);
        Assert.Throws<JsonException>(() => converter.Serialize(invalidValue));
    }

    [Fact]
    public void Deserialize_NonNullablePropertyWithNullJsonValue_ThrowsJsonException()
    {
        var converter = JsonSerializerTS.CreateConverter<NonNullStringRecord>(providerUnderTest.Provider);
        Assert.Throws<JsonException>(() => converter.Deserialize("""{"value":null}"""));
    }

    [Fact]
    public void Serialize_NullablePropertyWithNullValue_WorksAsExpected()
    {
        var converter = JsonSerializerTS.CreateConverter<NullableStringRecord>(providerUnderTest.Provider);
        var valueWithNull = new NullableStringRecord(null);
        
        string json = converter.Serialize(valueWithNull);

        Assert.Equal("""{"value":null}""", json);
    }

    [Fact]
    public void Serialize_NullablePropertyWithNullJsonValue_WorksAsExpected()
    {
        var converter = JsonSerializerTS.CreateConverter<NullableStringRecord>(providerUnderTest.Provider);
        
        NullableStringRecord? result = converter.Deserialize("""{"value":null}""");

        Assert.NotNull(result);
        Assert.Null(result.value);
    }

    [Theory]
    [MemberData(nameof(GetLongTuplesAndExpectedJson))]
    public void LongTuples_SerializedAsFlatJson<TTuple>(TestCase<TTuple> testCase, string expectedEncoding)
    {
        // Tuples should be serialized as flat JSON, without exposing "Rest" fields.
        var converter = GetConverterUnderTest(testCase);

        string json = converter.Serialize(testCase.Value);
        Assert.Equal(expectedEncoding, json);

        var deserializedValue = converter.Deserialize(json);
        Assert.Equal(testCase.Value, deserializedValue);
    }

    public static IEnumerable<object?[]> GetLongTuplesAndExpectedJson()
    {
        ITypeShapeProvider p = Witness.ShapeProvider;
        yield return [TestCase.Create(
            (x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9),p),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9}"""];

        yield return [TestCase.Create(
            (x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9, x10: 10, x11: 11, x12: 12, x13: 13, x14: 14, x15: 15, x16: 16, x17: 17, x18: 18, x19:19, x20:20, x21:21, x22:22, x23:23, x24:24, x25:25, x26:26, x27:27, x28:28, x29:29, x30:30), p),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9,"Item10":10,"Item11":11,"Item12":12,"Item13":13,"Item14":14,"Item15":15,"Item16":16,"Item17":17,"Item18":18,"Item19":19,"Item20":20,"Item21":21,"Item22":22,"Item23":23,"Item24":24,"Item25":25,"Item26":26,"Item27":27,"Item28":28,"Item29":29,"Item30":30}"""];

        yield return [TestCase.Create(
            new Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10)), p),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9,"Item10":10}"""];

        yield return [TestCase.Create(
            new Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10, 11, 12, 13, 14, new(15))), p),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9,"Item10":10,"Item11":11,"Item12":12,"Item13":13,"Item14":14,"Item15":15}"""];
    }

    [Theory]
    [MemberData(nameof(GetMultiDimensionalArraysAndExpectedJson))]
    public void MultiDimensionalArrays_SerializedAsJaggedArray<TArray>(TestCase<TArray> testCase, string expectedEncoding)
        where TArray : IEnumerable
    {
        var converter = GetConverterUnderTest(testCase);

        string json = converter.Serialize(testCase.Value);
        Assert.Equal(expectedEncoding, json);

        TArray? result = converter.Deserialize(json);
        Assert.Equal(testCase.Value, result);
    }

    public static IEnumerable<object?[]> GetMultiDimensionalArraysAndExpectedJson()
    {
#if NET8_0_OR_GREATER
        Witness p = new();
#else
        ITypeShapeProvider p = Witness.ShapeProvider;
#endif
        yield return [TestCase.Create(new int[,] { }, p), """[]"""];
        yield return [TestCase.Create(new int[,,] { }, p), """[]"""];
        yield return [TestCase.Create(new int[,,,,,] { }, p), """[]"""];

        yield return [TestCase.Create(
            new int[,] { { 1, 0, }, { 0, 1 } }, p),
            """[[1,0],[0,1]]"""];

        yield return [TestCase.Create(
            new int[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } }, p),
            """[[1,0,0],[0,1,0],[0,0,1]]"""];

        yield return [TestCase.Create(
            new int[,] { { 1, 2, 3 }, { 4, 5, 6 } }, p),
            """[[1,2,3],[4,5,6]]"""];
        
        yield return [TestCase.Create(
            new int[,,] // 3 x 2 x 2
            {
                { { 1, 0 }, { 0, 1 } }, 
                { { 1, 2 }, { 3, 4 } }, 
                { { 1, 1 }, { 1, 1 } }
            }, p),
            """[[[1,0],[0,1]],[[1,2],[3,4]],[[1,1],[1,1]]]"""];
        
        yield return [TestCase.Create(
            new int[,,] // 3 x 2 x 5
            {
                { { 1, 0, 0, 0, 0 }, { 0, 1, 0, 0, 0 } }, 
                { { 1, 2, 3, 4, 5 }, { 6, 7, 8, 9, 10 } }, 
                { { 1, 1, 1, 1, 1 }, { 1, 1, 1, 1, 1 } }
            }, p),
            """[[[1,0,0,0,0],[0,1,0,0,0]],[[1,2,3,4,5],[6,7,8,9,10]],[[1,1,1,1,1],[1,1,1,1,1]]]"""];
    }

    [Theory]
    [MemberData(nameof(GetFSharpUnionsAndExpectedJson))]
    public void Roundtrip_FSharpUnions<TUnion>(TestCase<TUnion> testCase, string expectedEncoding)
    {
        var converter = GetConverterUnderTest(testCase);

        string json = converter.Serialize(testCase.Value);
        Assert.Equal(expectedEncoding, json);

        TUnion? result = converter.Deserialize(json);
        Assert.Equal(testCase.Value, result);
    }

    public static IEnumerable<object[]> GetFSharpUnionsAndExpectedJson()
    {
#if NET8_0_OR_GREATER
        Witness p = new();
#else
        ITypeShapeProvider p = Witness.ShapeProvider;
#endif
        yield return [TestCase.Create(FSharpOption<int>.None, p), """null"""];
        yield return [TestCase.Create(FSharpOption<int>.Some(42), p), """42"""];
        yield return [TestCase.Create(FSharpUnion.NewA("string", 42), p), """{"$type":"A","bar":"string","baz":42}"""];
        yield return [TestCase.Create(FSharpUnion.B, p), """{"$type":"B"}"""];
        yield return [TestCase.Create(FSharpUnion.NewC(42), p), """{"$type":"C","foo":42}"""];
        yield return [TestCase.Create(FSharpEnumUnion.A, p), """{"$type":"A"}"""];
        yield return [TestCase.Create(FSharpEnumUnion.B, p), """{"$type":"B"}"""];
        yield return [TestCase.Create(FSharpEnumUnion.C, p), """{"$type":"C"}"""];
        yield return [TestCase.Create(FSharpSingleCaseUnion.NewCase(42), p), """{"$type":"Case","Item":42}"""];
        yield return [TestCase.Create(FSharpValueOption<int>.None, p), """null"""];
        yield return [TestCase.Create(FSharpValueOption<int>.Some(42), p), """42"""];
        yield return [TestCase.Create(FSharpStructUnion.NewA("string", 42), p), """{"$type":"A","bar":"string","baz":42}"""];
        yield return [TestCase.Create(FSharpStructUnion.B, p), """{"$type":"B"}"""];
        yield return [TestCase.Create(FSharpStructUnion.NewC(42), p), """{"$type":"C","foo":42}"""];
        yield return [TestCase.Create(FSharpEnumStructUnion.A, p), """{"$type":"A"}"""];
        yield return [TestCase.Create(FSharpEnumStructUnion.B, p), """{"$type":"B"}"""];
        yield return [TestCase.Create(FSharpEnumStructUnion.C, p), """{"$type":"C"}"""];
        yield return [TestCase.Create(FSharpSingleCaseStructUnion.NewCase(42), p), """{"$type":"Case","Item":42}"""];
        yield return [TestCase.Create(FSharpResult<string, int>.NewOk("str"), p), """{"$type":"Ok","ResultValue":"str"}"""];
        yield return [TestCase.Create(FSharpResult<string, int>.NewError(-1), p), """{"$type":"Error","ErrorValue":-1}"""];
    }

    [Fact]
    public void Roundtrip_DerivedClassWithVirtualProperties()
    {
        const string ExpectedJson = """{"X":42,"Y":"str","Z":42,"W":0}""";
        var converter = JsonSerializerTS.CreateConverter<DerivedClassWithVirtualProperties>(providerUnderTest.Provider);

        var value = new DerivedClassWithVirtualProperties();
        string json = converter.Serialize(value);
        Assert.Equal(ExpectedJson, json);
    }

    [Fact]
    public void Roundtrip_ClassWithMarshaller()
    {
        const string ExpectedJson = "\"the actual value\"";
        var converter = JsonSerializerTS.CreateConverter<TypeWithStringSurrogate>(providerUnderTest.Provider);

        TypeWithStringSurrogate value = new("the actual value");
        string json = converter.Serialize(value);
        Assert.Equal(ExpectedJson, json);
        
        TypeWithStringSurrogate? deserializedValue = converter.Deserialize(json);
        Assert.Equal(value, deserializedValue);
    }

    [Fact]
    public void ClassWithInitOnlyProperties_MissingPayloadPreservesDefaultValues()
    {
        var converter = JsonSerializerTS.CreateConverter<ClassWithInitOnlyProperties>(providerUnderTest.Provider);
        int expectedValue = new ClassWithInitOnlyProperties().Value;
        List<int> expectedValues = new ClassWithInitOnlyProperties().Values;

        ClassWithInitOnlyProperties? result = converter.Deserialize("{}");
        Assert.Equal(expectedValue, result?.Value);
        Assert.Equal(expectedValues, result?.Values);
    }

    public class PocoWithGenericProperty<T>
    { 
        public T? Value { get; set; }
    }

    protected static async Task<string> ToJsonBaseline<T>(T? value)
    {
        MemoryStream stream = new();
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, value, s_baselineOptions);
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }
    
    private static readonly JsonSerializerOptions s_baselineOptions = new()
    { 
        IncludeFields = true,
        Converters = 
        { 
            new JsonStringEnumConverter(),
            new BigIntegerConverter(),
#if NET8_0_OR_GREATER
            new RuneConverter(),
#endif
        },
    };

    private JsonConverter<T> GetConverterUnderTest<T>(TestCase<T> testCase) =>
        JsonSerializerTS.CreateConverter(providerUnderTest.ResolveShape(testCase));

    private protected static bool IsUnsupportedBySTJ<T>(TestCase<T> value) => 
        value.IsMultiDimensionalArray ||
        value.IsLongTuple ||
        value.HasRefConstructorParameters ||
        value.CustomKind is not null ||
        value.UsesMarshaller ||
        value.IsUnion && (!typeof(T).GetCustomAttributes<JsonDerivedTypeAttribute>().Any() || value.IsAbstract) ||
        value.Value is DerivedClassWithVirtualProperties; // https://github.com/dotnet/runtime/issues/96996
}

public sealed class JsonTests_Reflection() : JsonTests(RefectionProviderUnderTest.NoEmit);
public sealed class JsonTests_ReflectionEmit() : JsonTests(RefectionProviderUnderTest.Emit);
public sealed class JsonTests_SourceGen() : JsonTests(SourceGenProviderUnderTest.Default);