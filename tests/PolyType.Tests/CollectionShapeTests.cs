namespace PolyType.Tests;

public abstract partial class CollectionShapeTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollectionMetadata_RetainsMethodsEventsAndAssociatedShapes(bool dictionary)
    {
        ITypeShape shape = dictionary
            ? providerUnderTest.ResolveShape(TestCase.Create(new ObservableLookup { ["first"] = 42 }))
            : providerUnderTest.ResolveShape(TestCase.Create(new ObservableValues { 1, 2, 3 }));
        Assert.Equal("Sum", Assert.Single(shape.Methods).Name);
        Assert.Equal("Added", Assert.Single(shape.Events).Name);
        ITypeShape? associated = shape.GetAssociatedTypeShape(typeof(CollectionMetadataInfo));
        Assert.NotNull(associated);
        Assert.Equal(typeof(CollectionMetadataInfo), associated.Type);
    }

    [Fact]
    public void MutableListWithInternalConstructorCannotBeConstructed()
    {
        var shape = (IEnumerableTypeShape<PublicListOfIntWithInternalConstructor, int>?)providerUnderTest.Provider.GetTypeShape(typeof(PublicListOfIntWithInternalConstructor));
        Assert.NotNull(shape);
        Assert.Equal(CollectionConstructionStrategy.None, shape.ConstructionStrategy);
    }

    [Fact]
    public void InternalMutableListWithPublicConstructorCanBeConstructed()
    {
        var shape = (IEnumerableTypeShape<InternalListOfIntWithPublicConstructor, int>?)providerUnderTest.Provider.GetTypeShape(typeof(InternalListOfIntWithPublicConstructor));
        Assert.NotNull(shape);
        Assert.Equal(CollectionConstructionStrategy.Mutable, shape.ConstructionStrategy);
        Assert.NotNull(shape.GetDefaultConstructor()());
    }

    [Fact]
    public void MutableDictionaryWithInternalConstructorCannotBeConstructed()
    {
        var shape = (IDictionaryTypeShape<PublicDictionaryOfIntWithInternalConstructor, int, bool>?)providerUnderTest.Provider.GetTypeShape(typeof(PublicDictionaryOfIntWithInternalConstructor));
        Assert.NotNull(shape);
        Assert.Equal(CollectionConstructionStrategy.None, shape.ConstructionStrategy);
    }

    [Fact]
    public void InternalMutableDictionaryWithPublicConstructorCanBeConstructed()
    {
        var shape = (IDictionaryTypeShape<InternalDictionaryOfIntWithPublicConstructor, int, bool>?)providerUnderTest.Provider.GetTypeShape(typeof(InternalDictionaryOfIntWithPublicConstructor));
        Assert.NotNull(shape);
        Assert.Equal(CollectionConstructionStrategy.Mutable, shape.ConstructionStrategy);
        Assert.NotNull(shape.GetDefaultConstructor()());
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

    [GenerateShape, AssociatedTypeShape(typeof(CollectionMetadataInfo))]
    public partial class ObservableValues : List<int>
    {
        [MethodShape]
        public int Sum() => this.Aggregate(0, (sum, value) => sum + value);

        [EventShape]
        public event Action<int>? Added;

        public new void Add(int value)
        {
            base.Add(value);
            Added?.Invoke(value);
        }
    }

    [GenerateShape, AssociatedTypeShape(typeof(CollectionMetadataInfo))]
    public partial class ObservableLookup : Dictionary<string, int>
    {
        [MethodShape]
        public int Sum() => Values.Aggregate(0, (sum, value) => sum + value);

        [EventShape]
        public event Action<string>? Added;

        public new void Add(string key, int value)
        {
            base.Add(key, value);
            Added?.Invoke(key);
        }
    }

    public sealed class CollectionMetadataInfo;

    [GenerateShapeFor<PublicListOfIntWithInternalConstructor>]
    partial class Witness;

    public sealed class Reflection() : CollectionShapeTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : CollectionShapeTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : CollectionShapeTests(new SourceGenProviderUnderTest(Witness.GeneratedTypeShapeProvider));
}
