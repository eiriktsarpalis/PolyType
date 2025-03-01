using PolyType.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class JsonOptionalConverter<TOptional, TElement>(
    JsonConverter<TElement> elementConverter,
    OptionDeconstructor<TOptional, TElement> deconstructor,
    Func<TOptional> createNone,
    Func<TElement, TOptional> createSome) : JsonConverter<TOptional>
{
    public override TOptional Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return createNone();
        }

        TElement? element = elementConverter.Read(ref reader, typeToConvert, options);
        return createSome(element!);
    }

    public override void Write(Utf8JsonWriter writer, TOptional value, JsonSerializerOptions options)
    {
        if (!deconstructor(value, out TElement? element))
        {
            writer.WriteNullValue();
            return;
        }

        elementConverter.Write(writer, element, options);
    }
}
