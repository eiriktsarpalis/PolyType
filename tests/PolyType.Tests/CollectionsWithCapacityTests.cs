﻿namespace PolyType.Tests;

public abstract partial class CollectionsWithCapacityTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void List_OfInt()
    {
        IEnumerableTypeShape<List<int>, int> shape = (IEnumerableTypeShape<List<int>, int>)providerUnderTest.Provider.Resolve<List<int>>();
        List<int> list = shape.GetMutableCollectionConstructor()(new() { Capacity = 11 });
        Assert.Equal(11, list.Capacity);
    }

    [Fact]
    public void List_OfString()
    {
        IEnumerableTypeShape<List<string>, string> shape = (IEnumerableTypeShape<List<string>, string>)providerUnderTest.Provider.Resolve<List<string>>();
        List<string> list = shape.GetMutableCollectionConstructor()(new() { Capacity = 11 });
        Assert.Equal(11, list.Capacity);
    }

    [Fact]
    public void Dictionary()
    {
        IDictionaryTypeShape<Dictionary<int, bool>, int, bool> shape = (IDictionaryTypeShape<Dictionary<int, bool>, int, bool>)providerUnderTest.Provider.Resolve<Dictionary<int, bool>>();
        Dictionary<int, bool> dict = shape.GetMutableCollectionConstructor()(new() { Capacity = 11 });
#if NET9_0_OR_GREATER
        Assert.Equal(11, dict.Capacity);
#else
        Assert.Skip(".NET 9+ is required to assert capacity.");
#endif
    }

    [Fact]
    public void HashSet()
    {
        IEnumerableTypeShape<HashSet<int>, int> shape = (IEnumerableTypeShape<HashSet<int>, int>)providerUnderTest.Provider.Resolve<HashSet<int>>();
        HashSet<int> set = shape.GetMutableCollectionConstructor()(new() { Capacity = 11 });
#if NET9_0_OR_GREATER
        Assert.Equal(11, set.Capacity);
#else
        Assert.Skip(".NET 9+ is required to assert capacity.");
#endif
    }

    [GenerateShapeFor<List<int>>]
    [GenerateShapeFor<List<string>>]
    [GenerateShapeFor<HashSet<int>>]
    [GenerateShapeFor<Dictionary<int, bool>>]
    partial class Witness;

    ////public sealed class Reflection() : CollectionsWithCapacityTests(ReflectionProviderUnderTest.NoEmit);
    ////public sealed class ReflectionEmit() : CollectionsWithCapacityTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : CollectionsWithCapacityTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
