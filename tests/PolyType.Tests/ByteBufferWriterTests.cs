using PolyType.Examples.Utilities;

namespace PolyType.Tests;

public static class ByteBufferWriterTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    public static void InitialCapacityMustBePositive(int initialCapacity)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            nameof(initialCapacity), () => new ByteBufferWriter(initialCapacity));

        Assert.Equal(initialCapacity, Assert.IsType<int>(exception.ActualValue));
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    public static void NegativeAdvancePreservesWrittenData(int count)
    {
        ByteBufferWriter writer = new(4);
        writer.GetSpan()[0] = 42;
        writer.Advance(1);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            nameof(count), () => writer.Advance(count));

        Assert.Equal(count, Assert.IsType<int>(exception.ActualValue));
        Assert.Equal(1, writer.WrittenCount);
        Assert.Equal(4, writer.Capacity);
        Assert.Equal(new byte[] { 42 }, writer.WrittenMemory.ToArray());
    }

    [Theory]
    [InlineData(int.MinValue, false)]
    [InlineData(int.MinValue, true)]
    [InlineData(-1, false)]
    [InlineData(-1, true)]
    public static void NegativeSizeHintsPreserveBuffer(int sizeHint, bool memory)
    {
        ByteBufferWriter writer = new(4);
        writer.GetSpan()[0] = 42;
        writer.Advance(1);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(nameof(sizeHint), () =>
        {
            if (memory)
            {
                writer.GetMemory(sizeHint);
            }
            else
            {
                writer.GetSpan(sizeHint);
            }
        });

        Assert.Equal(sizeHint, Assert.IsType<int>(exception.ActualValue));
        Assert.Equal(1, writer.WrittenCount);
        Assert.Equal(4, writer.Capacity);
        Assert.Equal(new byte[] { 42 }, writer.WrittenMemory.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ZeroSizeHintsStillReturnNonEmptyBuffers(bool memory)
    {
        ByteBufferWriter writer = new(1);
        writer.Advance(1);

        int available = memory ? writer.GetMemory(0).Length : writer.GetSpan(0).Length;

        Assert.True(available > 0);
        Assert.Equal(1, writer.WrittenCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public static void AdvanceRetainsCapacityBoundaries(int written)
    {
        ByteBufferWriter writer = new(4);
        writer.Advance(written);

        Assert.Throws<InvalidOperationException>(() => writer.Advance(writer.FreeCapacity + 1));
        Assert.Equal(written, writer.WrittenCount);

        writer.Advance(writer.FreeCapacity);
        writer.Advance(0);
        Assert.Equal(writer.Capacity, writer.WrittenCount);
    }
}
