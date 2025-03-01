using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal abstract class JsonUnionCaseConverter<TUnion> : JsonConverter<TUnion>
{
    public abstract string Name { get; }
}

internal sealed class JsonUnionCaseConverter<TUnionCase, TUnion>(string name, JsonConverter<TUnionCase> underlying) : JsonUnionCaseConverter<TUnion>
    where TUnionCase : TUnion
{
    private static ReadOnlySpan<byte> ValuesPropertyName => "$values"u8;
    private readonly IJsonObjectConverter<TUnionCase>? _objectConverter = underlying as IJsonObjectConverter<TUnionCase>;

    public override string Name { get; } = name;

    public override TUnion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (_objectConverter is not null)
        {
            return underlying.Read(ref reader, typeToConvert, options);
        }

        if (!JsonHelpers.TryAdvanceToProperty(ref reader, ValuesPropertyName))
        {
            throw new JsonException("No $values property found.");
        }

        reader.EnsureRead();
        TUnion? result = underlying.Read(ref reader, typeToConvert, options);
        reader.EnsureRead();

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            reader.Skip();
            reader.EnsureRead();
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options)
    {
        DebugExt.Assert(value is TUnionCase);
        if (_objectConverter is { } objConv)
        {
            objConv.WriteProperties(writer, (TUnionCase)value, options);
        }
        else
        {
            writer.WritePropertyName(ValuesPropertyName);
            underlying.Write(writer, (TUnionCase)value, options);
        }
    }
}
