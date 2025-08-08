using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class JsonSurrogateConverter<T, TSurrogate>(IMarshaler<T, TSurrogate> marshaler, JsonConverter<TSurrogate> surrogateConverter) : JsonConverter<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        marshaler.Unmarshal(surrogateConverter.Read(ref reader, typeof(T), options));

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        surrogateConverter.Write(writer, marshaler.Marshal(value)!, options);
}