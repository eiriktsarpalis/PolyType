using BenchmarkDotNet.Attributes;
using PolyType;
using PolyType.Examples.JsonSerializer;
using PolyType.ReflectionProvider;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

public abstract class JsonBenchmark<T>
{
    protected T Value = default!;
    protected byte[] Utf8JsonValue = null!;
    protected JsonTypeInfo<T> StjReflectionInfo = null!;
    protected JsonTypeInfo<T> StjSourceGenInfo = null!;
    protected JsonTypeInfo<T> StjFastPathInfo = null!;
    protected JsonConverter<T> PolyTypeReflection = null!;
    protected JsonConverter<T> PolyTypeSourceGen = null!;
    protected readonly ArrayBufferWriter<byte> Buffer = new();
    protected readonly Utf8JsonWriter Writer;

    protected JsonBenchmark() => Writer = new(Buffer);

    [GlobalSetup]
    public void Setup()
    {
        Value = JsonBenchmarkData.Create<T>();
        StjReflectionInfo = (JsonTypeInfo<T>)JsonSerializerOptions.Default.GetTypeInfo(typeof(T));
        StjSourceGenInfo = (JsonTypeInfo<T>)StjContext.Default.GetTypeInfo(typeof(T))!;
        StjFastPathInfo = (JsonTypeInfo<T>)StjContext_FastPath.Default.GetTypeInfo(typeof(T))!;
        PolyTypeReflection = JsonSerializerTS.CreateConverter<T>(ReflectionTypeShapeProvider.Default);
        PolyTypeSourceGen = JsonSerializerTS.CreateConverter<T>(JsonBenchmarkWitness.GeneratedTypeShapeProvider);
        Utf8JsonValue = JsonSerializer.SerializeToUtf8Bytes(Value, StjReflectionInfo);

        if ((typeof(T) == typeof(CitmCatalog[]) || typeof(T) == typeof(LargePoco[])) && Utf8JsonValue.Length < 2_000_000)
        {
            throw new InvalidOperationException($"The {typeof(T)} payload must be at least 2 MB.");
        }

        // Validate equivalent output and round trips, and warm the reusable output buffer.
        foreach (JsonTypeInfo<T> info in new[] { StjReflectionInfo, StjSourceGenInfo, StjFastPathInfo })
        {
            JsonSerializer.Serialize(Writer, Value, info);
            ValidateOutput();
        }

        foreach (JsonTypeInfo<T> info in new[] { StjReflectionInfo, StjSourceGenInfo })
        {
            T roundTrip = JsonSerializer.Deserialize(Utf8JsonValue, info)
                ?? throw new InvalidOperationException($"Unexpected null round trip for {typeof(T)}.");
            JsonSerializer.Serialize(Writer, roundTrip, info);
            ValidateOutput();
        }

        foreach (JsonConverter<T> converter in new[] { PolyTypeReflection, PolyTypeSourceGen })
        {
            converter.Serialize(Writer, Value);
            ValidateOutput();
            converter.Serialize(Writer, converter.Deserialize(Utf8JsonValue));
            ValidateOutput();
        }
    }

    private void ValidateOutput()
    {
        Writer.Flush();
        if (!JsonNode.DeepEquals(JsonNode.Parse(Utf8JsonValue), JsonNode.Parse(Buffer.WrittenSpan)))
        {
            throw new InvalidOperationException($"JSON output or round trip differs for {typeof(T)}.");
        }

        Reset();
    }

    protected void Reset()
    {
        Buffer.ResetWrittenCount();
        Writer.Reset();
    }

    [GlobalCleanup]
    public void Cleanup() => Writer.Dispose();

    public int PayloadBytes => Utf8JsonValue.Length;
}

[MemoryDiagnoser(false)]
[HideColumns("Job", "Error", "StdDev", "Median", "RatioSD")]
[GenericTypeArguments(typeof(int))]
[GenericTypeArguments(typeof(string))]
[GenericTypeArguments(typeof(int[]))]
[GenericTypeArguments(typeof(List<int>))]
[GenericTypeArguments(typeof(Dictionary<string, int>))]
[GenericTypeArguments(typeof(MyPoco))]
[GenericTypeArguments(typeof(LargePoco))]
[GenericTypeArguments(typeof(CitmCatalog[]))]
[GenericTypeArguments(typeof(LargePoco[]))]
public class JsonSerializeBenchmark<T> : JsonBenchmark<T>
{
    [Benchmark(Baseline = true)]
    public void Serialize_StjReflection()
    {
        JsonSerializer.Serialize(Writer, Value, StjReflectionInfo);
        Reset();
    }

    [Benchmark]
    public void Serialize_StjSourceGen()
    {
        JsonSerializer.Serialize(Writer, Value, StjSourceGenInfo);
        Reset();
    }

    [Benchmark]
    public void Serialize_StjSourceGen_FastPath()
    {
        JsonSerializer.Serialize(Writer, Value, StjFastPathInfo);
        Reset();
    }

    [Benchmark]
    public void Serialize_PolyTypeReflection()
    {
        PolyTypeReflection.Serialize(Writer, Value);
        Reset();
    }

    [Benchmark]
    public void Serialize_PolyTypeSourceGen()
    {
        PolyTypeSourceGen.Serialize(Writer, Value);
        Reset();
    }
}

[MemoryDiagnoser(false)]
[HideColumns("Job", "Error", "StdDev", "Median", "RatioSD")]
[GenericTypeArguments(typeof(int))]
[GenericTypeArguments(typeof(string))]
[GenericTypeArguments(typeof(int[]))]
[GenericTypeArguments(typeof(List<int>))]
[GenericTypeArguments(typeof(Dictionary<string, int>))]
[GenericTypeArguments(typeof(MyPoco))]
[GenericTypeArguments(typeof(LargePoco))]
[GenericTypeArguments(typeof(CitmCatalog[]))]
[GenericTypeArguments(typeof(LargePoco[]))]
public class JsonDeserializeBenchmark<T> : JsonBenchmark<T>
{
    [Benchmark(Baseline = true)]
    public T? Deserialize_StjReflection()
        => JsonSerializer.Deserialize(Utf8JsonValue, StjReflectionInfo);

    [Benchmark]
    public T? Deserialize_StjSourceGen()
        => JsonSerializer.Deserialize(Utf8JsonValue, StjSourceGenInfo);

    [Benchmark]
    public T? Deserialize_PolyTypeReflection()
        => PolyTypeReflection.Deserialize(Utf8JsonValue);

    [Benchmark]
    public T? Deserialize_PolyTypeSourceGen()
        => PolyTypeSourceGen.Deserialize(Utf8JsonValue);
}

public static class JsonBenchmarkData
{
    public static T Create<T>()
    {
        object value = typeof(T) switch
        {
            Type t when t == typeof(int) => 42,
            Type t when t == typeof(string) => "Hello, PolyType!",
            Type t when t == typeof(int[]) => Enumerable.Range(0, 256).ToArray(),
            Type t when t == typeof(List<int>) => Enumerable.Range(0, 256).ToList(),
            Type t when t == typeof(Dictionary<string, int>) => Enumerable.Range(0, 64).ToDictionary(i => $"key{i}", i => i),
            Type t when t == typeof(MyPoco) => new MyPoco(@string: "myString") { List = [1, 2, 3], Dict = new() { ["key1"] = 42, ["key2"] = -1 } },
            Type t when t == typeof(LargePoco) => CreateLargePoco(42),
            Type t when t == typeof(CitmCatalog[]) => CitmCatalog.CreateBatch(),
            Type t when t == typeof(LargePoco[]) => Enumerable.Range(0, 4096).Select(CreateLargePoco).ToArray(),
            _ => throw new NotSupportedException($"No JSON benchmark fixture for {typeof(T)}."),
        };

        return (T)value;
    }

    private static LargePoco CreateLargePoco(int id) => new()
    {
        Id = id,
        Name = $"Record {id}",
        Description = "A representative record with scalar properties and nested collections.",
        IsActive = id % 2 == 0,
        Count = 100 + id,
        Price = 19.95m,
        Rating = 4.5,
        Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CorrelationId = new Guid("12345678-1234-5678-9abc-123456789abc"),
        Category = "benchmark",
        Region = "Europe",
        City = "Oslo",
        Street = "Example Street",
        PostalCode = "0123",
        Latitude = 59.91,
        Longitude = 10.75,
        IsVerified = true,
        Version = 3,
        Priority = 2,
        Owner = "PolyType",
        Tags = ["json", "serialization", "benchmark"],
        Scores = Enumerable.Range(0, 16).ToArray(),
        Metadata = new() { ["first"] = 1, ["second"] = 2, ["third"] = 3 },
        Detail = new MyPoco(@string: "nested") { List = [1, 2, 3], Dict = new() { ["key"] = 42 } },
    };
}

[GenerateShape]
public partial class MyPoco
{
    public MyPoco(bool @bool = true, string @string = "str")
    {
        Bool = @bool;
        String = @string;
    }

    public bool Bool { get; }
    public string String { get; }
    public List<int>? List { get; set; }
    public Dictionary<string, int>? Dict { get; set; }
}

public class LargePoco
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; }
    public int Count { get; set; }
    public decimal Price { get; set; }
    public double Rating { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid CorrelationId { get; set; }
    public string Category { get; set; } = "";
    public string Region { get; set; } = "";
    public string City { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsVerified { get; set; }
    public int Version { get; set; }
    public int Priority { get; set; }
    public string Owner { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public int[] Scores { get; set; } = [];
    public Dictionary<string, int> Metadata { get; set; } = [];
    public MyPoco Detail { get; set; } = new();
}

[GenerateShapeFor<int>]
[GenerateShapeFor<string>]
[GenerateShapeFor<int[]>]
[GenerateShapeFor<List<int>>]
[GenerateShapeFor<Dictionary<string, int>>]
[GenerateShapeFor<MyPoco>]
[GenerateShapeFor<LargePoco>]
[GenerateShapeFor<CitmCatalog[]>]
[GenerateShapeFor<LargePoco[]>]
public partial class JsonBenchmarkWitness;

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(MyPoco))]
[JsonSerializable(typeof(LargePoco))]
[JsonSerializable(typeof(CitmCatalog[]))]
[JsonSerializable(typeof(LargePoco[]))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
public partial class StjContext : JsonSerializerContext;

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(MyPoco))]
[JsonSerializable(typeof(LargePoco))]
[JsonSerializable(typeof(CitmCatalog[]))]
[JsonSerializable(typeof(LargePoco[]))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization)]
public partial class StjContext_FastPath : JsonSerializerContext;
