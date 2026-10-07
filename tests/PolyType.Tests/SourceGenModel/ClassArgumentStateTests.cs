using PolyType.SourceGenModel;

namespace PolyType.Tests.SourceGenModel;

public static class ClassArgumentStateTests
{
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
