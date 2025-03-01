using PolyType.Abstractions;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class JsonUnionConverter<TUnion>(
    Getter<TUnion, int> getUnionCaseIndex,
    JsonConverter<TUnion> baseConverter,
    JsonUnionCaseConverter<TUnion>[] unionCaseConverters) : JsonConverter<TUnion>
{
    private readonly JsonPropertyDictionary<JsonUnionCaseConverter<TUnion>> _unionCaseIndex = unionCaseConverters.ToJsonPropertyDictionary(p => p.Name);

    public override TUnion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TUnion) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        if (reader.TokenType is not JsonTokenType.StartObject)
        {
            return baseConverter.Read(ref reader, typeToConvert, options);
        }

        Utf8JsonReader checkpoint = reader;
        if (!JsonHelpers.TryAdvanceToProperty(ref reader, JsonHelpers.DiscriminatorPropertyName))
        {
            reader = checkpoint;
            return baseConverter.Read(ref reader, typeToConvert, options);
        }

        Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);
        reader.EnsureRead();
        reader.EnsureTokenType(JsonTokenType.String);

        if (_unionCaseIndex.LookupProperty(ref reader) is not { } unionCaseConverter)
        {
            throw new JsonException("Unrecognized type discriminator.");
        }

        reader = checkpoint;
        return unionCaseConverter.Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        int index = getUnionCaseIndex(ref value);
        if (index < 0)
        {
            baseConverter.Write(writer, value, options);
            return;
        }

        JsonUnionCaseConverter<TUnion> converter = unionCaseConverters[index];

        writer.WriteStartObject();
        writer.WriteString(JsonHelpers.DiscriminatorPropertyName, converter.Name);
        converter.Write(writer, value, options);
        writer.WriteEndObject();
    }
}