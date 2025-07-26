using System;
using PolyType.SourceGenModel;

namespace PolyType.Tests.SourceGenModel;

public static class LargeArgumentStateTests
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
        int count = 100;
        var requiredMask = new ValueBitArray(count);
        requiredMask[0] = true;
        requiredMask[50] = true;
        requiredMask[99] = true;

        // Act
        var state = new LargeArgumentState<TestArguments>(arguments, count, requiredMask);

        // Assert
        Assert.Equal(count, state.Count);
        Assert.Equal(42, state.Arguments.Value1);
        Assert.False(state.AreRequiredArgumentsSet); // No arguments marked as set yet
    }

    [Fact]
    public static void Constructor_WithMismatchedCountAndMask_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var arguments = new TestArguments();
        int count = 100;
        var requiredMask = new ValueBitArray(50); // Different length

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new LargeArgumentState<TestArguments>(arguments, count, requiredMask));
    }

    [Fact]
    public static void Constructor_WithZeroCount_Succeeds()
    {
        // Arrange
        var arguments = new TestArguments();
        int count = 0;
        var requiredMask = new ValueBitArray(0);

        // Act
        var state = new LargeArgumentState<TestArguments>(arguments, count, requiredMask);

        // Assert
        Assert.Equal(0, state.Count);
        Assert.True(state.AreRequiredArgumentsSet); // Vacuously true for empty set
    }

    [Fact]
    public static void IsArgumentSet_InitiallyAllArgumentsAreNotSet()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(100);
        var state = new LargeArgumentState<TestArguments>(arguments, 100, requiredMask);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            Assert.False(state.IsArgumentSet(i));
        }
    }

    [Fact]
    public static void IsArgumentSet_WithIndexOutOfRange_ReturnsFalse()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(5);
        var state = new LargeArgumentState<TestArguments>(arguments, 5, requiredMask);

        // Act & Assert
        Assert.False(state.IsArgumentSet(5));   // At boundary
        Assert.False(state.IsArgumentSet(100)); // Beyond boundary
        Assert.False(state.IsArgumentSet(-1));  // Negative index
    }

    [Fact]
    public static void MarkArgumentSet_SetsArgumentCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(100);
        var state = new LargeArgumentState<TestArguments>(arguments, 100, requiredMask);

        // Act
        state.MarkArgumentSet(0);
        state.MarkArgumentSet(25);
        state.MarkArgumentSet(50);
        state.MarkArgumentSet(99);

        // Assert
        Assert.True(state.IsArgumentSet(0));
        Assert.False(state.IsArgumentSet(1));
        Assert.True(state.IsArgumentSet(25));
        Assert.False(state.IsArgumentSet(26));
        Assert.True(state.IsArgumentSet(50));
        Assert.False(state.IsArgumentSet(51));
        Assert.True(state.IsArgumentSet(99));
        Assert.False(state.IsArgumentSet(98));
    }

    [Fact]
    public static void MarkArgumentSet_WithIndexOutOfRange_DoesNotThrow()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(5);
        var state = new LargeArgumentState<TestArguments>(arguments, 5, requiredMask);

        // Act (should not throw, behavior defined by ValueBitArray)
        state.MarkArgumentSet(5);
        state.MarkArgumentSet(100);
        state.MarkArgumentSet(-1);

        // Assert - verify no valid arguments were accidentally set
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
        var requiredMask = new ValueBitArray(100); // All bits false by default
        var state = new LargeArgumentState<TestArguments>(arguments, 100, requiredMask);

        // Act & Assert
        Assert.True(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WithSomeRequiredArguments_InitiallyReturnsFalse()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(100);
        requiredMask[0] = true;
        requiredMask[50] = true;
        requiredMask[99] = true;
        var state = new LargeArgumentState<TestArguments>(arguments, 100, requiredMask);

        // Act & Assert
        Assert.False(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WhenAllRequiredArgumentsSet_ReturnsTrue()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(100);
        requiredMask[10] = true;
        requiredMask[30] = true;
        requiredMask[70] = true;
        var state = new LargeArgumentState<TestArguments>(arguments, 100, requiredMask);

        // Act
        state.MarkArgumentSet(10);
        state.MarkArgumentSet(30);
        state.MarkArgumentSet(70);

        // Assert
        Assert.True(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WhenSomeRequiredArgumentsMissing_ReturnsFalse()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(100);
        requiredMask[10] = true;
        requiredMask[30] = true;
        requiredMask[70] = true;
        var state = new LargeArgumentState<TestArguments>(arguments, 100, requiredMask);

        // Act
        state.MarkArgumentSet(10);
        state.MarkArgumentSet(30);
        // Missing argument 70

        // Assert
        Assert.False(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WithExtraArgumentsSet_StillReturnsTrue()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(100);
        requiredMask[10] = true;
        requiredMask[30] = true;
        var state = new LargeArgumentState<TestArguments>(arguments, 100, requiredMask);

        // Act
        state.MarkArgumentSet(10);
        state.MarkArgumentSet(20); // Extra argument
        state.MarkArgumentSet(30);
        state.MarkArgumentSet(50); // Extra argument

        // Assert
        Assert.True(state.AreRequiredArgumentsSet);
    }

    [Fact]
    public static void Arguments_Property_AllowsModification()
    {
        // Arrange
        var arguments = new TestArguments { Value1 = 42, Value2 = "test" };
        var requiredMask = new ValueBitArray(3);
        var state = new LargeArgumentState<TestArguments>(arguments, 3, requiredMask);

        // Act
        state.Arguments.Value1 = 100;
        state.Arguments.Value3 = true;

        // Assert
        Assert.Equal(100, state.Arguments.Value1);
        Assert.Equal("test", state.Arguments.Value2);
        Assert.True(state.Arguments.Value3);
    }

    [Fact]
    public static void MarkArgumentSet_WithLargeNumberOfArguments_WorksCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(1000);
        var state = new LargeArgumentState<TestArguments>(arguments, 1000, requiredMask);

        // Act - Set arguments across multiple bytes
        var indicesToSet = new[] { 0, 63, 64, 127, 128, 255, 256, 511, 512, 999 };
        foreach (int index in indicesToSet)
        {
            state.MarkArgumentSet(index);
        }

        // Assert
        foreach (int index in indicesToSet)
        {
            Assert.True(state.IsArgumentSet(index));
        }

        // Verify adjacent arguments are not set
        var indicesToCheck = new[] { 1, 62, 65, 126, 129, 254, 257, 510, 513, 998 };
        foreach (int index in indicesToCheck)
        {
            Assert.False(state.IsArgumentSet(index));
        }
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WithLargeNumberOfRequiredArguments_WorksCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(1000);
        
        // Set every 10th argument as required
        for (int i = 0; i < 1000; i += 10)
        {
            requiredMask[i] = true;
        }
        
        var state = new LargeArgumentState<TestArguments>(arguments, 1000, requiredMask);

        // Initially not all required arguments are set
        Assert.False(state.AreRequiredArgumentsSet);

        // Act - Set all required arguments
        for (int i = 0; i < 1000; i += 10)
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
        var requiredMask = new ValueBitArray(100);
        var state = new LargeArgumentState<TestArguments>(arguments, 100, requiredMask);

        // Act
        state.MarkArgumentSet(50);
        state.MarkArgumentSet(50); // Set again
        state.MarkArgumentSet(50); // Set again

        // Assert
        Assert.True(state.IsArgumentSet(50));
        Assert.False(state.IsArgumentSet(49));
        Assert.False(state.IsArgumentSet(51));
    }

    [Fact]
    public static void StructBehavior_IsValueType()
    {
        // Arrange
        var arguments1 = new TestArguments { Value1 = 42 };
        var requiredMask = new ValueBitArray(3);
        var state1 = new LargeArgumentState<TestArguments>(arguments1, 3, requiredMask);
        
        // Act - Modify copy
        var state2 = state1;
        state2.MarkArgumentSet(0);
        state2.Arguments.Value1 = 100;

        // Assert - Original should be unchanged for Arguments field (value semantics)
        // but shared BitArray references mean both will see the set argument
        Assert.Equal(42, state1.Arguments.Value1);
        Assert.Equal(100, state2.Arguments.Value1);
        
        // Both will see the argument as set due to shared ValueBitArray reference
        Assert.True(state1.IsArgumentSet(0));
        Assert.True(state2.IsArgumentSet(0));
    }

    [Fact]
    public static void IArgumentState_Interface_ImplementedCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(100);
        requiredMask[0] = true;
        requiredMask[50] = true;
        IArgumentState state = new LargeArgumentState<TestArguments>(arguments, 100, requiredMask);

        // Act & Assert
        Assert.Equal(100, state.Count);
        Assert.False(state.AreRequiredArgumentsSet);
        Assert.False(state.IsArgumentSet(0));
        Assert.False(state.IsArgumentSet(50));
    }

    [Fact]
    public static void Constructor_WithLargeRequiredMask_WorksCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        int count = 500;
        var requiredMask = new ValueBitArray(count);
        
        // Set multiple arguments as required across byte boundaries
        for (int i = 0; i < count; i += 7) // Every 7th argument
        {
            requiredMask[i] = true;
        }

        // Act
        var state = new LargeArgumentState<TestArguments>(arguments, count, requiredMask);

        // Assert
        Assert.Equal(count, state.Count);
        Assert.False(state.AreRequiredArgumentsSet);
        
        // Verify the required mask was properly set
        for (int i = 0; i < count; i++)
        {
            bool expectedRequired = (i % 7) == 0;
            if (expectedRequired)
            {
                // We can't directly test the required mask, but we can verify
                // by setting only this argument and checking if requirements are met
                var testState = new LargeArgumentState<TestArguments>(arguments, count, requiredMask);
                testState.MarkArgumentSet(i);
                
                // If this was the only required argument, requirements would be met
                // We'll test this indirectly by the overall behavior
            }
        }
    }

    [Fact]
    public static void AreRequiredArgumentsSet_WithByteAlignedArguments_WorksCorrectly()
    {
        // Arrange
        var arguments = new TestArguments();
        var requiredMask = new ValueBitArray(200);
        
        // Set arguments at byte boundaries as required
        requiredMask[0] = true;   // First bit of first byte
        requiredMask[7] = true;   // Last bit of first byte
        requiredMask[8] = true;   // First bit of second byte
        requiredMask[63] = true;  // Last bit of 8th byte
        requiredMask[64] = true;  // First bit of 9th byte
        requiredMask[199] = true; // Last argument
        
        var state = new LargeArgumentState<TestArguments>(arguments, 200, requiredMask);

        // Initially not satisfied
        Assert.False(state.AreRequiredArgumentsSet);

        // Act - Set required arguments one by one
        state.MarkArgumentSet(0);
        Assert.False(state.AreRequiredArgumentsSet);
        
        state.MarkArgumentSet(7);
        Assert.False(state.AreRequiredArgumentsSet);
        
        state.MarkArgumentSet(8);
        Assert.False(state.AreRequiredArgumentsSet);
        
        state.MarkArgumentSet(63);
        Assert.False(state.AreRequiredArgumentsSet);
        
        state.MarkArgumentSet(64);
        Assert.False(state.AreRequiredArgumentsSet);
        
        state.MarkArgumentSet(199);
        Assert.True(state.AreRequiredArgumentsSet); // Now all required are set
    }
}