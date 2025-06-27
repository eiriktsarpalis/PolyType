namespace PolyType.Tests;

public class CollectionConstructionOptionsTests
{
    [Fact]
    public void CopyConstructor()
    {
        CollectionConstructionOptions<int> original = new()
        {
            EqualityComparer = EqualityComparer<int>.Default,
            Comparer = Comparer<int>.Default,
            Capacity = 10
        };
        CollectionConstructionOptions<int> copy = new(original);
        Assert.Same(original.EqualityComparer, copy.EqualityComparer);
        Assert.Same(original.Comparer, copy.Comparer);
        Assert.Equal(original.Capacity, copy.Capacity);
    }
}
