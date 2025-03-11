using PolyType;
using PolyType.Tests;

[assembly: TypeShapeExtension(typeof(AssociatedTypesTests.GenericDataType<,>), AssociatedTypes = [typeof(AssociatedTypesTests.GenericDataTypeVerifier<,>)])]

namespace PolyType.Tests;

public abstract partial class AssociatedTypesTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void CanConstructAssociatedType()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Func<object>? factory = typeShape.GetAssociatedTypeFactory(typeof(GenericDataTypeConverter<,>));
        Assert.NotNull(factory);
        var instance1 = Assert.IsType<GenericDataTypeConverter<int, string>>(factory.Invoke());
        var instance2 = factory.Invoke();
        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void CanConstructAssociatedType2()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Func<object>? factory = typeShape.GetAssociatedTypeFactory(typeof(GenericDataTypeCloner<,>));
        Assert.NotNull(factory);
        var instance1 = Assert.IsType<GenericDataTypeCloner<int, string>>(factory.Invoke());
        var instance2 = factory.Invoke();
        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void CanConstructAssociatedType_ByExtension()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Func<object>? factory = typeShape.GetAssociatedTypeFactory(typeof(GenericDataTypeVerifier<,>));
        Assert.NotNull(factory);
        Assert.IsType<GenericDataTypeVerifier<int, string>>(factory.Invoke());
    }

    [Fact]
    public void CanConstructAssociatedType_NonGeneric()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(NonGenericDataType));
        Assert.NotNull(typeShape);
        Func<object>? factory = typeShape.GetAssociatedTypeFactory(typeof(NonGenericDataTypeConverter));
        Assert.NotNull(factory);
        Assert.IsType<NonGenericDataTypeConverter>(factory.Invoke());
    }

    [Fact]
    public void CanConstructAssociatedType_GenericToNonGeneric()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Func<object>? factory = typeShape.GetAssociatedTypeFactory(typeof(NonGenericDataTypeConverter));
        Assert.NotNull(factory);
        Assert.IsType<NonGenericDataTypeConverter>(factory.Invoke());
    }


    [Fact]
    public void CanConstructAssociatedType_TypeArgsSplitAcrossTypes()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Func<object>? factory = typeShape.GetAssociatedTypeFactory(typeof(GenericWrapper<>.GenericNested<>));
        Assert.NotNull(factory);
        Assert.IsType<GenericWrapper<int>.GenericNested<string>>(factory.Invoke());
    }

    [GenerateShape]
    [TypeShape(AssociatedTypes = [typeof(NonGenericDataTypeConverter)])]
    public partial class NonGenericDataType;

    internal class NonGenericDataTypeConverter;

    [TypeShape(AssociatedTypes = [typeof(GenericDataTypeConverter<,>), typeof(GenericDataTypeCloner<,>), typeof(NonGenericDataTypeConverter), typeof(GenericWrapper<>.GenericNested<>)])]
    public class GenericDataType<T1, T2>;

    public class GenericDataTypeConverter<T1, T2>;
    public class GenericDataTypeCloner<T1, T2>;
    public class GenericDataTypeVerifier<T1, T2>;

    public class GenericWrapper<T1>
    {
        public class GenericNested<T2>;
    }

    [GenerateShape<GenericDataType<int, string>>]
    internal partial class Witness;

    public sealed class Reflection() : AssociatedTypesTests(RefectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : AssociatedTypesTests(RefectionProviderUnderTest.Emit);
    public sealed class SourceGen() : AssociatedTypesTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
