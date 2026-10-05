using PolyType;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public sealed class CitmCatalog
{
    public static CitmCatalog[] CreateBatch()
    {
        using Stream resource = typeof(CitmCatalog).Assembly.GetManifestResourceStream("citm_catalog.json.gz")
            ?? throw new InvalidOperationException("The embedded CITM catalog dataset is missing.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        gzip.CopyTo(buffer);
        byte[] json = buffer.ToArray();
        JsonNode original = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("The CITM catalog dataset is null.");
        var catalogs = new CitmCatalog[5];
        for (int i = 0; i < catalogs.Length; i++)
        {
            catalogs[i] = JsonSerializer.Deserialize(json, StjContext.Default.CitmCatalog)
                ?? throw new InvalidOperationException("The CITM catalog could not be deserialized.");
            if (catalogs[i].Events.Count != 184 || catalogs[i].Performances.Length != 243 ||
                !JsonNode.DeepEquals(original, JsonSerializer.SerializeToNode(catalogs[i], StjContext.Default.CitmCatalog)))
            {
                throw new InvalidOperationException("The typed CITM catalog does not preserve the complete dataset.");
            }
        }

        return catalogs;
    }

    [JsonPropertyName("areaNames"), PropertyShape(Name = "areaNames")]
    public Dictionary<string, string> AreaNames { get; set; } = [];

    [JsonPropertyName("audienceSubCategoryNames"), PropertyShape(Name = "audienceSubCategoryNames")]
    public Dictionary<string, string> AudienceSubCategoryNames { get; set; } = [];

    [JsonPropertyName("blockNames"), PropertyShape(Name = "blockNames")]
    public Dictionary<string, string> BlockNames { get; set; } = [];

    [JsonPropertyName("events"), PropertyShape(Name = "events")]
    public Dictionary<string, CitmEvent> Events { get; set; } = [];

    [JsonPropertyName("performances"), PropertyShape(Name = "performances")]
    public CitmPerformance[] Performances { get; set; } = [];

    [JsonPropertyName("seatCategoryNames"), PropertyShape(Name = "seatCategoryNames")]
    public Dictionary<string, string> SeatCategoryNames { get; set; } = [];

    [JsonPropertyName("subTopicNames"), PropertyShape(Name = "subTopicNames")]
    public Dictionary<string, string> SubTopicNames { get; set; } = [];

    [JsonPropertyName("subjectNames"), PropertyShape(Name = "subjectNames")]
    public Dictionary<string, string> SubjectNames { get; set; } = [];

    [JsonPropertyName("topicNames"), PropertyShape(Name = "topicNames")]
    public Dictionary<string, string> TopicNames { get; set; } = [];

    [JsonPropertyName("topicSubTopics"), PropertyShape(Name = "topicSubTopics")]
    public Dictionary<string, int[]> TopicSubTopics { get; set; } = [];

    [JsonPropertyName("venueNames"), PropertyShape(Name = "venueNames")]
    public Dictionary<string, string> VenueNames { get; set; } = [];
}

public sealed class CitmEvent
{
    [JsonPropertyName("description"), PropertyShape(Name = "description")]
    public string? Description { get; set; }

    [JsonPropertyName("id"), PropertyShape(Name = "id")]
    public int Id { get; set; }

    [JsonPropertyName("logo"), PropertyShape(Name = "logo")]
    public string? Logo { get; set; }

    [JsonPropertyName("name"), PropertyShape(Name = "name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("subTopicIds"), PropertyShape(Name = "subTopicIds")]
    public int[] SubTopicIds { get; set; } = [];

    [JsonPropertyName("subjectCode"), PropertyShape(Name = "subjectCode")]
    public string? SubjectCode { get; set; }

    [JsonPropertyName("subtitle"), PropertyShape(Name = "subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("topicIds"), PropertyShape(Name = "topicIds")]
    public int[] TopicIds { get; set; } = [];
}

public sealed class CitmPerformance
{
    [JsonPropertyName("eventId"), PropertyShape(Name = "eventId")]
    public int EventId { get; set; }

    [JsonPropertyName("id"), PropertyShape(Name = "id")]
    public int Id { get; set; }

    [JsonPropertyName("logo"), PropertyShape(Name = "logo")]
    public string? Logo { get; set; }

    [JsonPropertyName("name"), PropertyShape(Name = "name")]
    public string? Name { get; set; }

    [JsonPropertyName("prices"), PropertyShape(Name = "prices")]
    public CitmPrice[] Prices { get; set; } = [];

    [JsonPropertyName("seatCategories"), PropertyShape(Name = "seatCategories")]
    public CitmSeatCategory[] SeatCategories { get; set; } = [];

    [JsonPropertyName("seatMapImage"), PropertyShape(Name = "seatMapImage")]
    public string? SeatMapImage { get; set; }

    [JsonPropertyName("start"), PropertyShape(Name = "start")]
    public long Start { get; set; }

    [JsonPropertyName("venueCode"), PropertyShape(Name = "venueCode")]
    public string VenueCode { get; set; } = "";
}

public sealed class CitmPrice
{
    [JsonPropertyName("amount"), PropertyShape(Name = "amount")]
    public int Amount { get; set; }

    [JsonPropertyName("audienceSubCategoryId"), PropertyShape(Name = "audienceSubCategoryId")]
    public int AudienceSubCategoryId { get; set; }

    [JsonPropertyName("seatCategoryId"), PropertyShape(Name = "seatCategoryId")]
    public int SeatCategoryId { get; set; }
}

public sealed class CitmSeatCategory
{
    [JsonPropertyName("areas"), PropertyShape(Name = "areas")]
    public CitmArea[] Areas { get; set; } = [];

    [JsonPropertyName("seatCategoryId"), PropertyShape(Name = "seatCategoryId")]
    public int SeatCategoryId { get; set; }
}

public sealed class CitmArea
{
    [JsonPropertyName("areaId"), PropertyShape(Name = "areaId")]
    public int AreaId { get; set; }

    [JsonPropertyName("blockIds"), PropertyShape(Name = "blockIds")]
    public int[] BlockIds { get; set; } = [];
}
