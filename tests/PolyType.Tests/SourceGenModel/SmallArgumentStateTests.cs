using System;
using PolyType.SourceGenModel;

namespace PolyType.Tests.SourceGenModel;

public static class SmallArgumentStateTests
{
    public struct TestArguments
    {
        public int Value1;
        public string Value2;
        public bool Value3;
    }

    [Fact]
    public static void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var arguments = new TestArguments { Value1 = 42 };
        int count = 5;
        ulong requiredMask = 0b10101; // Arguments 0, 2, and 4 are required

        // Act
        var state = new SmallArgumentState<TestArguments>(arguments, count, requiredMask);

        // Assert
        Assert.Equal(count, state.Count);
        Assert.Equal(42, state.Arguments.Value1);
        Assert.False(state.AreRequiredArgumentsSet); // No arguments marked as set yet
    }

    [Fact]
    public static void Constructor_WithCountGreaterThan64_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var arguments = new TestArguments();
        int count = 65;
        ulong requiredMask = 0;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new SmallArgumentState<TestArguments>(arguments, count, requiredMask));
    }

    [Fact]
    public static void Constructor_WithCount64_Succeeds()
    {
        // Arrange
        var arguments = new TestArguments();
        int count = 64;
        ulong requiredMask = 0;

        // Act
        var state = new SmallArgumentState<TestArguments>(arguments, count, requiredMask);

        // Assert
        Assert.Equal(64, state.Count);
    }

    [Fact]
    public static void Constructor_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var arguments = new TestArguments();
        int count = -1;
        ulong requiredMask = 0;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new SmallArgumentState<TestArguments>(arguments, count, requiredMask));
    }

    [Fact]
    public static void Constructor_WithZeroCount_Succeeds()
    {
        // Arrange
        var arguments = new TestArguments();
        int count = 0;
        ulong requiredMask = 0;

        // Act
        var state = new SmallArgumentState<TestArguments>(arguments, count, requiredMask);

        // Assert
        Assert.Equal(0, state.Count);
        Assert.True(state.AreRequiredArgumentsSet); // Vacuously true for empty set
    }

    [Fact]
    public static void IsArgumentSet_InitiallyAllArgumentsAreNotSet()
    {
        // Arrange
        var arguments = new TestArguments();
        var state = new SmallArgumentState<TestArguments>(arguments, 5, 0);

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            Assert.False(state.IsArgumentSet(i));
        }
    }

    [Fact]
    public static void IsArgumentSet_WithIndexOutOfRange_ReturnsFalse()
    {
        // Arrange
        var arguments = new TestArguments();
        var state = new SmallArgumentState<TestArguments>(arguments, 5, 0);

        // Act & Assert
        Assert.False(state.IsArgumentSet(5));  // At boundary
        Assert.False(state.IsArgumentSet(10)); // Beyond boundary
        Assert.False(state.IsArgumentSet(-1)); // Negative index (treated as very large uint)
    }

    [Fact]
    public static void MarkArgumentSet_SetsArgumentCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        var state = new SmallArgumentState<TestArguments>(arguments, 5, 0);

        // Act
        state.MarkArgumentSet(0);
        state.MarkArgumentSet(2);
        state.MarkArgumentSet(4);

        // Assert
        Assert.True(state.IsArgumentSet(0));
        Assert.False(state.IsArgumentSet(1));
        Assert.True(state.IsArgumentSet(2));
        Assert.False(state.IsArgumentSet(3));
        Assert.True(state.IsArgumentSet(4));
    }

    [Fact]
    public static void MarkArgumentSet_WithIndexOutOfRange_DoesNothing()
    {
        // Arrange
        var arguments = new TestArguments();
        var state = new SmallArgumentState<TestArguments>(arguments, 5, 0);

        // Act (should not throw)
        state.MarkArgumentSet(5);
        state.MarkArgumentSet(10);
        state.MarkArgumentSet(-1);

        // Assert - no arguments should be set
        for (int i = 0; i < 5; i++)
        {
            Assert.False(state.IsArgumentSet(i));
        }
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WithNoRequiredArguments_ReturnsTrue()
    {
        // Arrange
        var arguments = new TestArguments();
        var state = new SmallArgumentState<TestArguments>(arguments, 5, 0); // No required arguments

        // Act & Assert
        Assert.True(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WithSomeRequiredArguments_InitiallyReturnsFalse()
    {
        // Arrange
        var arguments = new TestArguments();
        ulong requiredMask = 0b10101; // Arguments 0, 2, and 4 are required
        var state = new SmallArgumentState<TestArguments>(arguments, 5, requiredMask);

        // Act & Assert
        Assert.False(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WhenAllRequiredArgumentsSet_ReturnsTrue()
    {
        // Arrange
        var arguments = new TestArguments();
        ulong requiredMask = 0b10101; // Arguments 0, 2, and 4 are required
        var state = new SmallArgumentState<TestArguments>(arguments, 5, requiredMask);

        // Act
        state.MarkArgumentSet(0);
        state.MarkArgumentSet(2);
        state.MarkArgumentSet(4);

        // Assert
        Assert.True(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WhenSomeRequiredArgumentsMissing_ReturnsFalse()
    {
        // Arrange
        var arguments = new TestArguments();
        ulong requiredMask = 0b10101; // Arguments 0, 2, and 4 are required
        var state = new SmallArgumentState<TestArguments>(arguments, 5, requiredMask);

        // Act
        state.MarkArgumentSet(0);
        state.MarkArgumentSet(2);
        // Missing argument 4

        // Assert
        Assert.False(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WithExtraArgumentsSet_StillReturnsTrue()
    {
        // Arrange
        var arguments = new TestArguments();
        ulong requiredMask = 0b101; // Arguments 0 and 2 are required
        var state = new SmallArgumentState<TestArguments>(arguments, 5, requiredMask);

        // Act
        state.MarkArgumentSet(0);
        state.MarkArgumentSet(1); // Extra argument
        state.MarkArgumentSet(2);
        state.MarkArgumentSet(3); // Extra argument

        // Assert
        Assert.True(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void Arguments_Property_AllowsModification()
    {
        // Arrange
        var arguments = new TestArguments { Value1 = 42, Value2 = "test" };
        var state = new SmallArgumentState<TestArguments>(arguments, 3, 0);

        // Act
        state.Arguments.Value1 = 100;
        state.Arguments.Value3 = true;

        // Assert
        Assert.Equal(100, state.Arguments.Value1);
        Assert.Equal("test", state.Arguments.Value2);
        Assert.True(state.Arguments.Value3);
    }

    [Fact]
    public static void MarkArgumentSet_WithAllBitsInUlong_WorksCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        var state = new SmallArgumentState<TestArguments>(arguments, 64, 0);

        // Act - Set all 64 arguments
        for (int i = 0; i < 64; i++)
        {
            state.MarkArgumentSet(i);
        }

        // Assert
        for (int i = 0; i < 64; i++)
        {
            Assert.True(state.IsArgumentSet(i));
        }
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WithAllBitsRequired_WorksCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        ulong requiredMask = ulong.MaxValue; // All 64 bits required
        var state = new SmallArgumentState<TestArguments>(arguments, 64, requiredMask);

        // Initially no arguments set
        Assert.False(state.AreRequiredArgumentsSet);

        // Act - Set all arguments
        for (int i = 0; i < 64; i++)
        {
            state.MarkArgumentSet(i);
        }

        // Assert
        Assert.True(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void MarkArgumentSet_CanSetSameArgumentMultipleTimes()
    {
        // Arrange
        var arguments = new TestArguments();
        var state = new SmallArgumentState<TestArguments>(arguments, 3, 0);

        // Act
        state.MarkArgumentSet(1);
        state.MarkArgumentSet(1); // Set again
        state.MarkArgumentSet(1); // Set again

        // Assert
        Assert.True(state.IsArgumentSet(1));
        Assert.False(state.IsArgumentSet(0));
        Assert.False(state.IsArgumentSet(2));
    }

    [Theory]
    [InlineData(0, 0b1)]
    [InlineData(1, 0b10)]
    [InlineData(2, 0b100)]
    [InlineData(5, 0b100000)]
    [InlineData(63, 0x8000000000000000UL)]
    public static void MarkArgumentSet_SetsSingleBitCorrectly(int index, ulong _)
    {
        // Arrange
        var arguments = new TestArguments();
        var state = new SmallArgumentState<TestArguments>(arguments, 64, 0);

        // Act
        state.MarkArgumentSet(index);

        // Assert
        Assert.True(state.IsArgumentSet(index));
        
        // Verify only the expected bit is set by checking adjacent bits
        if (index > 0) Assert.False(state.IsArgumentSet(index - 1));
        if (index < 63) Assert.False(state.IsArgumentSet(index + 1));
    }

    [Fact]
    public static void StructBehavior_IsValueType()
    {
        // Arrange
        var arguments1 = new TestArguments { Value1 = 42 };
        var state1 = new SmallArgumentState<TestArguments>(arguments1, 3, 0);
        
        // Act - Modify copy
        var state2 = state1;
        state2.MarkArgumentSet(0);
        state2.Arguments.Value1 = 100;

        // Assert - Original should be unchanged (value semantics)
        Assert.False(state1.IsArgumentSet(0));
        Assert.Equal(42, state1.Arguments.Value1);
        Assert.True(state2.IsArgumentSet(0));
        Assert.Equal(100, state2.Arguments.Value1);
    }

    [Fact]
    public static void IArgumentState_Interface_ImplementedCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        IArgumentState state = new SmallArgumentState<TestArguments>(arguments, 5, 0b101);

        // Act & Assert
        Assert.Equal(5, state.Count);
        Assert.False(state.AreRequiredArgumentsSet);
        Assert.False(state.IsArgumentSet(0));
        Assert.False(state.IsArgumentSet(2));
    }
}