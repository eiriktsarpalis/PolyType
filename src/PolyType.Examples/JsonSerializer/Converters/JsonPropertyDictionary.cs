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

        scoped ReadOnlySpan<byte> source;
        byte[]? rentedBuffer = null;
        int bytesWritten = 0;

        if (!reader.ValueIsEscaped)
        {
            source = reader.ValueSpan;
        }
        else
        {
            Span<byte> tmpBuffer = reader.ValueSpan.Length <= 128
                ? stackalloc byte[128]
                : rentedBuffer = ArrayPool<byte>.Shared.Rent(reader.ValueSpan.Length);

            bytesWritten = reader.CopyString(tmpBuffer);
            source = tmpBuffer[..bytesWritten];
        }

        _dict.TryGetValue(source, out TValue? result);

        if (rentedBuffer != null)
        {
            rentedBuffer.AsSpan(0, bytesWritten).Clear();
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }

        return result;
    }
}