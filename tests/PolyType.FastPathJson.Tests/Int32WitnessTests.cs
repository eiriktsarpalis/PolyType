using PolyType.FastPathJson.Converters;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class Int32WitnessTests
{
    [Theory]
    [InlineData(int.MinValue, false)]
    [InlineData(int.MinValue, true)]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(42, false)]
    [InlineData(int.MaxValue, false)]
    [InlineData(int.MaxValue, true)]
    public static void ValueTypeWitnessComposesThroughPublicSerializer(int value, bool indented) =>
        Verify<Int32Converter>(value, new() { Indented = indented });

    private static void Verify<TWitness>(int value, JsonWriterOptions options)
        where TWitness : struct, IJsonSerializable<int>
    {
        Check<int, TWitness>(value, options);
        Check<int?, NullableConverter<int, TWitness>>(value, options);
        Check<int?, NullableConverter<int, TWitness>>(null, options);
        Check<int[], ArrayConverter<int, TWitness>>([value, 0, 42], options);
        Check<List<int>, ListConverter<int, TWitness>>([value, 0, 42], options);
        Check<Dictionary<int, int>, DictionaryConverter<int, int, TWitness, TWitness>>(
            new() { [value] = value }, options);
        Check<Dictionary<string, int>, DictionaryConverter<string, int, StringConverter, TWitness>>(
            new() { ["value"] = value }, options);
    }

    private static void Check<T, TWitness>(T? value, JsonWriterOptions options)
        where TWitness : IJsonSerializable<T>
    {
        string expected = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = options.Indented });
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<T, TWitness>(value, options));
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<T, TWitness>(
            FastPathJsonSerializer.Deserialize<T, TWitness>(expected.AsSpan()), options));
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<T, TWitness>(
            FastPathJsonSerializer.Deserialize<T, TWitness>(Encoding.UTF8.GetBytes(expected)), options));
    }
}
