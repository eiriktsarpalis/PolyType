using PolyType;
using PolyType.Tests;

[assembly: TypeShapeExtension(typeof(AssociatedTypesTests.GenericDataType<,>), AssociatedTypes = [typeof(AssociatedTypesTests.GenericDataTypeVerifier<,>)], AssociatedShapes = [typeof(AssociatedTypesTests.ExtraShape<,>)])]

namespace PolyType.Tests;

[Trait("AssociatedTypes", "true")]
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
    public void CanConstructAssociatedType_WithClosedGeneric()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Func<object>? factory = typeShape.GetAssociatedTypeFactory(typeof(GenericDataTypeConverter<int, string>));
        Assert.NotNull(factory);
        var instance1 = Assert.IsType<GenericDataTypeConverter<int, string>>(factory.Invoke());
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

    [Fact]
    public void AssociatedTypeAttribute_ConstructorParameter()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(CustomTypeWithCustomConverter));
        Assert.NotNull(typeShape);
        Func<object>? factory = typeShape.GetAssociatedTypeFactory(typeof(CustomTypeConverter));
        Assert.NotNull(factory);
        Assert.IsType<CustomTypeConverter>(factory.Invoke());
    }

    [Fact]
    public void AssociatedTypeAttribute_NamedParameter()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(CustomTypeWithCustomConverter));
        Assert.NotNull(typeShape);
        Func<object>? factory1 = typeShape.GetAssociatedTypeFactory(typeof(CustomTypeConverter1));
        Assert.NotNull(factory1);
        Assert.IsType<CustomTypeConverter1>(factory1.Invoke());

        Func<object>? factory2 = typeShape.GetAssociatedTypeFactory(typeof(CustomTypeConverter2));
        Assert.NotNull(factory2);
        Assert.IsType<CustomTypeConverter2>(factory2.Invoke());
    }

    [Fact]
    public void TypeShapeExtension_AssociatedShape()
    {
        // Get it through the associated type shape API, which accepts an unbound generic type.
        // We do this by first starting with a known shape, then jumping to the associated type.
        ITypeShape? ordinaryShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(ordinaryShape);

        // Fetch as an independent shape.
        ITypeShape? associatedShape = providerUnderTest.Provider.GetShape(typeof(ExtraShape<int, string>));
        Assert.NotNull(associatedShape);

        // Fetch as an associated shape by its unbound generic.
        associatedShape = ordinaryShape.GetAssociatedTypeShape(typeof(ExtraShape<,>));
        Assert.NotNull(associatedShape);

        // Fetch as an associated shape by its closed generic.
        associatedShape = ordinaryShape.GetAssociatedTypeShape(typeof(ExtraShape<int, string>));
        Assert.NotNull(associatedShape);
    }

    [GenerateShape]
    [TypeShape(AssociatedTypes = [typeof(NonGenericDataTypeConverter)])]
    public partial class NonGenericDataType;

    public class NonGenericDataTypeConverter;

    [TypeShape(AssociatedTypes = [typeof(GenericDataTypeConverter<,>), typeof(GenericDataTypeCloner<,>), typeof(NonGenericDataTypeConverter), typeof(GenericWrapper<>.GenericNested<>)])]
    public class GenericDataType<T1, T2>;

    public class GenericDataTypeConverter<T1, T2>;
    public class GenericDataTypeCloner<T1, T2>;
    public class GenericDataTypeVerifier<T1, T2>;

    /// <summary>
    /// A class that should <em>not</em> be directly referenced by any other shaped type.
    /// It should have a shape generated for it, due to type extensions.
    /// </summary>
    public class ExtraShape<T1, T2>;

    public class GenericWrapper<T1>
    {
        public class GenericNested<T2>;
    }

    [GenerateShape<GenericDataType<int, string>>]
    internal partial class Witness;

    [GenerateShape]
    [MyConverter(typeof(CustomTypeConverter))]
    [MyConverterNamedArg(Types = [typeof(CustomTypeConverter1), typeof(CustomTypeConverter2)])]
    internal partial class CustomTypeWithCustomConverter;

    public class CustomTypeConverter;
    public class CustomTypeConverter1;
    public class CustomTypeConverter2;

    [AssociatedTypeAttribute(nameof(type), AssociatedTypeRequirements.Factory)]
    internal class MyConverterAttribute(Type type) : Attribute
    {
        public Type Type => type;
    }

    [AssociatedTypeAttribute(nameof(Types), AssociatedTypeRequirements.Factory)]
    internal class MyConverterNamedArgAttribute : Attribute
    {
        public Type[] Types { get; init; } = [];
    }

    public sealed class Reflection() : AssociatedTypesTests(RefectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : AssociatedTypesTests(RefectionProviderUnderTest.Emit);
    public sealed class SourceGen() : AssociatedTypesTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
