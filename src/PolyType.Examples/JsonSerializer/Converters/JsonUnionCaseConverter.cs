using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal abstract class JsonUnionCaseConverter<TUnion> : JsonConverter<TUnion>
{
    public abstract string Name { get; }

    /// <summary>
    /// Scores this case type against a JSON token for structural matching.
    /// Higher scores indicate better matches. Returns -1 for incompatible tokens.
    /// </summary>
    public virtual int ScoreAgainstToken(JsonTokenType token, ref readonly Utf8JsonReader reader) => 0;

    /// <summary>
    /// Writes the union value directly (without discriminator wrapping) for C# union serialization.
    /// </summary>
    public virtual void WriteDirect(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options) =>
        Write(writer, value, options);
}

internal sealed class JsonUnionCaseConverter<TUnionCase, TUnion>(string name, IMarshaler<TUnionCase, TUnion> marshaler, JsonConverter<TUnionCase> underlying) : JsonUnionCaseConverter<TUnion>
{
    private static ReadOnlySpan<byte> ValuesPropertyName => "$values"u8;
    private readonly IJsonObjectConverter<TUnionCase>? _objectConverter = underlying as IJsonObjectConverter<TUnionCase>;

    public override string Name { get; } = name;

    public override TUnion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (_objectConverter is not null)
        {
            return marshaler.Marshal(underlying.Read(ref reader, typeToConvert, options));
        }

        if (!JsonHelpers.TryAdvanceToProperty(ref reader, ValuesPropertyName))
        {
            throw new JsonException("No $values property found.");
        }

        reader.EnsureRead();
        TUnion? result = marshaler.Marshal(underlying.Read(ref reader, typeToConvert, options));
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
            objConv.WriteProperties(writer, marshaler.Unmarshal(value)!, options);
        }
        else
        {
            writer.WritePropertyName(ValuesPropertyName);
            underlying.Write(writer, marshaler.Unmarshal(value)!, options);
        }
    }

    public override void WriteDirect(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options)
    {
        TUnionCase? caseValue = marshaler.Unmarshal(value);
        underlying.Write(writer, caseValue!, options);
    }

    public override int ScoreAgainstToken(JsonTokenType token, ref readonly Utf8JsonReader reader)
    {
        // Binary token type compatibility check
        Type caseType = typeof(TUnionCase);

        return token switch
        {
            JsonTokenType.Number => IsNumericType(caseType) ? 1 : -1,
            JsonTokenType.String => IsStringType(caseType) ? 1 : -1,
            JsonTokenType.True or JsonTokenType.False => caseType == typeof(bool) ? 1 : -1,
            JsonTokenType.StartArray => IsArrayType(caseType) ? 1 : -1,
            JsonTokenType.StartObject when _objectConverter is not null => ScoreObjectMatch(in reader),
            JsonTokenType.StartObject => 0,
            _ => 0,
        };
    }

    private int ScoreObjectMatch(ref readonly Utf8JsonReader reader)
    {
        // Count how many JSON property names match known properties of this case type
        if (underlying is not JsonObjectConverter<TUnionCase> objectConverter)
        {
            return 0;
        }

        Utf8JsonReader checkpoint = reader;
        int matchCount = 0;

        try
        {
            checkpoint.EnsureRead(); // past StartObject
            while (checkpoint.TokenType == JsonTokenType.PropertyName)
            {
                if (objectConverter.HasProperty(ref checkpoint))
                {
                    matchCount++;
                }

                checkpoint.EnsureRead(); // past property name
                checkpoint.Skip();       // skip value
                checkpoint.EnsureRead(); // to next property or EndObject
            }
        }
        catch (JsonException)
        {
            return 0;
        }

        return matchCount;
    }

    private static bool IsNumericType(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(double) ||
        type == typeof(float) || type == typeof(decimal) || type == typeof(short) ||
        type == typeof(byte) || type == typeof(uint) || type == typeof(ulong);

    private static bool IsStringType(Type type) =>
        type == typeof(string) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
        type == typeof(Guid) || type == typeof(Uri) || type.IsEnum;

    private static bool IsArrayType(Type type) =>
        type != typeof(string) &&
        (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type));
}
