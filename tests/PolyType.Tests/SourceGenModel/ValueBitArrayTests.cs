using System;
using PolyType.SourceGenModel;

namespace PolyType.Tests.SourceGenModel;

public static class ValueBitArrayTests
{
    [Fact]
    public static void Constructor_WithLength_CreatesArrayWithCorrectLength()
    {
        // Arrange & Act
        var bitArray = new ValueBitArray(10);

        // Assert
        Assert.Equal(10, bitArray.Length);
    }

    [Fact]
    public static void Constructor_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ValueBitArray(-1));
    }

    [Fact]
    public static void Constructor_WithZeroLength_CreatesEmptyArray()
    {
        // Arrange & Act
        var bitArray = new ValueBitArray(0);

        // Assert
        Assert.Equal(0, bitArray.Length);
        Assert.True(bitArray.Bytes.IsEmpty);
    }

    [Fact]
    public static void Constructor_WithBytesAndLength_InitializesCorrectly()
    {
        // Arrange
        byte[] bytes = [0b10101010, 0b11110000];
        int length = 12;

        // Act
        var bitArray = new ValueBitArray(bytes, length);

        // Assert
        Assert.Equal(length, bitArray.Length);
        Assert.Equal(2, bitArray.Bytes.Length);
        Assert.True(bitArray[1]); // Second bit should be set
        Assert.False(bitArray[0]); // First bit should not be set
    }

    [Fact]
    public static void Constructor_WithBytesAndNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        byte[] bytes = [0xFF];

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ValueBitArray(bytes, -1));
    }

    [Fact]
    public static void Constructor_WithLengthExceedingByteCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        byte[] bytes = [0xFF]; // 1 byte = 8 bits capacity

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new ValueBitArray(bytes, 9));
    }

    [Fact]
    public static void Constructor_WithBytesAndLength_NormalizesTrailingBits()
    {
        // Arrange
        byte[] bytes = [0xFF]; // All bits set
        int length = 5; // Only first 5 bits should be valid

        // Act
        var bitArray = new ValueBitArray(bytes, length);

        // Assert
        // The trailing 3 bits should be cleared
        Assert.Equal(0b00011111, bitArray.Bytes[0]);
    }

    [Fact]
    public static void Indexer_Get_WithValidIndex_ReturnsCorrectBit()
    {
        // Arrange
        byte[] bytes = [0b10101010];
        var bitArray = new ValueBitArray(bytes, 8);

        // Act & Assert
        Assert.False(bitArray[0]); // LSB is 0
        Assert.True(bitArray[1]);  // Second bit is 1
        Assert.False(bitArray[2]); // Third bit is 0
        Assert.True(bitArray[3]);  // Fourth bit is 1
    }

    [Fact]
    public static void Indexer_Get_WithIndexOutOfRange_ReturnsFalse()
    {
        // Arrange
        var bitArray = new ValueBitArray(5);

        // Act & Assert
        Assert.False(bitArray[10]); // Index beyond length
        Assert.False(bitArray[-1]); // Negative index (treated as very large uint)
    }

    [Fact]
    public static void Indexer_Set_WithValidIndex_UpdatesBitCorrectly()
    {
        // Arrange
        var bitArray = new ValueBitArray(8);

        // Act
        bitArray[0] = true;
        bitArray[3] = true;
        bitArray[7] = true;

        // Assert
        Assert.True(bitArray[0]);
        Assert.False(bitArray[1]);
        Assert.False(bitArray[2]);
        Assert.True(bitArray[3]);
        Assert.False(bitArray[4]);
        Assert.False(bitArray[5]);
        Assert.False(bitArray[6]);
        Assert.True(bitArray[7]);
    }

    [Fact]
    public static void Indexer_Set_WithIndexOutOfRange_DoesNothing()
    {
        // Arrange
        var bitArray = new ValueBitArray(5);

        // Act (should not throw)
        bitArray[10] = true;
        bitArray[-1] = true;

        // Assert - all bits should still be false
        for (int i = 0; i < bitArray.Length; i++)
        {
            Assert.False(bitArray[i]);
        }
    }

    [Fact]
    public static void Indexer_SetFalse_ClearsBitCorrectly()
    {
        // Arrange
        var bitArray = new ValueBitArray(8);
        bitArray[3] = true; // Set a bit first

        // Act
        bitArray[3] = false; // Clear the bit

        // Assert
        Assert.False(bitArray[3]);
    }

    [Fact]
    public static void IsSubsetOf_WithEqualArrays_ReturnsTrue()
    {
        // Arrange
        byte[] bytes = [0b10101010];
        var bitArray1 = new ValueBitArray(bytes, 8);
        var bitArray2 = new ValueBitArray(bytes, 8);

        // Act
        bool result = bitArray1.IsSubsetOf(bitArray2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public static void IsSubsetOf_WithDifferentLengths_ReturnsFalse()
    {
        // Arrange
        var bitArray1 = new ValueBitArray(5);
        var bitArray2 = new ValueBitArray(10);

        // Act
        bool result = bitArray1.IsSubsetOf(bitArray2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public static void IsSubsetOf_WithActualSubset_ReturnsTrue()
    {
        // Arrange
        var bitArray1 = new ValueBitArray(8);
        var bitArray2 = new ValueBitArray(8);
        
        bitArray1[1] = true;
        bitArray1[3] = true;
        
        bitArray2[1] = true;
        bitArray2[3] = true;
        bitArray2[5] = true; // Additional bit set in superset

        // Act
        bool result = bitArray1.IsSubsetOf(bitArray2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public static void IsSubsetOf_WithNotSubset_ReturnsFalse()
    {
        // Arrange
        var bitArray1 = new ValueBitArray(8);
        var bitArray2 = new ValueBitArray(8);
        
        bitArray1[1] = true;
        bitArray1[3] = true;
        bitArray1[5] = true; // This bit is not set in bitArray2
        
        bitArray2[1] = true;
        bitArray2[3] = true;

        // Act
        bool result = bitArray1.IsSubsetOf(bitArray2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public static void IsSubsetOf_WithEmptyArrays_ReturnsTrue()
    {
        // Arrange
        var bitArray1 = new ValueBitArray(0);
        var bitArray2 = new ValueBitArray(0);

        // Act
        bool result = bitArray1.IsSubsetOf(bitArray2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public static void IsSubsetOf_WithMultiByteArrays_WorksCorrectly()
    {
        // Arrange
        var bitArray1 = new ValueBitArray(16);
        var bitArray2 = new ValueBitArray(16);
        
        // Set some bits across multiple bytes
        bitArray1[1] = true;   // First byte
        bitArray1[9] = true;   // Second byte
        
        bitArray2[1] = true;   // First byte
        bitArray2[9] = true;   // Second byte
        bitArray2[15] = true;  // Additional bit in second byte

        // Act
        bool result = bitArray1.IsSubsetOf(bitArray2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public static void Bytes_Property_ReturnsCorrectSpan()
    {
        // Arrange
        byte[] initialBytes = [0b10101010, 0b11110000];
        var bitArray = new ValueBitArray(initialBytes, 16);

        // Act
        var bytes = bitArray.Bytes;

        // Assert
        Assert.Equal(2, bytes.Length);
        Assert.Equal(0b10101010, bytes[0]);
        Assert.Equal(0b11110000, bytes[1]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(16, 2)]
    [InlineData(17, 3)]
    [InlineData(64, 8)]
    [InlineData(65, 9)]
    public static void Constructor_CalculatesCorrectByteArraySize(int bitLength, int expectedByteCount)
    {
        // Act
        var bitArray = new ValueBitArray(bitLength);

        // Assert
        Assert.Equal(expectedByteCount, bitArray.Bytes.Length);
    }

    [Fact]
    public static void IsSubsetOf_WithAllBitsSet_WorksCorrectly()
    {
        // Arrange
        var subset = new ValueBitArray(8);
        var superset = new ValueBitArray(8);
        
        // Set all bits in superset
        for (int i = 0; i < 8; i++)
        {
            superset[i] = true;
        }
        
        // Set some bits in subset
        subset[1] = true;
        subset[4] = true;
        subset[7] = true;

        // Act
        bool result = subset.IsSubsetOf(superset);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public static void IsSubsetOf_SelfReference_ReturnsTrue()
    {
        // Arrange
        var bitArray = new ValueBitArray(8);
        bitArray[1] = true;
        bitArray[3] = true;

        // Act
        bool result = bitArray.IsSubsetOf(bitArray);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public static void ByteArrayAccess_AfterModification_ReflectsChanges()
    {
        // Arrange
        var bitArray = new ValueBitArray(8);

        // Act
        bitArray[0] = true;
        bitArray[7] = true;

        // Assert
        var bytes = bitArray.Bytes;
        Assert.Equal(0b10000001, bytes[0]); // LSB and MSB set
    }
}