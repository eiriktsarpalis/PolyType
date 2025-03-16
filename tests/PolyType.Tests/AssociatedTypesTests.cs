using PolyType;
using PolyType.Tests;

[assembly: TypeShapeExtension(typeof(AssociatedTypesTests.GenericDataType<,>), AssociatedTypes = [typeof(AssociatedTypesTests.GenericDataTypeVerifier<,>)])]

namespace PolyType.Tests;

[Trait("AssociatedTypes", "true")]
public abstract partial class AssociatedTypesTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void CanConstructAssociatedType()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Type? closedGeneric = typeShape.GetAssociatedType(typeof(GenericDataTypeConverter<,>));
        Assert.Equal(typeof(GenericDataTypeConverter<int, string>), closedGeneric);
    }

    [Fact]
    public void CanConstructAssociatedType2()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Type? closedGeneric = typeShape.GetAssociatedType(typeof(GenericDataTypeCloner<,>));
        Assert.Equal(typeof(GenericDataTypeCloner<int, string>), closedGeneric);
    }

    [Fact]
    public void CanConstructAssociatedType_WithClosedGeneric()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Assert.Throws<ArgumentException>(() => typeShape.GetAssociatedType(typeof(GenericDataTypeConverter<int, string>)));
    }

    [Fact]
    public void CanConstructAssociatedType_ByExtension()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Type? closedGeneric = typeShape.GetAssociatedType(typeof(GenericDataTypeVerifier<,>));
        Assert.Equal(typeof(GenericDataTypeVerifier<int, string>), closedGeneric);
    }

    [Fact]
    public void CanConstructAssociatedType_NonGeneric()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(NonGenericDataType));
        Assert.NotNull(typeShape);
        Assert.Throws<InvalidOperationException>(() => typeShape.GetAssociatedType(typeof(NonGenericDataTypeConverter)));
    }

    [Fact]
    public void CanConstructAssociatedType_GenericToNonGeneric()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Assert.Throws<ArgumentException>(() => typeShape.GetAssociatedType(typeof(NonGenericDataTypeConverter)));
    }


    [Fact]
    public void CanConstructAssociatedType_TypeArgsSplitAcrossTypes()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(GenericDataType<int, string>));
        Assert.NotNull(typeShape);
        Type? closedGeneric = typeShape.GetAssociatedType(typeof(GenericWrapper<>.GenericNested<>));
        Assert.Equal(typeof(GenericWrapper<int>.GenericNested<string>), closedGeneric);
    }

    [Fact]
    public void AssociatedTypeAttribute_ConstructorParameter()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(CustomTypeWithCustomConverter<int>));
        Assert.NotNull(typeShape);
        Type? closedGeneric = typeShape.GetAssociatedType(typeof(CustomTypeConverter<>));
        Assert.Equal(typeof(CustomTypeConverter<int>), closedGeneric);
    }

    [Fact]
    public void AssociatedTypeAttribute_NamedParameter()
    {
        ITypeShape? typeShape = providerUnderTest.Provider.GetShape(typeof(CustomTypeWithCustomConverter<int>));
        Assert.NotNull(typeShape);
        Type? closedGeneric1 = typeShape.GetAssociatedType(typeof(CustomTypeConverter1<>));
        Assert.Equal(typeof(CustomTypeConverter1<int>), closedGeneric1);

        Type? closedGeneric2 = typeShape.GetAssociatedType(typeof(CustomTypeConverter2<>));
        Assert.Equal(typeof(CustomTypeConverter2<int>), closedGeneric2);
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

    public class GenericWrapper<T1>
    {
        public class GenericNested<T2>;
    }

    [GenerateShape<GenericDataType<int, string>>]
    [GenerateShape<CustomTypeWithCustomConverter<int>>]
    internal partial class Witness;

    [MyConverter(typeof(CustomTypeConverter<>))]
    [MyConverterNamedArg(Types = [typeof(CustomTypeConverter1<>), typeof(CustomTypeConverter2<>)])]
    internal partial class CustomTypeWithCustomConverter<T>;

    public class CustomTypeConverter<T>;
    public class CustomTypeConverter1<T>;
    public class CustomTypeConverter2<T>;

    [AssociatedTypeAttribute(nameof(type))]
    internal class MyConverterAttribute(Type type) : Attribute
    {
        public Type Type => type;
    }

    [AssociatedTypeAttribute(nameof(Types))]
    internal class MyConverterNamedArgAttribute : Attribute
    {
        public Type[] Types { get; init; } = [];
    }

    public sealed class Reflection() : AssociatedTypesTests(RefectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : AssociatedTypesTests(RefectionProviderUnderTest.Emit);
    public sealed class SourceGen() : AssociatedTypesTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
