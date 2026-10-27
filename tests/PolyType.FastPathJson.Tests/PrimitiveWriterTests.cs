using PolyType.Examples.JsonSerializer;
using PolyType.FastPathJson.Converters;
using PolyType.ReflectionProvider;
using PolyType.Tests;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class PrimitiveWriterTests
{
    public static IEnumerable<object[]> PrimitiveValues()
    {
        HashSet<Type> types =
        [
            typeof(bool), typeof(sbyte), typeof(short), typeof(int), typeof(long),
            typeof(byte), typeof(ushort), typeof(uint), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal), typeof(char),
            typeof(string), typeof(byte[]), typeof(Guid), typeof(DateTime),
            typeof(DateTimeOffset), typeof(Uri),
        ];
        List<object[]> values = TestTypes.GetTestCasesWithExpandedValues()
            .Where(testCase => types.Contains(testCase.Type))
            .Select(testCase => new object[] { testCase, new TestCasesWitness() })
            .ToList();
        AddValues(false);
        AddValues(sbyte.MaxValue);
        AddValues(short.MaxValue);
        AddValues(int.MaxValue);
        AddValues(long.MaxValue);
        AddValues(byte.MinValue);
        AddValues(ushort.MinValue);
        AddValues(uint.MinValue);
        AddValues(ulong.MinValue);
        AddValues(float.MinValue, float.MaxValue, float.Epsilon, -0.0f);
        AddValues(double.MinValue, double.MaxValue, double.Epsilon, -0.0d);
        AddValues(decimal.MinValue, decimal.MaxValue, 0.0000000000000000000000000001m);
        AddValues('\0', '"', '\\', '<', '\u00E9', '\uD800', '\uDC00', '\uFFFF');
        AddValues<string?>(null, "", "<script>&\"\\\n", "\u00E9\uD83E\uDD2F", "\uD800");
        AddValues<byte[]?>(null, [], [0, 1, 2, 253, 254, 255]);
        AddValues(new Guid("12345678-1234-5678-9abc-123456789abc"));
        AddValues(DateTime.MinValue,
            new DateTime(2026, 10, 20, 1, 2, 3, DateTimeKind.Utc).AddTicks(1234567),
            new DateTime(2026, 10, 20, 1, 2, 3, DateTimeKind.Local).AddTicks(1234567),
            new DateTime(2026, 10, 20, 1, 2, 3, DateTimeKind.Unspecified).AddTicks(1234567));
        AddValues(DateTimeOffset.MinValue,
            new DateTimeOffset(2026, 10, 20, 1, 2, 3, TimeSpan.FromHours(5.5)).AddTicks(1234567));
        AddValues<Uri?>(null,
            new Uri("HTTP://EXAMPLE.COM/a%2Fb?q=%3Cvalue%3E#fragment"),
            new Uri("../relative path?q=<value>", UriKind.Relative));

        return values;

        void AddValues<T>(params T[] items) => values.AddRange(items.Select(Case));
    }

    [Theory]
    [MemberData(nameof(PrimitiveValues))]
    public static void PrimitiveWritesPreserveRepresentationAndOptions<T, TConverter>(TestCase<T> testCase, TConverter witness)
        where TConverter : IJsonSerializable<T>
    {
        _ = witness;
        var reference = JsonSerializerTS.CreateConverter(testCase.DefaultShape);
        T[] array = [testCase.Value!, default!];
        List<T> list = [testCase.Value!, default!];
        Dictionary<string, T> dictionary = new() { ["<value>"] = testCase.Value!, ["default"] = default! };
        foreach (JsonWriterOptions options in WriterOptions())
        {
            JsonSerializerOptions stjOptions = new() { Encoder = options.Encoder, WriteIndented = options.Indented };
            string expected = JsonSerializerTS.Serialize(reference, testCase.Value, options);
            Assert.Equal(JsonSerializer.Serialize(testCase.Value, stjOptions), expected);
            Assert.Equal(expected, FastPathJsonSerializer.Serialize<T, TConverter>(testCase.Value, options));
            string expectedArray = JsonSerializer.Serialize(list, stjOptions);
            Assert.Equal(expectedArray,
                FastPathJsonSerializer.Serialize<T[], ArrayConverter<T, TConverter>>(array, options));
            Assert.Equal(expectedArray,
                FastPathJsonSerializer.Serialize<List<T>, ListConverter<T, TConverter>>(list, options));
            Assert.Equal(JsonSerializer.Serialize(dictionary, stjOptions),
                FastPathJsonSerializer.Serialize<Dictionary<string, T>,
                    DictionaryConverter<string, T, StringConverter, TConverter>>(dictionary, options));
        }
    }

    public static IEnumerable<object[]> NonFiniteValues()
    {
        yield return Case(float.NaN);
        yield return Case(float.PositiveInfinity);
        yield return Case(float.NegativeInfinity);
        yield return Case(double.NaN);
        yield return Case(double.PositiveInfinity);
        yield return Case(double.NegativeInfinity);
    }

    [Theory]
    [MemberData(nameof(NonFiniteValues))]
    public static void NonFinitePrimitivesRemainRejected<T, TConverter>(TestCase<T> testCase, TConverter witness)
        where TConverter : IJsonSerializable<T>
    {
        _ = witness;
        foreach (bool skipValidation in new[] { false, true })
        {
            JsonWriterOptions options = new() { SkipValidation = skipValidation };
            Assert.Throws<ArgumentException>(() => JsonSerializer.Serialize(testCase.Value));
            Assert.Throws<ArgumentException>(() => FastPathJsonSerializer.Serialize<T, TConverter>(testCase.Value, options));
            Assert.Throws<ArgumentException>(() =>
                FastPathJsonSerializer.Serialize<T[], ArrayConverter<T, TConverter>>([testCase.Value!], options));
        }
    }

    [Theory]
    [InlineData('\0')]
    [InlineData('"')]
    [InlineData('\\')]
    [InlineData('<')]
    [InlineData('\u00E9')]
    [InlineData('\uD800')]
    [InlineData('\uDC00')]
    [InlineData('\uFFFF')]
    public static void CharacterPropertyNamesHonorEncoder(char key)
    {
        Dictionary<char, int> value = new() { [key] = 42 };
        foreach (JsonWriterOptions options in WriterOptions())
        {
            JsonSerializerOptions stjOptions = new() { Encoder = options.Encoder, WriteIndented = options.Indented };
            Assert.Equal(JsonSerializer.Serialize(value, stjOptions),
                FastPathJsonSerializer.Serialize<Dictionary<char, int>,
                    DictionaryConverter<char, int, CharConverter, Int32Converter>>(value, options));
        }
    }

    private static object[] Case<T>(T value) =>
        [new TestCase<T>(value, ReflectionTypeShapeProvider.Default), new TestCasesWitness()];

    private static IEnumerable<JsonWriterOptions> WriterOptions()
    {
        foreach (JavaScriptEncoder encoder in new[]
        {
            JavaScriptEncoder.Default,
            JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            JavaScriptEncoder.Create(UnicodeRanges.None),
        })
        {
            yield return new() { Encoder = encoder };
            yield return new() { Encoder = encoder, Indented = true };
        }
    }
}
