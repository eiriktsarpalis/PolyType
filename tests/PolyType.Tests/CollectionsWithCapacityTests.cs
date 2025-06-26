namespace PolyType.Tests;

public abstract partial class CollectionsWithCapacityTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void List_OfInt()
    {
        IEnumerableTypeShape<List<int>, int> shape = (IEnumerableTypeShape<List<int>, int>)providerUnderTest.Provider.Resolve<List<int>>();
        List<int> list = shape.GetMutableCollectionConstructor()(new() { Capacity = 10 });
        Assert.Equal(10, list.Capacity);
    }

    [Fact]
    public void List_OfString()
    {
        IEnumerableTypeShape<List<string>, string> shape = (IEnumerableTypeShape<List<string>, string>)providerUnderTest.Provider.Resolve<List<string>>();
        List<string> list = shape.GetMutableCollectionConstructor()(new() { Capacity = 10 });
        Assert.Equal(10, list.Capacity);
    }

    [Fact]
    public void Dictionary()
    {
        IDictionaryTypeShape<Dictionary<int, bool>, int, bool> shape = (IDictionaryTypeShape<Dictionary<int, bool>, int, bool>)providerUnderTest.Provider.Resolve<Dictionary<int, bool>>();
        Dictionary<int, bool> dict = shape.GetMutableCollectionConstructor()(new() { Capacity = 10 });
#if NET9_0_OR_GREATER
        Assert.Equal(10, dict.Capacity);
#else
        Assert.Skip(".NET Framework provides no way to assert capacity.");
#endif
    }

    [GenerateShape<List<int>>]
    [GenerateShape<List<string>>]
    [GenerateShape<Dictionary<int, bool>>]
    partial class Witness;

    public sealed class Reflection() : CollectionsWithCapacityTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : CollectionsWithCapacityTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : CollectionsWithCapacityTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
