using PolyType.SourceGenModel;

namespace PolyType.Tests.SourceGenModel;

public static class ClassArgumentStateTests
{
    [Fact]
    public static void ClassArgumentStates_ArePoolable()
    {
        ClassSmallArgumentState<(string? Name, int Value)> small =
            ClassSmallArgumentState<(string? Name, int Value)>.Rent((null, 0), 2, 0);
        ClassLargeArgumentState<(string? Name, int Value)> large =
            ClassLargeArgumentState<(string? Name, int Value)>.Rent((null, 0), 65, new ValueBitArray(65));

        Assert.True(small.IsPoolable);
        Assert.True(large.IsPoolable);

        small.Return();
        large.Return();
    }

    [Fact]
    public static void ClassSmallArgumentState_RentReusesAndReinitializesReturnedState()
    {
        ClassSmallArgumentState<(string? Name, int Value)> first = ClassSmallArgumentState<(string? Name, int Value)>.Rent(("retained", 42), 2, 0b10);
        first.MarkArgumentSet(1);
        first.Return();

        ClassSmallArgumentState<(string? Name, int Value)> next = ClassSmallArgumentState<(string? Name, int Value)>.Rent((null, 7), 2, 0b01);

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
    public static void ClassLargeArgumentState_RentReusesAndReinitializesReturnedState()
    {
        ValueBitArray firstRequiredMask = new(65);
        firstRequiredMask[64] = true;
        ClassLargeArgumentState<(string? Name, int Value)> first =
            ClassLargeArgumentState<(string? Name, int Value)>.Rent(("retained", 42), 65, firstRequiredMask);
        first.MarkArgumentSet(64);
        first.Return();

        ValueBitArray nextRequiredMask = new(65);
        nextRequiredMask[0] = true;
        ClassLargeArgumentState<(string? Name, int Value)> next =
            ClassLargeArgumentState<(string? Name, int Value)>.Rent((null, 7), 65, nextRequiredMask);

        Assert.Same(first, next);
        Assert.Equal((null, 7), next.Arguments);
        Assert.False(next.IsArgumentSet(0));
        Assert.False(next.IsArgumentSet(64));
        Assert.False(next.AreRequiredArgumentsSet);
        next.MarkArgumentSet(0);
        Assert.True(next.AreRequiredArgumentsSet);

        next.Return();
    }
}
