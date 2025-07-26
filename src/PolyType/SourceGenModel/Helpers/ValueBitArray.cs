using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PolyType.SourceGenModel;

/// <summary>
/// A variant of BitArray that supports subset comparisons.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct ValueBitArray
{
    private readonly byte[] _array;
    private readonly uint _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueBitArray"/> struct.
    /// </summary>
    /// <param name="length">Total number of bits in the array.</param>
    public ValueBitArray(int length)
    {
        if (length < 0)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
        }

        _length = (uint)length;
        _array = new byte[(length + 7) >> 3];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueBitArray"/> struct.
    /// </summary>
    /// <param name="bytes">Byte data used to seed the bit array.</param>
    /// <param name="length">Total number of bits in the array.</param>
    public ValueBitArray(ReadOnlySpan<byte> bytes, int length)
    {
        if (length < 0)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
        }

        if (length > bytes.Length * 8)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(length), "The declared length exceeds the bit capacity of the byte buffer");
        }

        byte[] array = new byte[(length + 7) >> 3];
        bytes[..array.Length].CopyTo(array);
        int remainder = length & 7;
        if (remainder > 0)
        {
            // Normalize by clearing any trailing bits.
            array[^1] &= (byte)((1 << remainder) - 1);
        }

        _array = array;
        _length = (uint)length;
    }

    /// <summary>
    /// Gets the total number of bits tracked by the bit array.
    /// </summary>
    public int Length => (int)_length;

    /// <summary>
    /// Gets a read-only span of bytes representing the bit array.
    /// </summary>
    public ReadOnlySpan<byte> Bytes => _array;

    /// <summary>
    /// Gets or sets the bit in the specified index.
    /// </summary>
    /// <param name="index">The index at which to set the bit.</param>
    /// <returns><see langword="true"/> if the bit is set, or <see langword="false"/> otherwise.</returns>
    public bool this[int index]
    {
        get
        {
            if ((uint)index >= _length)
            {
                return false;
            }

            return (_array[index >> 3] & (1 << (index & 7))) != 0;
        }
        set
        {
            if ((uint)index >= _length)
            {
                return;
            }

            int byteIndex = index >> 3;
            byte mask = (byte)(1 << (index & 7));

            if (value)
            {
                _array[byteIndex] |= mask;
            }
            else
            {
                _array[byteIndex] &= (byte)~mask;
            }
        }
    }

    /// <summary>
    /// Checks if this bit array defines a subset over the other bit array.
    /// </summary>
    /// <param name="other">The other bit array to compare against.</param>
    /// <returns><see cref="bool"/> indicating whether this bit array is a subset of the other.</returns>
    public bool IsSubsetOf(ValueBitArray other)
    {
        if (_length != other._length)
        {
            return false;
        }

        for (int i = 0; i < _array.Length; i++)
        {
            var leftByte = _array[i];
            var rightByte = other._array[i];
            if ((leftByte & rightByte) != leftByte)
            {
                return false;
            }
        }

        return true;
    }
}
