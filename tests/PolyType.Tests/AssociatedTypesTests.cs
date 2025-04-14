using PolyType;
using PolyType.Tests;
using Xunit.Internal;

[assembly: TypeShapeExtension(typeof(AssociatedTypesTests.GenericDataType<,>), AssociatedShapeDepth = TypeShapeDepth.Constructor, AssociatedTypes = [typeof(AssociatedTypesTests.GenericDataTypeVerifier<,>)])]
[assembly: TypeShapeExtension(typeof(AssociatedTypesTests.GenericDataType<,>), AssociatedShapeDepth = TypeShapeDepth.All, AssociatedTypes = [typeof(AssociatedTypesTests.ExtraShape<,>)])]

// This pair is for testing the union of depth flags for a given shape.
[assembly: TypeShapeExtension(typeof(AssociatedTypesTests.GenericDataType<,>), AssociatedShapeDepth = TypeShapeDepth.Properties, AssociatedTypes = [typeof(AssociatedTypesTests.ExtraShape2<,>)])]
[assembly: TypeShapeExtension(typeof(AssociatedTypesTests.GenericDataType<,>), AssociatedShapeDepth = TypeShapeDepth.Constructor, AssociatedTypes = [typeof(AssociatedTypesTests.ExtraShape2<,>)])]

namespace PolyType.Tests;

[Trait("AssociatedTypes", "true")]
public abstract partial class AssociatedTypesTests(ProviderUnderTest providerUnderTest, bool partialShapesSupported)
{
    [Fact]
    public void CanConstructAssociatedType()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Func<object>? factory = GetAssociatedTypeFactory(typeShape, typeof(GenericDataTypeConverter<,>));
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
        Func<object>? factory = GetAssociatedTypeFactory(typeShape, typeof(GenericDataTypeCloner<,>));
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
        Func<object>? factory = GetAssociatedTypeFactory(typeShape, typeof(GenericDataTypeConverter<int, string>));
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
        IObjectTypeShape? associatedShape = (IObjectTypeShape?)typeShape.GetAssociatedTypeShape(typeof(GenericDataTypeVerifier<,>));
        Assert.NotNull(associatedShape);
        Func<object>? factory = associatedShape.GetDefaultConstructor();
        Assert.NotNull(factory);
        Assert.IsType<GenericDataTypeVerifier<int, string>>(factory.Invoke());

        if (partialShapesSupported)
        {
            // Verify the associated type's shape is only partially available.
            Assert.Empty(associatedShape.Properties);
        }
    }

    [Fact]
    public void CanConstructAssociatedType_NonGeneric()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(NonGenericDataType));
        Assert.NotNull(typeShape);
        Func<object>? factory = GetAssociatedTypeFactory(typeShape, typeof(NonGenericDataTypeConverter));
        Assert.NotNull(factory);
        Assert.IsType<NonGenericDataTypeConverter>(factory.Invoke());
    }

    [Fact]
    public void CanConstructAssociatedType_GenericToNonGeneric()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Func<object>? factory = GetAssociatedTypeFactory(typeShape, typeof(NonGenericDataTypeConverter));
        Assert.NotNull(factory);
        Assert.IsType<NonGenericDataTypeConverter>(factory.Invoke());
    }


    [Fact]
    public void CanConstructAssociatedType_TypeArgsSplitAcrossTypes()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Func<object>? factory = GetAssociatedTypeFactory(typeShape, typeof(GenericWrapper<>.GenericNested<>));
        Assert.NotNull(factory);
        Assert.IsType<GenericWrapper<int>.GenericNested<string>>(factory.Invoke());
    }

    [Fact]
    public void AssociatedTypeAttribute_ConstructorParameter()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(CustomTypeWithCustomConverter));
        Assert.NotNull(typeShape);
        IObjectTypeShape<CustomTypeConverter>? associatedShape = (IObjectTypeShape<CustomTypeConverter>?)typeShape.GetAssociatedTypeShape(typeof(CustomTypeConverter));
        Assert.NotNull(associatedShape);
        Func<object>? factory = associatedShape.GetDefaultConstructor();
        Assert.NotNull(factory);
        Assert.IsType<CustomTypeConverter>(factory.Invoke());

        if (partialShapesSupported)
        {
            // Verify the associated type's shape is only partially available.
            Assert.Empty(associatedShape.Properties);
        }
    }

    [Fact]
    public void AssociatedTypeAttribute_NamedParameter()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(CustomTypeWithCustomConverter));
        Assert.NotNull(typeShape);
        IObjectTypeShape? associatedShape = (IObjectTypeShape<CustomTypeConverter1>?)typeShape.GetAssociatedTypeShape(typeof(CustomTypeConverter1));
        Assert.NotNull(associatedShape);
        Func<object>? factory1 = associatedShape.GetDefaultConstructor();
        Assert.NotNull(factory1);
        Assert.IsType<CustomTypeConverter1>(factory1.Invoke());
        if (partialShapesSupported)
        {
            Assert.Empty(associatedShape.Properties); // Verify the associated type's shape is only partially available.
        }

        associatedShape = (IObjectTypeShape?)typeShape.GetAssociatedTypeShape(typeof(CustomTypeConverter2));
        Assert.NotNull(associatedShape);
        Func<object>? factory2 = associatedShape.GetDefaultConstructor();
        Assert.NotNull(factory2);
        Assert.IsType<CustomTypeConverter2>(factory2.Invoke());
        if (partialShapesSupported)
        {
            Assert.Empty(associatedShape.Properties); // Verify the associated type's shape is only partially available.
        }
    }

    [Fact]
    public void TypeShapeExtension_AssociatedShape()
    {
        // Get it through the associated type shape API, which accepts an unbound generic type.
        // We do this by first starting with a known shape, then jumping to the associated type.
        ITypeShape? ordinaryShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(ordinaryShape);

        // Fetch as an independent shape.
        IObjectTypeShape? associatedShape = (IObjectTypeShape<ExtraShape<int, string>>?)providerUnderTest.Provider.GetShape(typeof(ExtraShape<int, string>));
        Assert.NotNull(associatedShape);

        // Fetch as an associated shape by its unbound generic.
        associatedShape = (IObjectTypeShape<ExtraShape<int, string>>?)ordinaryShape.GetAssociatedTypeShape(typeof(ExtraShape<,>));
        Assert.NotNull(associatedShape);

        // Verify a wholly defined shape.
        Assert.NotEmpty(associatedShape.Properties);

        // Fetch as an associated shape by its closed generic.
        associatedShape = (IObjectTypeShape<ExtraShape<int, string>>?)ordinaryShape.GetAssociatedTypeShape(typeof(ExtraShape<int, string>));
        Assert.NotNull(associatedShape);
    }

    [Fact]
    public void TypeShapeExtension_AssociatedShape_UnionFlags()
    {
        // Get it through the associated type shape API, which accepts an unbound generic type.
        // We do this by first starting with a known shape, then jumping to the associated type.
        ITypeShape? ordinaryShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(ordinaryShape);

        // Fetch as an independent shape.
        IObjectTypeShape? associatedShape = (IObjectTypeShape<ExtraShape2<int, string>>?)providerUnderTest.Provider.GetShape(typeof(ExtraShape2<int, string>));
        Assert.NotNull(associatedShape);

        // Fetch as an associated shape by its unbound generic.
        associatedShape = (IObjectTypeShape<ExtraShape2<int, string>>?)ordinaryShape.GetAssociatedTypeShape(typeof(ExtraShape2<,>));
        Assert.NotNull(associatedShape);

        // Verify a shape with all expected elements.
        Assert.NotEmpty(associatedShape.Properties);
        Assert.NotNull(associatedShape.Constructor);

        // Fetch as an associated shape by its closed generic.
        associatedShape = (IObjectTypeShape<ExtraShape2<int, string>>?)ordinaryShape.GetAssociatedTypeShape(typeof(ExtraShape2<int, string>));
        Assert.NotNull(associatedShape);
    }

    private static Func<object>? GetAssociatedTypeFactory(ITypeShape typeShape, Type associatedType)
        => (typeShape.GetAssociatedTypeShape(associatedType) as IObjectTypeShape)?.GetDefaultConstructor();

    [GenerateShape]
    [AssociatedTypeShape(typeof(NonGenericDataTypeConverter))]
    public partial class NonGenericDataType;

    public class NonGenericDataTypeConverter;

    [AssociatedTypeShape(typeof(GenericDataTypeConverter<,>), typeof(GenericDataTypeCloner<,>), typeof(NonGenericDataTypeConverter), typeof(GenericWrapper<>.GenericNested<>))]
    public class GenericDataType<T1, T2>;

    public class GenericDataTypeConverter<T1, T2>;
    public class GenericDataTypeCloner<T1, T2>;
    public class GenericDataTypeVerifier<T1, T2>
    {
        public string? Property { get; set; }
    }

    /// <summary>
    /// A class that should <em>not</em> be directly referenced by any other shaped type.
    /// It should have a shape generated for it, due to type extensions.
    /// </summary>
    public class ExtraShape<T1, T2>
    {
        public int MyProperty { get; set; }
    }

    /// <summary>
    /// A class that should <em>not</em> be directly referenced by any other shaped type.
    /// It should have a shape generated for it, due to type extensions.
    /// </summary>
    public class ExtraShape2<T1, T2>
    {
        public int MyProperty { get; set; }
    }

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

    public class CustomTypeConverter { public string? MyProperty { get; set; } }
    public class CustomTypeConverter1 { public string? MyProperty { get; set; } }
    public class CustomTypeConverter2 { public string? MyProperty { get; set; } }

    [AssociatedTypeAttribute(nameof(type), TypeShapeDepth.Constructor)]
    internal class MyConverterAttribute(Type type) : Attribute
    {
        public Type Type => type;
    }

    [AssociatedTypeAttribute(nameof(Types), TypeShapeDepth.Constructor)]
    internal class MyConverterNamedArgAttribute : Attribute
    {
        public Type[] Types { get; init; } = [];
    }

    public sealed class Reflection() : AssociatedTypesTests(RefectionProviderUnderTest.NoEmit, partialShapesSupported: false);
    public sealed class ReflectionEmit() : AssociatedTypesTests(RefectionProviderUnderTest.Emit, partialShapesSupported: false);
    public sealed class SourceGen() : AssociatedTypesTests(new SourceGenProviderUnderTest(Witness.ShapeProvider), partialShapesSupported: true);
}
