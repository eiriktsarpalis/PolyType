using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class JsonPropertyDictionary<TDeclaringType>(IEnumerable<JsonPropertyConverter<TDeclaringType>> propertiesToRead)
{
    private readonly SpanDictionary<byte, JsonPropertyConverter<TDeclaringType>> _dict = propertiesToRead.ToSpanDictionary(p => Encoding.UTF8.GetBytes(p.Name), ByteSpanEqualityComparer.Ordinal);

    public JsonPropertyConverter<TDeclaringType>? LookupProperty(ref Utf8JsonReader reader)
    {
        Debug.Assert(reader.TokenType is JsonTokenType.PropertyName);
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

        _dict.TryGetValue(source, out JsonPropertyConverter<TDeclaringType>? result);

        if (rentedBuffer != null)
        {
            rentedBuffer.AsSpan(0, bytesWritten).Clear();
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }

        return result;
    }
}