using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer.Converters;

internal class JsonObjectConverter<T>(JsonPropertyConverter<T>[] properties) : JsonConverter<T>, IJsonObjectConverter<T>
{
    private readonly JsonPropertyConverter<T>[] _propertiesToWrite = properties.Where(prop => prop.HasGetter).ToArray();

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(T) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        throw new NotSupportedException($"Deserialization for type {typeof(T)} is not supported.");
    }

    public sealed override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        DebugExt.Assert(_propertiesToWrite != null);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        WriteProperties(writer, value, options);
        writer.WriteEndObject();
    }

    public void WriteProperties(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        foreach (JsonPropertyConverter<T> property in _propertiesToWrite)
        {
            writer.WritePropertyName(property.EncodedName);
            property.Write(writer, ref value, options);
        }
    }
}

internal sealed class JsonObjectConverterWithDefaultCtor<T>(Func<T> defaultConstructor, JsonPropertyConverter<T>[] properties) : JsonObjectConverter<T>(properties)
{
    private readonly JsonPropertyDictionary<JsonPropertyConverter<T>> _propertiesToRead = properties.Where(prop => prop.HasSetter).ToJsonPropertyDictionary(p => p.Name);

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(T) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);
        reader.EnsureRead();

        T result = defaultConstructor();
        JsonPropertyDictionary<JsonPropertyConverter<T>> propertiesToRead = _propertiesToRead;

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);

            JsonPropertyConverter<T>? jsonProperty = propertiesToRead.LookupProperty(ref reader);
            reader.EnsureRead();
            
            if (jsonProperty != null)
            {
                jsonProperty.Read(ref reader, ref result, options);
            }
            else
            {
                reader.Skip();
            }

            reader.EnsureRead();
        }

        return result;
    }
}

internal sealed class JsonObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
    Func<TArgumentState> createArgumentState,
    Constructor<TArgumentState, TDeclaringType> createObject,
    JsonPropertyConverter<TArgumentState>[] constructorParameters,
    JsonPropertyConverter<TDeclaringType>[] properties,
    IReadOnlyList<IParameterShape> parameters) : JsonObjectConverter<TDeclaringType>(properties)
    where TArgumentState : IArgumentState
{
    private readonly JsonPropertyDictionary<JsonPropertyConverter<TArgumentState>> _constructorParameters = constructorParameters.ToJsonPropertyDictionary(p => p.Name);

    public sealed override TDeclaringType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TDeclaringType) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartObject);
        reader.EnsureRead();

        JsonPropertyDictionary<JsonPropertyConverter<TArgumentState>> ctorParams = _constructorParameters;
        TArgumentState argumentState = createArgumentState();

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);

            JsonPropertyConverter<TArgumentState>? jsonProperty = ctorParams.LookupProperty(ref reader);
            reader.EnsureRead();

            if (jsonProperty != null)
            {
                jsonProperty.Read(ref reader, ref argumentState, options);
            }
            else
            {
                reader.Skip();
            }

            reader.EnsureRead();
        }

        if (!argumentState.AreRequiredArgumentsSet)
        {
            Helpers.ThrowMissingRequiredArguments(ref argumentState, parameters);
        }

        return createObject(ref argumentState);
    }
}

internal interface IJsonObjectConverter<TUnion>
{
    void WriteProperties(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options);
}