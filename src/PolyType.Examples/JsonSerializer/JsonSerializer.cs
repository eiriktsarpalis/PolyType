using PolyType.Abstractions;
using PolyType.Examples.JsonSerializer.Converters;
using PolyType.Examples.Utilities;
using PolyType.Utilities;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyType.Examples.JsonSerializer;

/// <summary>
/// Provides an JSON serialization implementation built on top of PolyType.
/// </summary>
public static partial class JsonSerializerTS
{
    private static readonly JsonSerializerOptions s_options = new();
    private static readonly MultiProviderTypeCache s_converterCaches = new() 
    {
        ValueBuilderFactory = ctx => new Builder(ctx),
        DelayedValueFactory = new DelayedJsonConverterFactory() 
    };

    /// <summary>
    /// Builds a <see cref="JsonConverter{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shape">The shape instance guiding converter construction.</param>
    /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
    public static JsonConverter<T> CreateConverter<T>(ITypeShape<T> shape) => (JsonConverter<T>)s_converterCaches.GetOrAdd(shape)!;

    /// <summary>
    /// Builds an <see cref="JsonConverter{T}"/> instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="typeShapeProvider">The shape provider guiding converter construction.</param>
    /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
    public static JsonConverter<T> CreateConverter<T>(ITypeShapeProvider typeShapeProvider) => (JsonConverter<T>)s_converterCaches.GetOrAdd(typeof(T), typeShapeProvider)!;

    /// <summary>
    /// Builds a <see cref="JsonConverter"/> instance from the reflection-based shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
    [RequiresUnreferencedCode("PolyType reflection provider requires unreferenced code")]
    [RequiresDynamicCode("PolyType reflection provider requires dynamic code")]
    public static JsonConverter<T> CreateConverterUsingReflection<T>() => CreateConverter<T>(ReflectionProvider.ReflectionTypeShapeProvider.Default);

    /// <summary>
    /// Builds a <see cref="JsonConverter{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
    /// <exception cref="NotSupportedException">No source generated implementation for <typeparamref name="T"/> was found.</exception>
#if NET8_0
    [RequiresDynamicCode("Dynamic resolution of IShapeable<T> interface may require dynamic code generation in .NET 8 Native AOT. It is recommended to switch to statically resolved IShapeable<T> APIs or upgrade your app to .NET 9 or later.")]
#endif
    public static JsonConverter<T> CreateConverterUsingSourceGen<T>() =>
        CreateConverter(TypeShapeResolver.ResolveDynamicOrThrow<T>());

    /// <summary>
    /// Creates a JSON marshaling delegate that wraps the specified method shape.
    /// </summary>
    /// <param name="methodShape">The method shape to wrap.</param>
    /// <param name="target">The target object used by the delegate.</param>
    /// <returns>A JSON marshaling delegate.</returns>
    public static JsonFunc CreateJsonFunc(IMethodShape methodShape, object? target = null)
    {
        TypeCache scopedCache = s_converterCaches.GetScopedCache(methodShape.DeclaringType.Provider);
        TypeGenerationContext ctx = scopedCache.CreateGenerationContext();
        return (JsonFunc)methodShape.Accept((Builder)ctx.ValueBuilder!, state: target)!;
    }

    /// <summary>
    /// Creates a JSON marshaling event wrapping the specified event shape.
    /// </summary>
    /// <param name="eventShape">The event shape to wrap.</param>
    /// <param name="target">The target object used by the delegate.</param>
    /// <returns>A JSON event instance.</returns>
    public static JsonEvent CreateJsonEvent(IEventShape eventShape, object? target = null)
    {
        (bool RequireAsync, object? Target) state = (RequireAsync: false, Target: target);
        TypeCache scopedCache = s_converterCaches.GetScopedCache(eventShape.DeclaringType.Provider);
        TypeGenerationContext ctx = scopedCache.CreateGenerationContext();
        return (JsonEvent)eventShape.Accept((Builder)ctx.ValueBuilder!, state)!;
    }

    /// <summary>
    /// Creates an asynchronous JSON marshaling event wrapping the specified event shape.
    /// </summary>
    /// <param name="eventShape">The event shape to wrap.</param>
    /// <param name="target">The target object used by the delegate.</param>
    /// <returns>A JSON event instance.</returns>
    public static AsyncJsonEvent CreateAsyncJsonEvent(IEventShape eventShape, object? target = null)
    {
        (bool RequireAsync, object? Target) state = (RequireAsync: true, Target: target);
        TypeCache scopedCache = s_converterCaches.GetScopedCache(eventShape.DeclaringType.Provider);
        TypeGenerationContext ctx = scopedCache.CreateGenerationContext();
        return (AsyncJsonEvent)eventShape.Accept((Builder)ctx.ValueBuilder!, state)!;
    }

    /// <summary>
    /// Serializes a value to a JSON string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="converter">The converter used to serialize the value.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>A JSON encoded string containing the serialized value.</returns>
    public static string Serialize<T>(this JsonConverter<T> converter, T? value, JsonWriterOptions options = default)
    {
        Utf8JsonWriter writer = JsonHelpers.GetPooledJsonWriter(options, out ByteBufferWriter bufferWriter);
        try
        {
            converter.Serialize(writer, value);
            return JsonHelpers.DecodeFromUtf8(bufferWriter.WrittenSpan);
        }
        finally
        {
            JsonHelpers.ReturnPooledJsonWriter(writer, bufferWriter);
        }
    }

    /// <summary>
    /// Serializes a value to a JSON element using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="converter">The converter used to serialize the value.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>A JSON encoded element containing the serialized value.</returns>
    public static JsonElement SerializeToElement<T>(this JsonConverter<T> converter, T? value, JsonWriterOptions options = default)
    {
        Utf8JsonWriter writer = JsonHelpers.GetPooledJsonWriter(options, out ByteBufferWriter bufferWriter);
        try
        {
            converter.Serialize(writer, value);
            Utf8JsonReader reader = new(bufferWriter.WrittenSpan);
            return JsonElement.ParseValue(ref reader);
        }
        finally
        {
            JsonHelpers.ReturnPooledJsonWriter(writer, bufferWriter);
        }
    }

    /// <summary>
    /// Serializes a value to JSON using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="converter">The converter used to serialize the value.</param>
    /// <param name="writer">The JSON writer where the value will be written.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A JSON encoded string containing the serialized value.</returns>
    public static void Serialize<T>(this JsonConverter<T> converter, Utf8JsonWriter writer, T? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            converter.Write(writer, value!, s_options);
        }

        writer.Flush();
    }

    /// <summary>
    /// Deserializes a value from a JSON string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="json">The JSON encoding to be deserialized.</param>
    /// <param name="options">The options object guiding JSON reading.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(this JsonConverter<T> converter, [StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> json, JsonReaderOptions options = default)
    {
        Span<byte> utf8Json = JsonHelpers.DecodeToUtf8UsingRentedBuffer(json, out byte[] rentedBuffer);
        try
        {
            return converter.Deserialize(utf8Json, options);
        }
        finally
        {
            utf8Json.Clear();
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Deserializes a value from a JSON element using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="element">The JSON element to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(this JsonConverter<T> converter, JsonElement element)
    {
        ReadOnlySpan<byte> rawBytes = element.GetRawValue().Span;
        return converter.Deserialize(rawBytes);
    }

#if !NET
    /// <summary>
    /// Deserializes a value from a JSON string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="json">The JSON encoding to be deserialized.</param>
    /// <param name="options">The options object guiding JSON reading.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(this JsonConverter<T> converter, [StringSyntax(StringSyntaxAttribute.Json)] string json, JsonReaderOptions options = default) =>
        Deserialize(converter, json.AsSpan(), options);
#endif

    /// <summary>
    /// Deserializes a value from a JSON string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="utf8Json">The JSON encoding to be deserialized.</param>
    /// <param name="options">The options object guiding JSON reading.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(this JsonConverter<T> converter, ReadOnlySpan<byte> utf8Json, JsonReaderOptions options = default)
    {
        Utf8JsonReader reader = new(utf8Json, options);
        reader.EnsureRead();
        return default(T) is null && reader.TokenType is JsonTokenType.Null ? default : converter.Read(ref reader, typeof(T), s_options);
    }

    /// <summary>
    /// Invokes the JSON function using parameters defined in a JSON string.
    /// </summary>
    /// <param name="function">The function to invoke.</param>
    /// <param name="json">A JSON object containing the function parameters.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A <see cref="JsonElement"/> containing the serialized result.</returns>
    public static ValueTask<JsonElement> Invoke(this JsonFunc function, [StringSyntax(StringSyntaxAttribute.Json)] string json, CancellationToken cancellationToken = default)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return function(document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value), cancellationToken);
    }

#if NET
    /// <summary>
    /// Builds an <see cref="JsonConverter{T}"/> instance using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <returns>An <see cref="JsonConverter{T}"/> instance.</returns>
    public static JsonConverter<T> CreateConverter<T>() where T : IShapeable<T> =>
        CreateConverter(T.GetTypeShape());

    /// <summary>
    /// Serializes a value to a JSON string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>An JSON encoded string containing the serialized value.</returns>
    public static string Serialize<T>(T? value, JsonWriterOptions options = default) where T : IShapeable<T> => 
        JsonSerializerCache<T, T>.Value.Serialize(value, options);

    /// <summary>
    /// Deserializes a value from a JSON string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="json">The JSON encoding to be deserialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> json, JsonReaderOptions options = default) where T : IShapeable<T> => 
        JsonSerializerCache<T, T>.Value.Deserialize(json, options);

    /// <summary>
    /// Serializes a value to a JSON string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>A JSON encoded string containing the serialized value.</returns>
    public static string Serialize<T, TProvider>(T? value, JsonWriterOptions options = default) where TProvider : IShapeable<T> => 
        JsonSerializerCache<T, TProvider>.Value.Serialize(value, options);

    /// <summary>
    /// Deserializes a value from a JSON string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="json">The JSON encoding to be deserialized.</param>
    /// <param name="options">The options object guiding JSON formatting.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T, TProvider>([StringSyntax(StringSyntaxAttribute.Json)] ReadOnlySpan<char> json, JsonReaderOptions options = default) where TProvider : IShapeable<T> => 
        JsonSerializerCache<T, TProvider>.Value.Deserialize(json, options);

    private static class JsonSerializerCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static JsonConverter<T> Value => s_value ??= CreateConverter(TProvider.GetTypeShape());
        private static JsonConverter<T>? s_value;
    }
#endif
}