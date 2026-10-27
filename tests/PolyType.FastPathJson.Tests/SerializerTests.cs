using PolyType.Examples.JsonSerializer;
using PolyType.FastPathJson.Converters;
using PolyType.ReflectionProvider;
using PolyType.Tests;
using System.Buffers;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class SerializerTests
{
    public static IEnumerable<object[]> TestCases()
    {
        HashSet<Type> supported = typeof(TestCasesWitness).GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IJsonSerializable<>))
            .Select(i => i.GetGenericArguments()[0]).ToHashSet();
        return TestTypes.GetTestCasesWithExpandedValues()
            .Where(t => supported.Contains(t.Type))
            .Select(t => new object[] { t, new TestCasesWitness() });
    }

    public static IEnumerable<object[]> GetSerializationBaselines()
    {
        yield return Case(42, "42");
        yield return Case(new byte[] { 1, 2, 3 }, "\"AQID\"");
        yield return Case(BindingFlags.Instance | BindingFlags.Public, "\"Instance, Public\"");
        yield return Case<int?>(42, "42");
        yield return Case<int?>(null, "null");
        yield return Case(new SimplePoco { Value = 42 }, """{"Value":42}""");
        yield return Case(new PocoWithListAndDictionaryProps(@string: "text")
        {
            List = [1, 2],
            Dict = new() { ["key"] = 3 },
        }, """{"Bool":true,"String":"text","List":[1,2],"Dict":{"key":3}}""");
        yield return Case(new int[] { 1, 2, 3 }, "[1,2,3]");
        yield return Case(new List<string> { "one", "two" }, """["one","two"]""");
        yield return Case(new CollectionWithEnumerableCtor([1, 2, 3]), "[1,2,3]");
        yield return Case(new SortedList<int, string> { [42] = "value" }, """{"42":"value"}""");
        yield return Case((42, "text"), """{"Item1":42,"Item2":"text"}""");
        yield return Case((1, 2, 3, 4, 5, 6, 7, 8, 9),
            """{"Item1":1,"Item2":2,"Item3":3,"Item4":4,"Item5":5,"Item6":6,"Item7":7,"Item8":8,"Item9":9}""");
        yield return Case(new int[,] { { 1, 2 }, { 3, 4 } }, "[[1,2],[3,4]]");
        yield return Case<Tree>(new Tree.Node(42, new Tree.Leaf(), new Tree.Leaf()),
            """{"$type":"node","Value":42,"Left":{"$type":"leaf"},"Right":{"$type":"leaf"}}""");

        static object[] Case<T>(T? value, string expectedJson) =>
            [new TestCase<T>(value, ReflectionTypeShapeProvider.Default), new TestCasesWitness(), expectedJson];
    }

    [Theory]
    [MemberData(nameof(GetSerializationBaselines))]
    public static void SerializationMatchesBaseline<T, TConverter>(TestCase<T> testCase, TConverter witness, string expectedJson)
        where TConverter : IJsonSerializable<T>
    {
        _ = witness;
        Assert.Equal(expectedJson, FastPathJsonSerializer.Serialize<T, TConverter>(testCase.Value));

        T? fromChars = FastPathJsonSerializer.Deserialize<T, TConverter>(expectedJson.AsSpan());
        T? fromBytes = FastPathJsonSerializer.Deserialize<T, TConverter>(Encoding.UTF8.GetBytes(expectedJson));
        Assert.Equal(expectedJson, FastPathJsonSerializer.Serialize<T, TConverter>(fromChars));
        Assert.Equal(expectedJson, FastPathJsonSerializer.Serialize<T, TConverter>(fromBytes));
    }

    [Theory]
    [MemberData(nameof(GetSerializationBaselines))]
    public static void RuntimeDispatchComposesSingleValues<T, TConverter>(TestCase<T> testCase, TConverter witness, string expectedJson)
        where TConverter : IJsonSerializable<T>
    {
        _ = witness;
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartArray();
            SerializationHelpers.Write<T, TConverter>(writer, testCase.Value);
            SerializationHelpers.Write<int, Int32Converter>(writer, 42);
            writer.WriteEndArray();
            writer.Flush();
        }

        Assert.Equal($"[{expectedJson},42]", Encoding.UTF8.GetString(buffer.WrittenSpan));
        Utf8JsonReader reader = new(buffer.WrittenSpan);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);
        Assert.True(reader.Read());

        T? value = SerializationHelpers.Read<T, TConverter>(ref reader);
        Assert.Equal(1L + Encoding.UTF8.GetByteCount(expectedJson), reader.BytesConsumed);
        Assert.Equal(expectedJson, FastPathJsonSerializer.Serialize<T, TConverter>(value));
        Assert.True(reader.Read());
        Assert.Equal(42, SerializationHelpers.Read<int, Int32Converter>(ref reader));
        Assert.Equal(JsonTokenType.Number, reader.TokenType);
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        Assert.False(reader.Read());
    }

    [Theory]
    [InlineData(nameof(SerializationHelpers.Write))]
    [InlineData(nameof(SerializationHelpers.Read))]
    [InlineData(nameof(SerializationHelpers.WriteKey))]
    [InlineData(nameof(SerializationHelpers.ReadKey))]
    [InlineData(nameof(SerializationHelpers.EnsureRead))]
    [InlineData(nameof(SerializationHelpers.EnsureToken))]
    public static void RuntimeDispatchRequestsAggressiveInlining(string methodName)
    {
        MethodInfo method = Assert.Single(typeof(SerializationHelpers).GetMethods(BindingFlags.Public | BindingFlags.Static),
            candidate => candidate.Name == methodName);
        Assert.True((method.MethodImplementationFlags & MethodImplAttributes.AggressiveInlining) != 0);
    }

    [Theory]
    [InlineData(nameof(SerializationHelpers.ThrowNullPropertyOnWrite))]
    [InlineData(nameof(SerializationHelpers.ThrowNullPropertyOnRead))]
    [InlineData(nameof(SerializationHelpers.ThrowMissingRequiredParameters))]
    public static void RuntimeThrowHelpersRemainOutlined(string methodName)
    {
        MethodInfo method = Assert.Single(typeof(SerializationHelpers).GetMethods(BindingFlags.Public | BindingFlags.Static),
            candidate => candidate.Name == methodName);
        Assert.True((method.MethodImplementationFlags & MethodImplAttributes.NoInlining) != 0);
        Assert.NotNull(method.GetCustomAttribute<System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute>());
    }

    public static IEnumerable<object[]> GetErrorCases()
    {
        yield return [(Action)(() =>
        {
            Utf8JsonReader reader = new("{"u8, isFinalBlock: false, state: default);
            Assert.True(reader.Read());
            ReadGenerated<Receipt>(ref reader);
        }), typeof(JsonException), "Incomplete JSON value."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<Receipt>("[]")),
            typeof(JsonException), "Expected StartObject, got StartArray."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<Receipt>("{}")),
            typeof(KeyNotFoundException), "Missing required constructor parameters: 'Number', 'Customer'."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<Receipt>("""{"Customer":"value"}""")),
            typeof(KeyNotFoundException), "Missing required constructor parameter 'Number'."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<Receipt>("""{"Number":42,"Customer":null}""")),
            typeof(JsonException), "The property 'Customer' cannot be set to null."];
        yield return [(Action)(() => FastPathJsonSerializer.Serialize(new Receipt(42, null!))),
            typeof(JsonException), "The property 'Customer' cannot contain null."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<Tree, TestCasesWitness>("""{"$type":"unknown"}""")),
            typeof(JsonException), "Unrecognized type discriminator."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<int[,], TestCasesWitness>("[[1],[2,3]]")),
            typeof(JsonException), "Expected a rectangular array."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<int[], TestCasesWitness>("42")),
            typeof(JsonException), "Expected a JSON array."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<Dictionary<string, int>, TestCasesWitness>("[]")),
            typeof(JsonException), "Expected a JSON object."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<BigInteger, BigIntegerConverter>("1.5")),
            typeof(JsonException), "Invalid arbitrary-precision integer."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<Rune, RuneConverter>("\"ab\"")),
            typeof(JsonException), "Expected a single Unicode scalar value."];
        yield return [(Action)(() => FastPathJsonSerializer.Deserialize<Dictionary<SimplePoco, int>,
                DictionaryConverter<SimplePoco, int, TestCasesWitness, Int32Converter>>("""{"key":1}""")),
            typeof(NotSupportedException), "'PolyType.Tests.SimplePoco' is not supported as a JSON property name."];
    }

    [Theory]
    [MemberData(nameof(GetErrorCases))]
    public static void ThrowHelpersPreserveExceptionContracts(Action operation, Type exceptionType, string expectedMessage)
    {
        Exception exception = Assert.Throws(exceptionType, operation);
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public static void BidirectionalCompatibility<T, TConverter>(TestCase<T> testCase, TConverter witness)
        where TConverter : IJsonSerializable<T>
    {
        _ = witness;
        var reference = JsonSerializerTS.CreateConverter(testCase.DefaultShape);
        string expected = JsonSerializerTS.Serialize(reference, testCase.Value);
        string actual = FastPathJsonSerializer.Serialize<T, TConverter>(testCase.Value);

        T? referenceFromFast = JsonSerializerTS.Deserialize(reference, actual.AsSpan());
        T? fastFromReference = FastPathJsonSerializer.Deserialize<T, TConverter>(expected.AsSpan());
        T? fastFromUtf8 = FastPathJsonSerializer.Deserialize<T, TConverter>(Encoding.UTF8.GetBytes(expected));
        T? fastFromFast = FastPathJsonSerializer.Deserialize<T, TConverter>(actual.AsSpan());

        if (testCase.IsEquatable)
        {
            Assert.Equal(testCase.Value, referenceFromFast);
            Assert.Equal(testCase.Value, fastFromReference);
            Assert.Equal(testCase.Value, fastFromUtf8);
            Assert.Equal(testCase.Value, fastFromFast);
        }

        Assert.Equal(expected, JsonSerializerTS.Serialize(reference, referenceFromFast));
        Assert.Equal(expected, JsonSerializerTS.Serialize(reference, fastFromReference));
        Assert.Equal(expected, JsonSerializerTS.Serialize(reference, fastFromUtf8));
        Assert.Equal(actual, FastPathJsonSerializer.Serialize<T, TConverter>(fastFromFast));
    }

    [Theory]
    [InlineData(typeof(SimplePoco))]
    [InlineData(typeof(SimpleRecord))]
    [InlineData(typeof(MyLinkedList<int>))]
    [InlineData(typeof(CollectionWithSpanCtor))]
    [InlineData(typeof(DictionaryWithEnumerableCtor))]
    [InlineData(typeof(int[,]))]
    [InlineData(typeof((int, string)))]
    [InlineData(typeof(Tree))]
    public static void RepresentativeModelKindsAreExercised(Type requiredType) =>
        Assert.Contains(TestCases(), row => ((ITestCase)row[0]).Type == requiredType && ((ITestCase)row[0]).Value is not null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TargetOwnedGenerationHonorsMetadata(bool indented)
    {
        Receipt value = new(42, "customer") { Lines = [new("book", 12.50m)], Notification = () => { } };
        var options = new JsonWriterOptions { Indented = indented };
        var reference = JsonSerializerTS.CreateConverter<Receipt>(ReflectionTypeShapeProvider.Default);
        string json = FastPathJsonSerializer.Serialize(value, options);
        Assert.Equal(JsonSerializerTS.Serialize(reference, value, options), json);
        Receipt fromChars = FastPathJsonSerializer.Deserialize<Receipt>(json.AsSpan())!;
        Receipt fromBytes = FastPathJsonSerializer.Deserialize<Receipt>(Encoding.UTF8.GetBytes(json))!;
        Assert.Equal(value.Number, fromChars.Number);
        Assert.Equal(value.Customer, fromChars.Customer);
        Assert.Equal(value.Lines, fromChars.Lines);
        Assert.Null(fromChars.Notification);
        Assert.Equal(json, FastPathJsonSerializer.Serialize(fromChars, options));
        Assert.Equal(json, FastPathJsonSerializer.Serialize(fromBytes, options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void GeneratorFoldsNestedWitnesses(bool indented)
    {
        List<List<List<int>>> lists = [[[1, 2], []], [[3]]];
        var options = new JsonWriterOptions { Indented = indented };
        string listJson = FastPathJsonSerializer.Serialize<List<List<List<int>>>, FoldingWitness>(lists, options);
        var restoredLists = FastPathJsonSerializer.Deserialize<List<List<List<int>>>, FoldingWitness>(listJson.AsSpan());
        Assert.Equal(listJson, FastPathJsonSerializer.Serialize<List<List<List<int>>>, FoldingWitness>(restoredLists, options));

        Dictionary<string, (Receipt, double[])> values = new()
        {
            ["key"] = (new Receipt(42, "customer"), [3.14, -1.1]),
        };
        var reference = JsonSerializerTS.CreateConverter<Dictionary<string, (Receipt, double[])>>(ReflectionTypeShapeProvider.Default);
        string json = FastPathJsonSerializer.Serialize<Dictionary<string, (Receipt, double[])>, FoldingWitness>(values, options);
        Assert.Equal(JsonSerializerTS.Serialize(reference, values, options), json);
        var restored = FastPathJsonSerializer.Deserialize<Dictionary<string, (Receipt, double[])>, FoldingWitness>(json.AsSpan())!;
        Assert.Equal(values["key"].Item1, restored["key"].Item1);
        Assert.Equal(values["key"].Item2, restored["key"].Item2);
    }

    [Theory]
    [InlineData("{\"42\":\"first\",\"42\":\"last\"}", "last")]
    [InlineData("{\"$type\":\"ignored\",\"42\":\"last\"}", "last")]
    public static void DictionaryWitnessComposesPropertyNames(string json, string expected)
    {
        var value = FastPathJsonSerializer.Deserialize<Dictionary<int, string>,
            DictionaryConverter<int, string, Int32Converter, StringConverter>>(json.AsSpan())!;
        Assert.Equal(expected, value[42]);
        string roundtrip = FastPathJsonSerializer.Serialize<Dictionary<int, string>,
            DictionaryConverter<int, string, Int32Converter, StringConverter>>(value);
        Assert.Equal("{\"42\":\"last\"}", roundtrip);
        Assert.Throws<NotSupportedException>(() =>
            FastPathJsonSerializer.Deserialize<Dictionary<SimplePoco, int>,
                DictionaryConverter<SimplePoco, int, TestCasesWitness, Int32Converter>>("{\"key\":1}"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Number\":42}")]
    public static void MissingConstructorParametersFail(string json) =>
        Assert.Throws<KeyNotFoundException>(() => FastPathJsonSerializer.Deserialize<Receipt>(json.AsSpan()));

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"Number\":42,\"Customer\":null}")]
    [InlineData("{\"Number\":42,\"Customer\":\"a\"")]
    public static void InvalidObjectsFail(string json) =>
        Assert.ThrowsAny<JsonException>(() => FastPathJsonSerializer.Deserialize<Receipt>(json.AsSpan()));

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("[1")]
    [InlineData("[[1],[2,3]]")]
    public static void InvalidRectangularArraysFail(string json)
    {
        if (json == "null")
        {
            Assert.Null(FastPathJsonSerializer.Deserialize<int[,], TestCasesWitness>(json.AsSpan()));
            return;
        }

        Assert.ThrowsAny<JsonException>(() => FastPathJsonSerializer.Deserialize<int[,], TestCasesWitness>(json.AsSpan()));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public static void WriterDepthBoundsRecursiveInstances(int maxDepth)
    {
        MyLinkedList<int> value = new() { Value = 42 };
        value.Next = value;
        Assert.Throws<InvalidOperationException>(() =>
            FastPathJsonSerializer.Serialize<MyLinkedList<int>, TestCasesWitness>(value, new() { MaxDepth = maxDepth }));
    }

    [Theory]
    [InlineData("{\"Value\":1,\"unknown\":{\"items\":[1,2]},\"Value\":2}", 2)]
    [InlineData("{\"Value\":3} ignored", 3)]
    [InlineData("{/* comment */\"Value\":4,}", 4)]
    public static void ReaderOptionsAndSingleValueConsumption(string json, int expected)
    {
        SimplePoco value = FastPathJsonSerializer.Deserialize<SimplePoco, TestCasesWitness>(
            json.AsSpan(), new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!;
        Assert.Equal(expected, value.Value);
    }

    [Theory]
    [InlineData(JsonCommentHandling.Disallow)]
    [InlineData(JsonCommentHandling.Skip)]
    [InlineData(JsonCommentHandling.Allow)]
    public static void PropertyLoopsPreserveCommentHandling(JsonCommentHandling handling)
    {
        JsonReaderOptions options = new() { CommentHandling = handling };
        Action[] read =
        [
            () => Assert.NotNull(FastPathJsonSerializer.Deserialize<Receipt>(
                """{/* comment */"Number":42,"Customer":"value"}""", options)),
            () => Assert.NotNull(FastPathJsonSerializer.Deserialize<Tree, TestCasesWitness>(
                """{/* comment */"$type":"leaf"}""", options)),
            () => Assert.NotNull(FastPathJsonSerializer.Deserialize<SortedList<int, string>, TestCasesWitness>(
                """{/* comment */"42":"value"}""", options)),
        ];
        foreach (Action operation in read)
        {
            if (handling is JsonCommentHandling.Skip)
            {
                operation();
            }
            else if (handling is JsonCommentHandling.Allow)
            {
                var exception = Assert.Throws<JsonException>(operation);
                Assert.Equal("Expected PropertyName, got Comment.", exception.Message);
            }
            else
            {
                Assert.ThrowsAny<JsonException>(operation);
            }
        }
    }

    [Theory]
    [InlineData("{\"$type\":\"leaf\"}")]
    [InlineData("{\"ignored\":{\"nested\":1},\"$type\":\"leaf\"}")]
    public static void HierarchyDispatchIsGenerated(string json)
    {
        Tree value = FastPathJsonSerializer.Deserialize<Tree, TestCasesWitness>(json.AsSpan())!;
        Assert.IsType<Tree.Leaf>(value);
        Assert.Equal("{\"$type\":\"leaf\"}", FastPathJsonSerializer.Serialize<Tree, TestCasesWitness>(value));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[[],[]]")]
    [InlineData("[[1,2],[3,4]]")]
    public static void RectangularArrayRoundtripIsStable(string json)
    {
        int[,]? value = FastPathJsonSerializer.Deserialize<int[,], TestCasesWitness>(json.AsSpan());
        string actual = FastPathJsonSerializer.Serialize<int[,], TestCasesWitness>(value);
        Assert.Equal(json == "[[],[]]" ? "[]" : json, actual);
    }

    private static T? ReadGenerated<T>(ref Utf8JsonReader reader) where T : IJsonSerializable<T> => T.Read(ref reader);
}
