﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using PolyType.Abstractions;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.JsonSerializer.Converters;

internal class JsonEnumerableConverter<TEnumerable, TElement>(JsonConverter<TElement> elementConverter, IEnumerableTypeShape<TEnumerable, TElement> typeShape) : JsonConverter<TEnumerable>
{
    private static readonly bool s_isIList = typeof(IList<TElement>).IsAssignableFrom(typeof(TEnumerable));
    private protected readonly JsonConverter<TElement> _elementConverter = elementConverter;
    private readonly Func<TEnumerable, IEnumerable<TElement>> _getEnumerable = typeShape.GetGetPotentiallyBlockingEnumerable();

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        throw new NotSupportedException($"Deserialization not supported for type {typeof(TEnumerable)}.");
    }

    public sealed override void Write(Utf8JsonWriter writer, TEnumerable value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();

        if (s_isIList)
        {
            WriteElementsAsIList(writer, (IList<TElement>)value, options);
        }
        else
        {
            WriteElementsAsEnumerable(writer, value, options);
        }

        writer.WriteEndArray();
    }

    private void WriteElementsAsIList(Utf8JsonWriter writer, IList<TElement> value, JsonSerializerOptions options)
    {
        JsonConverter<TElement> elementConverter = _elementConverter;
        int count = value.Count;

        for (int i = 0; i < count; i++)
        {
            elementConverter.Write(writer, value[i], options);
        }
    }

    private void WriteElementsAsEnumerable(Utf8JsonWriter writer, TEnumerable value, JsonSerializerOptions options)
    {
        JsonConverter<TElement> elementConverter = _elementConverter;
        foreach (TElement element in _getEnumerable(value))
        {
            elementConverter.Write(writer, element, options);
        }
    }
}

internal sealed class JsonMutableEnumerableConverter<TEnumerable, TElement>(
    JsonConverter<TElement> elementConverter,
    IEnumerableTypeShape<TEnumerable, TElement> typeShape,
    MutableCollectionConstructor<TElement, TEnumerable> createObject,
    EnumerableAppender<TEnumerable, TElement> appender) : JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, typeShape)
{
    private readonly EnumerableAppender<TEnumerable, TElement> _appender = appender;

    public override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);

        TEnumerable result = createObject();
        reader.EnsureRead();

        JsonConverter<TElement> elementConverter = _elementConverter;
        EnumerableAppender<TEnumerable, TElement> appender = _appender;

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
            appender(ref result, element!);
            reader.EnsureRead();
        }

        return result;
    }
}

internal sealed class JsonParameterizedEnumerableConverter<TEnumerable, TElement>(
    JsonConverter<TElement> elementConverter,
    IEnumerableTypeShape<TEnumerable, TElement> typeShape,
    ParameterizedCollectionConstructor<TElement, TElement, TEnumerable> spanConstructor)
    : JsonEnumerableConverter<TEnumerable, TElement>(elementConverter, typeShape)
{
    public sealed override TEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {   
        if (default(TEnumerable) is null && reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.EnsureTokenType(JsonTokenType.StartArray);
        reader.EnsureRead();

        using PooledList<TElement> buffer = new();
        JsonConverter<TElement> elementConverter = _elementConverter;

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
            buffer.Add(element!);
            reader.EnsureRead();
        }

        return spanConstructor(buffer.AsSpan());
    }
}

internal sealed class JsonMDArrayConverter<TArray, TElement>(JsonConverter<TElement> elementConverter, int rank) : JsonConverter<TArray>
{
    [ThreadStatic] private static int[]? _dimensions;

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The Array.CreateInstance method generates TArray instances.")]
    public override TArray? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        int[] dimensions = _dimensions ??= new int[rank];
        dimensions.AsSpan().Fill(-1);
        PooledList<TElement> buffer = new();
        try
        {
            ReadSubArray(ref reader, ref buffer, dimensions, options);
            dimensions.AsSpan().Replace(-1, 0);
            Array result = Array.CreateInstance(typeof(TElement), dimensions);
            using Helpers.UnsafeArraySpan<TElement> unsafeArraySpan = new(result);
            buffer.AsSpan().CopyTo(unsafeArraySpan.Span);
            return (TArray)(object)result;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    public override void Write(Utf8JsonWriter writer, TArray value, JsonSerializerOptions options)
    {
        var array = (Array)(object)value!;
        Debug.Assert(rank == array.Rank);

        int[] dimensions = _dimensions ??= new int[rank];
        for (int i = 0; i < rank; i++) 
        { 
            dimensions[i] = array.GetLength(i); 
        }

        using Helpers.UnsafeArraySpan<TElement> unsafeArraySpan = new(array);
        WriteSubArray(writer, dimensions, unsafeArraySpan.Span, options);
    }

    private void ReadSubArray(
        ref Utf8JsonReader reader,
        ref PooledList<TElement> buffer,
        Span<int> dimensions,
        JsonSerializerOptions options)
    {
        Debug.Assert(dimensions.Length > 0);
        reader.EnsureTokenType(JsonTokenType.StartArray);
        reader.EnsureRead();
        
        int dimension = 0;
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            if (dimensions.Length > 1)
            {
                ReadSubArray(ref reader, ref buffer, dimensions[1..], options);
            }
            else
            {
                TElement? element = elementConverter.Read(ref reader, typeof(TElement), options);
                buffer.Add(element!);
            }
            
            reader.EnsureRead();
            dimension++;
        }
        
        if (dimensions[0] < 0)
        {
            dimensions[0] = dimension;
        }
        else if (dimensions[0] != dimension)
        {
            JsonHelpers.ThrowJsonException("The deserialized jagged array was not rectangular.");
        }
    }
    
    private void WriteSubArray(
        Utf8JsonWriter writer,
        ReadOnlySpan<int> dimensions,
        ReadOnlySpan<TElement> elements,
        JsonSerializerOptions options)
    {
        Debug.Assert(dimensions.Length > 0);
        
        writer.WriteStartArray();

        int outerDim = dimensions[0];
        if (dimensions.Length > 1 && outerDim > 0)
        {
            int subArrayLength = elements.Length / outerDim;
            for (int i = 0; i < outerDim; i++)
            {
                WriteSubArray(writer, dimensions[1..], elements[..subArrayLength], options);
                elements = elements[subArrayLength..];
            }
        }
        else
        {
            for (int i = 0; i < outerDim; i++)
            {
                elementConverter.Write(writer, elements[i], options);
            }
        }
            
        writer.WriteEndArray();
    }
}
