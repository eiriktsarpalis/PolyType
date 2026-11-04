using PolyType.Examples.JsonSerializer;
using PolyType.ReflectionProvider;
using System.Text.Json;

namespace PolyType.Tests;

public static class PooledJsonBufferTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(300)]
    [InlineData(512)]
    [InlineData(4096)]
    public static void SerializationHandlesGrowthAndAlternatingPayloadSizes(int length)
    {
        var converter = JsonSerializerTS.CreateConverter<string>(ReflectionTypeShapeProvider.Default);
        foreach (string? value in new[] { new string('x', length), "short", "", null, new string('y', length) })
        {
            string expected = JsonSerializer.Serialize(value);
            Assert.Equal(expected, converter.Serialize(value));
            Assert.Equal(expected, converter.SerializeToElement(value).GetRawText());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ReturnedElementsAreIndependentOfReusedBuffer(bool indented)
    {
        var converter = JsonSerializerTS.CreateConverter<string[]>(ReflectionTypeShapeProvider.Default);
        string[] value = [new string('x', 4096), "<value>"];
        JsonElement element = converter.SerializeToElement(value, new() { Indented = indented });
        string expected = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = indented });
        Assert.Equal(expected, element.GetRawText());
        Assert.Equal("[]", converter.Serialize([]));
        Assert.Equal("null", converter.Serialize(null));
        Assert.Equal(expected, element.GetRawText());
    }
}
