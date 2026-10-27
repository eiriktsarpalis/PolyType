using PolyType.FastPathJson.Converters;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class EnumValidationTests
{
    [Theory]
    [InlineData(SignedByteEnum.One, "-129")]
    [InlineData(ByteEnum.One, "256")]
    [InlineData(ShortEnum.One, "32768")]
    [InlineData(UnsignedShortEnum.One, "65536")]
    [InlineData(AliasEnum.First, "2147483648")]
    [InlineData(UnsignedEnum.One, "4294967296")]
    [InlineData(CompositeFlags.One, "9223372036854775808")]
    [InlineData(UnsignedLongEnum.One, "18446744073709551616")]
    public static void NumericEnumValuesRespectUnderlyingRange<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(T witness, string number)
        where T : struct, Enum
    {
        _ = witness;
        Assert.Throws<JsonException>(() => FastPathJsonSerializer.Deserialize<T, EnumConverter<T>>(number));
        Assert.Throws<JsonException>(() => FastPathJsonSerializer.Deserialize<T, EnumConverter<T>>("\"" + number + "\""));
    }

    [Theory]
    [InlineData(LeadingWhitespaceName.Value)]
    [InlineData(TrailingWhitespaceName.Value)]
    [InlineData(EmptyFlagName.Value)]
    [InlineData(CommaFlagName.Value)]
    public static void InvalidEnumMemberNamesAreRejected<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(T value)
        where T : struct, Enum
    {
        TypeInitializationException exception = Assert.Throws<TypeInitializationException>(() =>
            FastPathJsonSerializer.Serialize<T, EnumConverter<T>>(value));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }
}

public enum LeadingWhitespaceName
{
    [JsonStringEnumMemberName(" value")]
    Value,
}

public enum TrailingWhitespaceName
{
    [JsonStringEnumMemberName("value ")]
    Value,
}

[Flags]
public enum EmptyFlagName
{
    [JsonStringEnumMemberName("")]
    Value = 1,
}

[Flags]
public enum CommaFlagName
{
    [JsonStringEnumMemberName("first, second")]
    Value = 1,
}
