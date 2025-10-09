namespace PolyType.Roslyn.Tests;

using Xunit;
using System.Collections;

public static class TestSuite
{
    [Fact]
    public static void SimpleTest()
    {
        Assert.Equal(2, 1 + 1);
    }
}