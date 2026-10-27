using PolyType.Examples.Utilities;

namespace PolyType.Tests;

public static class ByteBufferWriterCapacityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void OversizedRequestsPreserveWrittenData(bool memory)
    {
        ByteBufferWriter writer = new(4);
        writer.GetSpan()[0] = 42;
        writer.Advance(1);

        Assert.Throws<OutOfMemoryException>(() =>
        {
            if (memory)
            {
                writer.GetMemory(int.MaxValue);
            }
            else
            {
                writer.GetSpan(int.MaxValue);
            }
        });

        Assert.Equal(4, writer.Capacity);
        Assert.Equal(1, writer.WrittenCount);
        Assert.Equal(new byte[] { 42 }, writer.WrittenMemory.ToArray());
    }
}
