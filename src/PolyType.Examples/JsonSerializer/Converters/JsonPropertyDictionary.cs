using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.JsonSerializer.Converters;

internal static class JsonPropertyDictionary
{
    public static JsonPropertyDictionary<TValue> ToJsonPropertyDictionary<TValue>(this IEnumerable<TValue> source, Func<TValue, string> keySelector) where TValue : class
        => new(source.Select(t => new KeyValuePair<string, TValue>(keySelector(t), t)));
}

internal sealed class JsonPropertyDictionary<TValue>(IEnumerable<KeyValuePair<string, TValue>> entries) where TValue : class
{
    private readonly SpanDictionary<byte, TValue> _dict = entries.ToSpanDictionary(p => Encoding.UTF8.GetBytes(p.Key), p => p.Value, ByteSpanEqualityComparer.Ordinal);

    public TValue? LookupProperty(ref Utf8JsonReader reader)
    {
        Debug.Assert(reader.TokenType is JsonTokenType.PropertyName or JsonTokenType.String);
        Debug.Assert(!reader.HasValueSequence);

        if (_dict.Count == 0)
        {
            return null;
        }

        if (!reader.ValueIsEscaped)
        {
            _dict.TryGetValue(reader.ValueSpan, out TValue? result);
            return result;
        }

        return LookupUnescapedProperty(ref reader);
    }

    private TValue? LookupUnescapedProperty(ref Utf8JsonReader reader)
    {
        int length = reader.ValueSpan.Length;
        byte[]? rentedBuffer = null;
        Span<byte> buffer = length <= 128
            ? stackalloc byte[128]
            : rentedBuffer = ArrayPool<byte>.Shared.Rent(length);

        try
        {
            int bytesWritten = reader.CopyString(buffer);
            _dict.TryGetValue(buffer[..bytesWritten], out TValue? result);
            return result;
        }
        finally
        {
            if (rentedBuffer is not null)
            {
                buffer[..length].Clear();
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }
}