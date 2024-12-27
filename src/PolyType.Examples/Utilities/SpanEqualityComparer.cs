using System.Diagnostics;

namespace PolyType.Examples.Utilities;

/// <summary>Defines a span-based equality comparer.</summary>
public interface ISpanEqualityComparer<T>
{
    /// <summary>Gets the hash code for the specified buffer.</summary>
    int GetHashCode(ReadOnlySpan<T> buffer);
    /// <summary>Checks the two buffers for equality.</summary>
    bool Equals(ReadOnlySpan<T> x, ReadOnlySpan<T> y);
}

/// <summary>Defines an equality comparer for byte spans.</summary>
public static class ByteSpanEqualityComparer
{
    /// <summary>Gets the default ordinal equality comparer for byte spans.</summary>
    public static ISpanEqualityComparer<byte> Ordinal { get; } = new OrdinalEqualityComparer();

    private sealed class OrdinalEqualityComparer : ISpanEqualityComparer<byte>
    {
        public bool Equals(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
            => x.SequenceEqual(y);

        public int GetHashCode(ReadOnlySpan<byte> buffer)
        {
            var hc = new HashCode();
            hc.AddBytes(buffer);
            return hc.ToHashCode();
        }
    }
}