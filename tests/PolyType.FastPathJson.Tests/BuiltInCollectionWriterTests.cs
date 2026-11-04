using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class BuiltInCollectionWriterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void RegisteredCollectionWritersPreserveNullElements(bool indented)
    {
        string?[] array = ["first", null, "<last>"];
        List<string?> list = [.. array];
        Dictionary<BindingFlags, string?> dictionary = new()
        {
            [BindingFlags.Public] = array[0],
            [BindingFlags.Instance] = null,
        };
        JsonWriterOptions options = new() { Indented = indented };
        string expectedArray = JsonSerializer.Serialize(new string?[] { "first", null, "<last>" },
            new JsonSerializerOptions { WriteIndented = indented });
        string expectedDictionary = JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["Public"] = "first",
            ["Instance"] = null,
        }, new JsonSerializerOptions { WriteIndented = indented });
        Assert.Equal(expectedArray,
            FastPathJsonSerializer.Serialize<string?[], BuiltInCollectionsWitness>(array, options));
        Assert.Equal(expectedArray,
            FastPathJsonSerializer.Serialize<List<string?>, BuiltInCollectionsWitness>(list, options));
        Assert.Equal(expectedDictionary,
            FastPathJsonSerializer.Serialize<Dictionary<BindingFlags, string?>, BuiltInCollectionsWitness>(dictionary, options));
        Assert.Equal(expectedArray, FastPathJsonSerializer.Serialize<string?[], BuiltInCollectionsWitness>(
            FastPathJsonSerializer.Deserialize<string?[], BuiltInCollectionsWitness>(expectedArray), options));
        Assert.Equal(expectedArray, FastPathJsonSerializer.Serialize<List<string?>, BuiltInCollectionsWitness>(
            FastPathJsonSerializer.Deserialize<List<string?>, BuiltInCollectionsWitness>(expectedArray), options));
        Assert.Equal(expectedDictionary,
            FastPathJsonSerializer.Serialize<Dictionary<BindingFlags, string?>, BuiltInCollectionsWitness>(
                FastPathJsonSerializer.Deserialize<Dictionary<BindingFlags, string?>, BuiltInCollectionsWitness>(expectedDictionary), options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[]")]
    [InlineData("[1,null,-2]")]
    public static void RegisteredNullableListKeepsItsReadContract(string? json)
    {
        List<int?>? value = json is null ? null : JsonSerializer.Deserialize<List<int?>>(json);
        string expected = json ?? "null";
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<List<int?>, BuiltInCollectionsWitness>(value));
        Assert.Equal(expected, FastPathJsonSerializer.Serialize<List<int?>, BuiltInCollectionsWitness>(
            FastPathJsonSerializer.Deserialize<List<int?>, BuiltInCollectionsWitness>(expected)));
        Assert.Throws<JsonException>(() =>
            FastPathJsonSerializer.Deserialize<List<int?>, BuiltInCollectionsWitness>("{}"));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public static void RegisteredPrimitiveLoopsKeepNumericValidation(float value)
    {
        foreach (bool skipValidation in new[] { false, true })
        {
            Assert.Throws<ArgumentException>(() =>
                FastPathJsonSerializer.Serialize<List<float>, BuiltInCollectionsWitness>([value],
                    new() { SkipValidation = skipValidation }));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void RegisteredWritersPreserveEnumeratorInvalidation(bool dictionary)
    {
        MutatingCollectionElement first = new(42);
        if (dictionary)
        {
            Dictionary<int, MutatingCollectionElement> value = new() { [0] = first };
            first.OnWrite = () => value.Add(1, new(1));
            Assert.Throws<InvalidOperationException>(() =>
                FastPathJsonSerializer.Serialize<Dictionary<int, MutatingCollectionElement>, BuiltInCollectionsWitness>(value));
        }
        else
        {
            List<MutatingCollectionElement> value = [first];
            first.OnWrite = () => value.Add(new(1));
            Assert.Throws<InvalidOperationException>(() =>
                FastPathJsonSerializer.Serialize<List<MutatingCollectionElement>, BuiltInCollectionsWitness>(value));
        }
    }
}

[GenerateJsonFor<string[]>]
[GenerateJsonFor<List<string>>]
[GenerateJsonFor<Dictionary<BindingFlags, string>>]
[GenerateJsonFor<List<int?>>]
[GenerateJsonFor<List<float>>]
[GenerateJsonFor<List<MutatingCollectionElement>>]
[GenerateJsonFor<Dictionary<int, MutatingCollectionElement>>]
public partial class BuiltInCollectionsWitness;

[GenerateJson]
public partial class MutatingCollectionElement(int value)
{
    [JsonIgnore]
    public Action? OnWrite { get; set; }

    public int Value
    {
        get
        {
            OnWrite?.Invoke();

            return value;
        }
    }
}
