using PolyType.SourceGenModel;

namespace PolyType.Tests.SourceGenModel;

public static class ClassArgumentStateTests
{
    [Theory]
    [InlineData(1, 64)]
    [InlineData(64, 1)]
    [InlineData(0, 0)]
    public static void SmallClassArgumentState_RentReinitializesTrackingFields(int firstCount, int nextCount)
    {
        ulong firstMask = firstCount == 0 ? 0 : 1UL << (firstCount - 1);
        SmallClassArgumentState<(int, int)> first = SmallClassArgumentState<(int, int)>.Rent((42, 43), firstCount, firstMask, markAllArgumentsSet: true);
        Assert.True(first.AreRequiredArgumentsSet);
        first.Return();

        ulong nextMask = nextCount == 0 ? 0 : 1UL << (nextCount - 1);
        SmallClassArgumentState<(int, int)> next = SmallClassArgumentState<(int, int)>.Rent((1, 2), nextCount, nextMask);
        Assert.Same(first, next);
        Assert.Equal((1, 2), next.Arguments);
        Assert.Equal(nextCount, next.Count);
        Assert.Equal(nextCount == 0, next.AreRequiredArgumentsSet);
        for (int i = 0; i < nextCount; i++)
        {
            Assert.False(next.IsArgumentSet(i));
        }

        next.MarkArgumentSet(nextCount - 1);
        Assert.True(next.AreRequiredArgumentsSet);
        next.Return();
    }

    [Theory]
    [InlineData(65)]
    [InlineData(66)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    public static void LargeClassArgumentState_RentInitializesReusedAndResizedBitsets(int count)
    {
        ValueBitArray mask = new(count);
        mask[count - 1] = true;
        LargeClassArgumentState<int> first = LargeClassArgumentState<int>.Rent(42, count, mask, markAllArgumentsSet: true);
        Assert.True(first.AreRequiredArgumentsSet);
        first.Return();

        foreach (bool markAllArgumentsSet in new[] { false, true })
        {
            LargeClassArgumentState<int> next = LargeClassArgumentState<int>.Rent(7, count, mask, markAllArgumentsSet);
            Assert.Same(first, next);
            Assert.Equal(7, next.Arguments);
            Assert.Equal(count, next.Count);
            Assert.Equal(markAllArgumentsSet, next.AreRequiredArgumentsSet);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(markAllArgumentsSet, next.IsArgumentSet(i));
            }

            Assert.False(next.IsArgumentSet(count));
            next.Return();
        }

        ValueBitArray resizedMask = new(count + 1);
        resizedMask[count] = true;
        LargeClassArgumentState<int> resized = LargeClassArgumentState<int>.Rent(9, count + 1, resizedMask, markAllArgumentsSet: true);
        Assert.Same(first, resized);
        Assert.True(resized.AreRequiredArgumentsSet);
        Assert.True(resized.IsArgumentSet(count));
        resized.Return();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ClassArgumentState_ReturnClearsNestedReferences(bool large)
    {
        (object?, (object?, int)) arguments = (new object(), (new object(), 42));
        PolyType.Abstractions.IArgumentState state = large
            ? LargeClassArgumentState<(object?, (object?, int))>.Rent(arguments, 65, new ValueBitArray(65))
            : SmallClassArgumentState<(object?, (object?, int))>.Rent(arguments, 2, 0);
        state.Return();

        // Inspect backing storage rather than depend on GC stack-scanning precision on Mono.
        string fieldName = nameof(SmallClassArgumentState<(object?, (object?, int))>.Arguments);
        var cleared = ((object?, (object?, int)))state.GetType().GetField(fieldName)!.GetValue(state)!;
        Assert.Null(cleared.Item1);
        Assert.Null(cleared.Item2.Item1);
    }

    [Fact]
    public static void ClassArgumentStates_ImplementReturnContract()
    {
        SmallClassArgumentState<(string? Name, int Value)> small =
            SmallClassArgumentState<(string? Name, int Value)>.Rent((null, 0), 2, 0);
        LargeClassArgumentState<(string? Name, int Value)> large =
            LargeClassArgumentState<(string? Name, int Value)>.Rent((null, 0), 65, new ValueBitArray(65));

        ((PolyType.Abstractions.IArgumentState)small).Return();
        ((PolyType.Abstractions.IArgumentState)large).Return();
    }

    [Fact]
    public static void SmallClassArgumentState_MarkAllArgumentsSet_Supports64Arguments()
    {
        SmallClassArgumentState<int> state = SmallClassArgumentState<int>.Rent(42, 64, ulong.MaxValue, markAllArgumentsSet: true);

        Assert.Equal(64, state.Count);
        Assert.True(state.AreRequiredArgumentsSet);
        Assert.True(state.IsArgumentSet(0));
        Assert.True(state.IsArgumentSet(63));
        Assert.False(state.IsArgumentSet(-1));
        Assert.False(state.IsArgumentSet(64));

        state.MarkArgumentSet(-1);
        state.MarkArgumentSet(64);
        Assert.True(state.IsArgumentSet(63));

        state.Return();
    }

    [Fact]
    public static void SmallClassArgumentState_RentRejectsMoreThan64Arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SmallClassArgumentState<int>.Rent(42, 65, 0));
    }

    [Fact]
    public static void SmallClassArgumentState_RentReusesAndReinitializesReturnedState()
    {
        SmallClassArgumentState<(string? Name, int Value)> first = SmallClassArgumentState<(string? Name, int Value)>.Rent(("retained", 42), 2, 0b10);
        first.MarkArgumentSet(1);
        first.Return();

        SmallClassArgumentState<(string? Name, int Value)> next = SmallClassArgumentState<(string? Name, int Value)>.Rent((null, 7), 2, 0b01);

        Assert.Same(first, next);
        Assert.Equal((null, 7), next.Arguments);
        Assert.False(next.IsArgumentSet(0));
        Assert.False(next.IsArgumentSet(1));
        Assert.False(next.AreRequiredArgumentsSet);
        next.MarkArgumentSet(0);
        Assert.True(next.AreRequiredArgumentsSet);

        next.Return();
    }

    [Fact]
    public static void LargeClassArgumentState_MarkAllArgumentsSet_ResizesReturnedState()
    {
        LargeClassArgumentState<int> first = LargeClassArgumentState<int>.Rent(42, 65, new ValueBitArray(65));
        first.Return();

        ValueBitArray requiredArgumentsMask = new(66);
        requiredArgumentsMask[65] = true;
        LargeClassArgumentState<int> next = LargeClassArgumentState<int>.Rent(7, 66, requiredArgumentsMask, markAllArgumentsSet: true);

        Assert.Same(first, next);
        Assert.Equal(66, next.Count);
        Assert.True(next.AreRequiredArgumentsSet);
        Assert.True(next.IsArgumentSet(0));
        Assert.True(next.IsArgumentSet(65));

        next.Return();
    }

    [Fact]
    public static void LargeClassArgumentState_RentRejectsMismatchedRequiredArgumentsMask()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LargeClassArgumentState<int>.Rent(42, 65, new ValueBitArray(64)));
    }

    [Fact]
    public static void LargeClassArgumentState_RentReusesAndReinitializesReturnedState()
    {
        ValueBitArray firstRequiredMask = new(65);
        firstRequiredMask[64] = true;
        LargeClassArgumentState<(string? Name, int Value)> first =
            LargeClassArgumentState<(string? Name, int Value)>.Rent(("retained", 42), 65, firstRequiredMask);
        first.MarkArgumentSet(64);
        first.Return();

        ValueBitArray nextRequiredMask = new(65);
        nextRequiredMask[0] = true;
        LargeClassArgumentState<(string? Name, int Value)> next =
            LargeClassArgumentState<(string? Name, int Value)>.Rent((null, 7), 65, nextRequiredMask);

        Assert.Same(first, next);
        Assert.Equal((null, 7), next.Arguments);
        Assert.False(next.IsArgumentSet(0));
        Assert.False(next.IsArgumentSet(64));
        Assert.False(next.AreRequiredArgumentsSet);
        next.MarkArgumentSet(0);
        Assert.True(next.AreRequiredArgumentsSet);

        next.Return();
    }

    [Fact]
    public static void EmptyArgumentState_ReturnIsNoOp()
    {
        EmptyArgumentState state = EmptyArgumentState.Instance;

        Assert.Equal(0, state.Count);
        Assert.True(state.AreRequiredArgumentsSet);
        Assert.False(state.IsArgumentSet(0));
        state.Return();
        Assert.Equal(0, state.Count);
        Assert.True(state.AreRequiredArgumentsSet);
    }
}
