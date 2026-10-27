using PolyType.FastPathJson.Converters;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class DirectEnumConverterTests
{
    public static IEnumerable<object[]> Values()
    {
        yield return [SignedByteEnum.One];
        yield return [(SignedByteEnum)sbyte.MinValue];
        yield return [ByteEnum.One];
        yield return [(ByteEnum)byte.MaxValue];
        yield return [ShortEnum.One];
        yield return [(ShortEnum)short.MinValue];
        yield return [UnsignedShortEnum.One];
        yield return [(UnsignedShortEnum)ushort.MaxValue];
        yield return [AliasEnum.First];
        yield return [AliasEnum.Alias];
        yield return [AliasEnum.FIRST];
        yield return [(AliasEnum)int.MinValue];
        yield return [UnsignedEnum.One];
        yield return [(UnsignedEnum)uint.MaxValue];
        yield return [CompositeFlags.One | CompositeFlags.Four];
        yield return [CompositeFlags.Pair | CompositeFlags.Four];
        yield return [CompositeFlags.All];
        yield return [CompositeFlags.High | CompositeFlags.One];
        yield return [(CompositeFlags)8];
        yield return [UnsignedLongEnum.One];
        yield return [(UnsignedLongEnum)ulong.MaxValue];
        yield return [NamedEnum.First];
        yield return [NamedEnum.Lower];
        yield return [NamedEnum.Upper];
        yield return [NamedEnum.NumericName];
        yield return [NamedEnum.EmptyName];
        yield return [NamedEnum.DefaultName];
        yield return [(NamedEnum)99];
    }

    [Theory]
    [MemberData(nameof(Values))]
    public static void DirectEnumValuesAndKeysMatchStringEnumRepresentation<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(T value)
        where T : struct, Enum
    {
        foreach (JavaScriptEncoder? encoder in new[]
        {
            null, JavaScriptEncoder.Default, JavaScriptEncoder.UnsafeRelaxedJsonEscaping, JavaScriptEncoder.Create(UnicodeRanges.None),
        })
        {
            JsonSerializerOptions options = Options<T>(encoder);
            JsonWriterOptions writerOptions = new() { Encoder = encoder };
            string expected = JsonSerializer.Serialize(value, options);
            string actual = FastPathJsonSerializer.Serialize<T, EnumConverter<T>>(value, writerOptions);
            Assert.Equal(expected, actual);
            Assert.Equal(value, FastPathJsonSerializer.Deserialize<T, EnumConverter<T>>(actual.AsSpan()));
            Assert.Equal(value, FastPathJsonSerializer.Deserialize<T, EnumConverter<T>>(Encoding.UTF8.GetBytes(actual)));

            Dictionary<T, int> dictionary = new() { [value] = 42 };
            string expectedDictionary = JsonSerializer.Serialize(dictionary, options);
            string actualDictionary = FastPathJsonSerializer.Serialize<Dictionary<T, int>,
                DictionaryConverter<T, int, EnumConverter<T>, Int32Converter>>(dictionary, writerOptions);
            Assert.Equal(expectedDictionary, actualDictionary);
            var restored = FastPathJsonSerializer.Deserialize<Dictionary<T, int>,
                DictionaryConverter<T, int, EnumConverter<T>, Int32Converter>>(actualDictionary);
            Assert.NotNull(restored);
            Assert.Equal(42, restored[value]);
        }
    }

    [Theory]
    [InlineData("\"<value>\"")]
    [InlineData("\"\\u003Cvalue\\u003E\"")]
    [InlineData("\" defaultname \"")]
    [InlineData("\"same\"")]
    [InlineData("\"SAME\"")]
    [InlineData("\"1\"")]
    [InlineData("\"\"")]
    [InlineData("\"99\"")]
    [InlineData("99")]
    public static void NamedEnumParsingPreservesExactNamesAndNumericFallback(string json)
    {
        NamedEnum expected = JsonSerializer.Deserialize<NamedEnum>(json, Options<NamedEnum>());
        Assert.Equal(expected, FastPathJsonSerializer.Deserialize<NamedEnum, EnumConverter<NamedEnum>>(json));
        Assert.Equal(expected, FastPathJsonSerializer.Deserialize<NamedEnum, EnumConverter<NamedEnum>>(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("\"One, Four\"")]
    [InlineData("\"Pair, Four\"")]
    [InlineData("\" high, ONE \"")]
    [InlineData("\"One,\"")]
    [InlineData("\"7\"")]
    [InlineData("-1")]
    [InlineData("0")]
    public static void FlagsParsingPreservesCombinationsAndCanonicalOrdering(string json)
    {
        JsonSerializerOptions options = Options<CompositeFlags>();
        CompositeFlags expected = JsonSerializer.Deserialize<CompositeFlags>(json, options);
        CompositeFlags actual = FastPathJsonSerializer.Deserialize<CompositeFlags, EnumConverter<CompositeFlags>>(json);
        Assert.Equal(expected, actual);
        Assert.Equal(JsonSerializer.Serialize(expected, options),
            FastPathJsonSerializer.Serialize<CompositeFlags, EnumConverter<CompositeFlags>>(actual));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("1.5")]
    [InlineData("-1")]
    [InlineData("256")]
    [InlineData("\"missing\"")]
    public static void InvalidEnumTokensAndOutOfRangeNumbersAreRejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ByteEnum>(json, Options<ByteEnum>()));
        Assert.Throws<JsonException>(() => FastPathJsonSerializer.Deserialize<ByteEnum, EnumConverter<ByteEnum>>(json));
    }

    private static JsonSerializerOptions Options<T>(JavaScriptEncoder? encoder = null) where T : struct, Enum
    {
        JsonSerializerOptions options = new() { Encoder = encoder };
        options.Converters.Add(new JsonStringEnumConverter<T>());

        return options;
    }
}

public enum SignedByteEnum : sbyte { One = 1 }
public enum ByteEnum : byte { One = 1 }
public enum ShortEnum : short { One = 1 }
public enum UnsignedShortEnum : ushort { One = 1 }
public enum AliasEnum { First = 1, Alias = 1, FIRST = 2 }
public enum UnsignedEnum : uint { One = 1 }
public enum UnsignedLongEnum : ulong { One = 1 }

[Flags]
public enum CompositeFlags : long
{
    None = 0,
    One = 1,
    Two = 2,
    Pair = 3,
    Four = 4,
    High = long.MinValue,
    All = -1,
}

public enum NamedEnum
{
    [JsonStringEnumMemberName("<value>")]
    First = 1,
    [JsonStringEnumMemberName("same")]
    Lower = 2,
    [JsonStringEnumMemberName("SAME")]
    Upper = 3,
    [JsonStringEnumMemberName("1")]
    NumericName = 4,
    [JsonStringEnumMemberName("")]
    EmptyName = 5,
    DefaultName = 6,
}
