namespace PolyType.Tests;

public abstract partial class CollectionShapeTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void MutableListWithInternalConstructorCannotBeConstructed()
    {
        var shape = (IEnumerableTypeShape<PublicListOfIntWithInternalConstructor, int>?)providerUnderTest.Provider.GetShape(typeof(PublicListOfIntWithInternalConstructor));
        Assert.NotNull(shape);
        Assert.Equal(CollectionConstructionStrategy.None, shape.ConstructionStrategy);
    }

    [Fact]
    public void InternalMutableListWithPublicConstructorCanBeConstructed()
    {
        var shape = (IEnumerableTypeShape<InternalListOfIntWithPublicConstructor, int>?)providerUnderTest.Provider.GetShape(typeof(InternalListOfIntWithPublicConstructor));
        Assert.NotNull(shape);
        Assert.Equal(CollectionConstructionStrategy.Mutable, shape.ConstructionStrategy);
        Assert.NotNull(shape.GetMutableConstructor()());
    }

    [Fact]
    public void MutableDictionaryWithInternalConstructorCannotBeConstructed()
    {
        var shape = (IDictionaryTypeShape<PublicDictionaryOfIntWithInternalConstructor, int, bool>?)providerUnderTest.Provider.GetShape(typeof(PublicDictionaryOfIntWithInternalConstructor));
        Assert.NotNull(shape);
        Assert.Equal(CollectionConstructionStrategy.None, shape.ConstructionStrategy);
    }

    [Fact]
    public void InternalMutableDictionaryWithPublicConstructorCanBeConstructed()
    {
        var shape = (IDictionaryTypeShape<InternalDictionaryOfIntWithPublicConstructor, int, bool>?)providerUnderTest.Provider.GetShape(typeof(InternalDictionaryOfIntWithPublicConstructor));
        Assert.NotNull(shape);
        Assert.Equal(CollectionConstructionStrategy.Mutable, shape.ConstructionStrategy);
        Assert.NotNull(shape.GetMutableConstructor()());
    }

    [GenerateShape]
    public partial class PublicListOfIntWithInternalConstructor : List<int>
    {
        internal PublicListOfIntWithInternalConstructor()
        {
        }
    }

    [GenerateShape]
    internal partial class InternalListOfIntWithPublicConstructor : List<int>
    {
        public InternalListOfIntWithPublicConstructor()
        {
        }
    }

    [GenerateShape]
    public partial class PublicDictionaryOfIntWithInternalConstructor : Dictionary<int, bool>
    {
        internal PublicDictionaryOfIntWithInternalConstructor()
        {
        }
    }

    [GenerateShape]
    internal partial class InternalDictionaryOfIntWithPublicConstructor : Dictionary<int, bool>
    {
        public InternalDictionaryOfIntWithPublicConstructor()
        {
        }
    }

    [GenerateShape<PublicListOfIntWithInternalConstructor>]
    partial class Witness;

    public sealed class Reflection() : CollectionShapeTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : CollectionShapeTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : CollectionShapeTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
