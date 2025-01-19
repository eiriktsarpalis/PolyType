using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class JsonSurrogateConverter<T, TSurrogate>(IMarshaller<T, TSurrogate> marshaller, JsonConverter<TSurrogate> surrogateConverter) : JsonConverter<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        marshaller.FromSurrogate(surrogateConverter.Read(ref reader, typeof(T), options));

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        surrogateConverter.Write(writer, marshaller.ToSurrogate(value)!, options);
}