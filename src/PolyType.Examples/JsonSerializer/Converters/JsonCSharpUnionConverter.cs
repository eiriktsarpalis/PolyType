using PolyType.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class JsonCSharpUnionConverter<TUnion>(
    Getter<TUnion, int> getUnionCaseIndex,
    JsonUnionCaseConverter<TUnion>[] unionCaseConverters) : JsonConverter<TUnion>
{
    public override TUnion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TUnion) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        int bestIndex = FindBestMatchingCase(in reader);
        if (bestIndex < 0)
        {
            throw new JsonException($"Unable to match JSON token to any case type of union '{typeof(TUnion)}'.");
        }

        return unionCaseConverters[bestIndex].Read(ref reader, typeToConvert, options);
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
            throw new JsonException($"Unable to determine union case for value of type '{value?.GetType()}'.");
        }

        unionCaseConverters[index].WriteDirect(writer, value, options);
    }

    private int FindBestMatchingCase(ref readonly Utf8JsonReader reader)
    {
        // Simple structural matching: score each case type against the JSON token.
        // For objects, count how many JSON property names match known properties on each candidate.
        // For primitives, check binary token type compatibility.

        Utf8JsonReader snapshot = reader;
        JsonTokenType token = snapshot.TokenType;
        int bestIndex = -1;
        int bestScore = -1;

        for (int i = 0; i < unionCaseConverters.Length; i++)
        {
            int score = unionCaseConverters[i].ScoreAgainstToken(token, ref snapshot);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
