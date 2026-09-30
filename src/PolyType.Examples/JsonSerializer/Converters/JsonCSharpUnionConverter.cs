using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class JsonCSharpUnionConverter<TUnion> : JsonConverter<TUnion>, ISchematizedJsonConverter
{
    // Nested C# unions remain writable but contribute no non-null categories to their parent's read dispatch.
    public JsonValueType ValueType => JsonValueType.None;

    private readonly Getter<TUnion, int> _getUnionCaseIndex;
    private readonly JsonCSharpUnionCaseConverter<TUnion>[] _cases;
    private readonly Func<TUnion?> _createNull;

    public JsonCSharpUnionConverter(
        Getter<TUnion, int> getUnionCaseIndex,
        JsonCSharpUnionCaseConverter<TUnion>[] cases)
    {
        _getUnionCaseIndex = getUnionCaseIndex;
        _cases = cases;
        _createNull = cases.FirstOrDefault(c => c.IsNullable) is { } nullableCase
            ? nullableCase.CreateNull
            : static () => throw new JsonException($"The union {typeof(TUnion)} has no nullable case.");
    }

    public override bool HandleNull => true;

    public override TUnion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Dictionary<JsonValueType, int> caseIndicesByValueType = CaseIndicesByValueType;
        if (reader.TokenType is JsonTokenType.Null)
        {
            // Construct the nullable case directly: returning null or invoking a surrogate's null conversion isn't equivalent.
            return _createNull();
        }

        JsonValueType valueType = reader.TokenType switch
        {
            JsonTokenType.StartObject => JsonValueType.Object,
            JsonTokenType.StartArray => JsonValueType.Array,
            JsonTokenType.String => JsonValueType.String,
            JsonTokenType.Number => JsonValueType.Number,
            JsonTokenType.True or JsonTokenType.False => JsonValueType.Boolean,
            _ => JsonValueType.None,
        };

        if (!caseIndicesByValueType.TryGetValue(valueType, out int caseIndex))
        {
            Throw(reader.TokenType);
            [DoesNotReturn]
            static void Throw(JsonTokenType tokenType) =>
                throw new JsonException($"The JSON token {tokenType} does not match a case of {typeof(TUnion)}.");
        }

        return _cases[caseIndex].Read(ref reader, typeof(TUnion), options);
    }

    public override void Write(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options)
    {
        _ = CaseIndicesByValueType;
        int caseIndex = _getUnionCaseIndex(ref value);
        _cases[caseIndex].Write(writer, value, options);
    }

    private Dictionary<JsonValueType, int> CreateCaseIndicesByValueType()
    {
        Dictionary<JsonValueType, int> result = new();
        ReadOnlySpan<JsonValueType> valueTypes = [
            JsonValueType.Object,
            JsonValueType.Array,
            JsonValueType.String,
            JsonValueType.Number,
            JsonValueType.Boolean,
        ];

        foreach (JsonValueType valueType in valueTypes)
        {
            int caseIndex = FindCaseIndex(valueType);
            if (caseIndex >= 0)
            {
                result[valueType] = caseIndex;
            }
        }

        return result;
    }

    private int FindCaseIndex(JsonValueType valueType)
    {
        int matchingCaseIndex = -1;
        int caseIndex = 0;
        foreach (JsonCSharpUnionCaseConverter<TUnion> unionCase in _cases)
        {
            if ((unionCase.ValueTypes & valueType) != JsonValueType.None)
            {
                if (matchingCaseIndex != -1)
                {
                    string conflictingTypes = string.Join(", ", _cases
                        .Where(c => (c.ValueTypes & valueType) != JsonValueType.None)
                        .Select(c => $"'{c.CaseType}'"));
                    throw new InvalidOperationException(
                        $"The union '{typeof(TUnion)}' has multiple cases matching JSON {valueType} values: {conflictingTypes}.");
                }

                matchingCaseIndex = caseIndex;
            }

            caseIndex++;
        }

        return matchingCaseIndex;
    }

    // Resolve payload categories on first use, once recursive converters are complete.
    private Dictionary<JsonValueType, int> CaseIndicesByValueType
    {
        get
        {
            if (field is { } result)
            {
                return result;
            }

            result = CreateCaseIndicesByValueType();
            return Interlocked.CompareExchange(ref field, result, null) ?? result;
        }
    }
}

internal abstract class JsonCSharpUnionCaseConverter<TUnion>(
    bool isNullable) : JsonConverter<TUnion>
{
    public abstract Type CaseType { get; }
    public abstract JsonValueType ValueTypes { get; }
    public bool IsNullable { get; } = isNullable;
    public abstract TUnion? CreateNull();
}

internal sealed class JsonCSharpUnionCaseConverter<TUnionCase, TUnion>(
    JsonConverter<TUnionCase> underlying,
    IMarshaler<TUnionCase, TUnion> marshaler,
    bool isNullable) : JsonCSharpUnionCaseConverter<TUnion>(isNullable)
{
    public override Type CaseType => typeof(TUnionCase);
    public override JsonValueType ValueTypes => JsonSerializerTS.GetJsonValueType(underlying);
    public override TUnion? CreateNull() => marshaler.Marshal(default);

    public override TUnion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        marshaler.Marshal(underlying.Read(ref reader, typeof(TUnionCase), options));

    public override void Write(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options) =>
        underlying.Write(writer, marshaler.Unmarshal(value)!, options);
}
