using PolyType.Tests;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class PropertyNameCachingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ObjectPropertyNamesHonorWriterEncoder(bool indented)
    {
        CachedNamePayload value = new();
        OtherCachedNamePayload other = new(42, "<other>");
        foreach (JsonWriterOptions options in WriterOptions(indented))
        {
            JsonSerializerOptions referenceOptions = ReferenceOptions(options);
            string expected = JsonSerializer.Serialize(value, referenceOptions);
            Assert.Equal(expected, FastPathJsonSerializer.Serialize(value, options));
            Assert.Equal(expected, FastPathJsonSerializer.Serialize<CachedNamePayload, CachedNamesWitness>(value, options));
            Assert.Equal(expected, FastPathJsonSerializer.Serialize(
                FastPathJsonSerializer.Deserialize<CachedNamePayload>(expected), options));
            string otherExpected = JsonSerializer.Serialize(other, referenceOptions);
            Assert.Equal(otherExpected,
                FastPathJsonSerializer.Serialize<OtherCachedNamePayload, CachedNamesWitness>(other, options));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TuplePropertyNamesHonorWriterEncoder(bool indented)
    {
        (int, string) value = (42, "<text>");
        foreach (JsonWriterOptions options in WriterOptions(indented))
        {
            JsonSerializerOptions referenceOptions = ReferenceOptions(options);
            referenceOptions.IncludeFields = true;
            string expected = JsonSerializer.Serialize(value, referenceOptions);
            Assert.Equal(expected, FastPathJsonSerializer.Serialize<(int, string), CachedNamesWitness>(value, options));
            Assert.Equal(expected, FastPathJsonSerializer.Serialize<(int, string), CachedNamesWitness>(
                FastPathJsonSerializer.Deserialize<(int, string), CachedNamesWitness>(expected), options));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void HierarchyPropertyNamesHonorWriterEncoder(bool indented)
    {
        Tree value = new Tree.Node(42, new Tree.Leaf(), new Tree.Leaf());
        Dictionary<string, object> reference = new()
        {
            ["$type"] = "node",
            ["Value"] = 42,
            ["Left"] = new Dictionary<string, string> { ["$type"] = "leaf" },
            ["Right"] = new Dictionary<string, string> { ["$type"] = "leaf" },
        };
        foreach (JsonWriterOptions options in WriterOptions(indented))
        {
            string expected = JsonSerializer.Serialize(reference, ReferenceOptions(options));
            Assert.Equal(expected, FastPathJsonSerializer.Serialize<Tree, TestCasesWitness>(value, options));
            Assert.Equal(expected, FastPathJsonSerializer.Serialize<Tree, TestCasesWitness>(
                FastPathJsonSerializer.Deserialize<Tree, TestCasesWitness>(expected), options));
        }
    }

    private static JsonSerializerOptions ReferenceOptions(JsonWriterOptions options) =>
        new() { Encoder = options.Encoder, WriteIndented = options.Indented };

    private static IEnumerable<JsonWriterOptions> WriterOptions(bool indented)
    {
        JavaScriptEncoder?[] encoders =
        [
            JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            null,
            JavaScriptEncoder.Default,
            JavaScriptEncoder.Create(UnicodeRanges.None),
            JavaScriptEncoder.Create(UnicodeRanges.All),
            null,
            JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            JavaScriptEncoder.Default,
        ];
        foreach (JavaScriptEncoder? encoder in encoders)
        {
            yield return new() { Encoder = encoder, Indented = indented };
        }
    }
}

public class CachedNamePayloadBase
{
    [JsonPropertyName("inherited<name>"), JsonPropertyOrder(-1)]
    public int Inherited { get; set; } = 42;
}

[GenerateJson]
public partial class CachedNamePayload : CachedNamePayloadBase
{
    [JsonPropertyName("ascii"), JsonPropertyOrder(0)]
    public string Plain { get; set; } = "<plain>";

    [JsonPropertyName("<name>"), JsonPropertyOrder(1)]
    public string Html { get; set; } = "<html>";

    [JsonPropertyName("quote\"\\\n\0"), JsonPropertyOrder(2)]
    public string Escaped { get; set; } = "\"\\\n";

    [JsonPropertyName("\u00E9\uD83E\uDD2F"), JsonPropertyOrder(3)]
    public string Unicode { get; set; } = "\u00E9\uD83E\uDD2F";

    [JsonPropertyName(""), JsonPropertyOrder(4)]
    public string Empty { get; set; } = "";
}

public record OtherCachedNamePayload(
    [property: JsonPropertyName("<other>")] int Number,
    [property: JsonPropertyName("quote\"")] string Text);

[GenerateJsonFor<CachedNamePayload>]
[GenerateJsonFor<OtherCachedNamePayload>]
[GenerateJsonFor<(int, string)>]
public partial class CachedNamesWitness;
