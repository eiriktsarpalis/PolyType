using PolyType.Examples.JsonSerializer;
using PolyType.FastPathJson.Converters;
using PolyType.ReflectionProvider;
using PolyType.Tests;
using System.Buffers;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class DirectScalarConverterTests
{
    private static readonly HashSet<Type> ScalarTypes =
    [
        typeof(bool), typeof(sbyte), typeof(short), typeof(int), typeof(long),
        typeof(byte), typeof(ushort), typeof(uint), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal), typeof(char),
        typeof(string), typeof(byte[]), typeof(Guid), typeof(DateTime),
        typeof(DateTimeOffset), typeof(TimeSpan), typeof(DateOnly), typeof(TimeOnly),
        typeof(Int128), typeof(UInt128), typeof(Half), typeof(Uri), typeof(Version),
        typeof(BigInteger), typeof(Rune), typeof(BindingFlags),
    ];

    public static IEnumerable<object[]> Values() => TestTypes.GetTestCasesWithExpandedValues()
        .Where(testCase => ScalarTypes.Contains(testCase.Type))
        .Select(testCase => new object[] { testCase, new TestCasesWitness() });

    [Theory]
    [MemberData(nameof(Values))]
    public static void ScalarValuesPreserveRepresentationAndRoundtrips<T, TWitness>(TestCase<T> testCase, TWitness witness)
        where TWitness : IJsonSerializable<T>
    {
        _ = witness;
        var reference = JsonSerializerTS.CreateConverter(testCase.DefaultShape);
        string expected = JsonSerializerTS.Serialize(reference, testCase.Value);
        string actual = FastPathJsonSerializer.Serialize<T, TWitness>(testCase.Value);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<T, TWitness>(
            FastPathJsonSerializer.Deserialize<T, TWitness>(actual.AsSpan())));
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<T, TWitness>(
            FastPathJsonSerializer.Deserialize<T, TWitness>(Encoding.UTF8.GetBytes(actual))));
    }

    public static IEnumerable<object[]> Keys()
    {
        foreach (var testCase in TestTypes.GetTestCasesWithExpandedValues()
            .Where(testCase => ScalarTypes.Contains(testCase.Type) && testCase.Value is not null &&
                testCase.Type != typeof(byte[]) && testCase.Type != typeof(BigInteger) && testCase.Type != typeof(Rune)))
        {
            yield return [testCase, new ScalarKeyWitness()];
        }

        yield return Key(float.NaN);
        yield return Key(float.PositiveInfinity);
        yield return Key(float.NegativeInfinity);
        yield return Key(double.NaN);
        yield return Key(double.PositiveInfinity);
        yield return Key(double.NegativeInfinity);
        yield return Key(Half.NaN);
        yield return Key(Half.PositiveInfinity);
        yield return Key(Half.NegativeInfinity);
        yield return Key(1.5e20f);
        yield return Key(1.5e200);
        yield return Key(-0.0d);
        yield return Key(0.0000000000000000000000000001m);
        yield return Key(TimeSpan.MinValue);
        yield return Key(TimeSpan.MaxValue);
        yield return Key(DateOnly.MinValue);
        yield return Key(DateOnly.MaxValue);
        yield return Key(TimeOnly.MinValue);
        yield return Key(TimeOnly.MaxValue);
        yield return Key(Int128.MinValue);
        yield return Key(Int128.MaxValue);
        yield return Key(UInt128.MaxValue);
        yield return Key(new DateTime(2026, 10, 26, 12, 34, 56, DateTimeKind.Utc).AddTicks(1234000));
        yield return Key(new DateTimeOffset(2026, 10, 26, 12, 34, 56, TimeSpan.FromHours(5.5)).AddTicks(1234000));
        yield return Key(new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public static void ScalarKeysPreserveFormattingAndEncoderRules<T, TWitness>(TestCase<T> testCase, TWitness witness)
        where T : notnull
        where TWitness : IJsonSerializable<Dictionary<T, int>>
    {
        _ = witness;
        Dictionary<T, int> value = new() { [testCase.Value!] = 42 };
        foreach (JavaScriptEncoder? encoder in new[]
        {
            null, JavaScriptEncoder.Default, JavaScriptEncoder.UnsafeRelaxedJsonEscaping, JavaScriptEncoder.Create(UnicodeRanges.None),
        })
        {
            JsonSerializerOptions referenceOptions = Options(encoder);
            JsonWriterOptions options = new() { Encoder = encoder };
            string expected;
            try
            {
                expected = JsonSerializer.Serialize(value, referenceOptions);
            }
            catch (ArgumentException)
            {
                Assert.Throws<ArgumentException>(() => FastPathJsonSerializer.Serialize<Dictionary<T, int>, TWitness>(value, options));
                continue;
            }

            string actual = FastPathJsonSerializer.Serialize<Dictionary<T, int>, TWitness>(value, options);
            Assert.Equal(expected, actual);
            Assert.Equal(expected, FastPathJsonSerializer.Serialize<Dictionary<T, int>, TWitness>(
                FastPathJsonSerializer.Deserialize<Dictionary<T, int>, TWitness>(actual.AsSpan()), options));
            Assert.Equal(expected, FastPathJsonSerializer.Serialize<Dictionary<T, int>, TWitness>(
                FastPathJsonSerializer.Deserialize<Dictionary<T, int>, TWitness>(Encoding.UTF8.GetBytes(actual)), options));
        }
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public static void ScalarKeyParsingMatchesSupportedLexicalForms<T, TWitness>(TestCase<T> testCase, TWitness witness)
        where T : notnull
        where TWitness : IJsonSerializable<Dictionary<T, int>>
    {
        _ = testCase;
        _ = witness;
        JsonSerializerOptions options = Options();
        foreach (string text in new[]
        {
            "0", "+0", "-0", "0000", "0.0", ".5", "1.", "1e2", "1e500", "1,234",
            " 0", "0 ", "NaN", "nan", "nAn", "Infinity", "+Infinity", "-Infinity",
            "true", "TRUE", "false", "FALSE", "2026-10-26", "1:2", "1:2:3", "1.2", "0.0:0:0",
            " 1.2", "1.2 ", "Public, Instance", "", new string('0', 512),
        })
        {
            string json = "{" + JsonSerializer.Serialize(text) + ":42}";
            Dictionary<T, int>? reference;
            try
            {
                reference = JsonSerializer.Deserialize<Dictionary<T, int>>(json, options);
            }
            catch (JsonException)
            {
                Exception? error = Record.Exception(() => FastPathJsonSerializer.Deserialize<Dictionary<T, int>, TWitness>(json));
                Assert.True(error is JsonException or FormatException or InvalidOperationException,
                    $"Unexpected acceptance or exception for {typeof(T)} key '{text}': {error}");
                continue;
            }

            var actual = FastPathJsonSerializer.Deserialize<Dictionary<T, int>, TWitness>(json);
            Assert.NotNull(reference);
            Assert.NotNull(actual);
            Assert.Equal(reference.Count, actual.Count);
            foreach (var entry in reference)
            {
                Assert.True(actual.TryGetValue(entry.Key, out int value));
                Assert.Equal(entry.Value, value);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Values))]
    public static void ScalarReadsSupportEscapedAndSegmentedValues<T, TWitness>(TestCase<T> testCase, TWitness witness)
        where TWitness : IJsonSerializable<T>
    {
        _ = witness;
        var reference = JsonSerializerTS.CreateConverter(testCase.DefaultShape);
        string expected = JsonSerializerTS.Serialize(reference, testCase.Value);
        using JsonDocument document = JsonDocument.Parse(expected);
        string json = document.RootElement.ValueKind is JsonValueKind.String
            ? JsonSerializer.Serialize(document.RootElement.GetString(), new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.None) })
            : expected;
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Segment first = new(bytes.AsMemory(0, 1));
        Segment last = first;
        for (int i = 1; i < bytes.Length; i++)
        {
            last = last.Append(bytes.AsMemory(i, 1));
        }

        Utf8JsonReader reader = new(new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length));
        Assert.True(reader.Read());
        T? result = TWitness.Read(ref reader);
        Assert.Equal(bytes.Length, reader.BytesConsumed);
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<T, TWitness>(result));
        Assert.False(reader.Read());
    }

    public static IEnumerable<object[]> InvalidValues()
    {
        yield return Invalid<int>("\"1\"", typeof(InvalidOperationException));
        yield return Invalid<char>("\"ab\"", typeof(InvalidOperationException));
        yield return Invalid<Int128>("1.5", typeof(FormatException));
        yield return Invalid<UInt128>("-1", typeof(FormatException));
        yield return Invalid<Half>("1e100", typeof(FormatException));
        yield return Invalid<DateOnly>("\"2026-10-26T00:00:00\"", typeof(FormatException));
        yield return Invalid<TimeOnly>("\"24:00:00\"", typeof(FormatException));
        yield return Invalid<TimeSpan>("\" 1\"", typeof(FormatException));
        yield return Invalid<Version>("\"\"", typeof(FormatException));
        yield return Invalid<Version>("\"invalid\"", typeof(JsonException));
        yield return Invalid<Uri>("\"http://[invalid]\"", typeof(JsonException));
    }

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public static void InvalidScalarValuesPreserveExceptionCategories<T, TWitness>(TestCase<T> testCase, TWitness witness, string json, Type exceptionType)
        where TWitness : IJsonSerializable<T>
    {
        _ = testCase;
        _ = witness;
        Assert.Throws(exceptionType, () => FastPathJsonSerializer.Deserialize<T, TWitness>(json));
    }

    private static object[] Key<T>(T value) =>
        [new TestCase<T>(value, ReflectionTypeShapeProvider.Default), new ScalarKeyWitness()];

    private static object[] Invalid<T>(string json, Type exceptionType) =>
        [new TestCase<T>(default, ReflectionTypeShapeProvider.Default), new TestCasesWitness(), json, exceptionType];

    private static JsonSerializerOptions Options(JavaScriptEncoder? encoder = null)
    {
        JsonSerializerOptions options = new() { Encoder = encoder };
        options.Converters.Add(new JsonStringEnumConverter<BindingFlags>());

        return options;
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            Segment next = new(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;

            return next;
        }

    }
}

[GenerateJsonFor<Dictionary<bool, int>>]
[GenerateJsonFor<Dictionary<sbyte, int>>]
[GenerateJsonFor<Dictionary<short, int>>]
[GenerateJsonFor<Dictionary<int, int>>]
[GenerateJsonFor<Dictionary<long, int>>]
[GenerateJsonFor<Dictionary<byte, int>>]
[GenerateJsonFor<Dictionary<ushort, int>>]
[GenerateJsonFor<Dictionary<uint, int>>]
[GenerateJsonFor<Dictionary<ulong, int>>]
[GenerateJsonFor<Dictionary<float, int>>]
[GenerateJsonFor<Dictionary<double, int>>]
[GenerateJsonFor<Dictionary<decimal, int>>]
[GenerateJsonFor<Dictionary<char, int>>]
[GenerateJsonFor<Dictionary<string, int>>]
[GenerateJsonFor<Dictionary<Guid, int>>]
[GenerateJsonFor<Dictionary<DateTime, int>>]
[GenerateJsonFor<Dictionary<DateTimeOffset, int>>]
[GenerateJsonFor<Dictionary<TimeSpan, int>>]
[GenerateJsonFor<Dictionary<DateOnly, int>>]
[GenerateJsonFor<Dictionary<TimeOnly, int>>]
[GenerateJsonFor<Dictionary<Int128, int>>]
[GenerateJsonFor<Dictionary<UInt128, int>>]
[GenerateJsonFor<Dictionary<Half, int>>]
[GenerateJsonFor<Dictionary<Uri, int>>]
[GenerateJsonFor<Dictionary<Version, int>>]
[GenerateJsonFor<Dictionary<BindingFlags, int>>]
public readonly partial struct ScalarKeyWitness;
