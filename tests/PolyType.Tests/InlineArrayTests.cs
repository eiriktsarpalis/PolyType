using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace PolyType.Tests;

public abstract partial class InlineArrayTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public unsafe void FixedArrays_Byte()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithFixedArrayByte, byte>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithFixedArrayByte>());
        AssertCommonQualities(shape);

        var ctor = shape.GetParameterizedConstructor();
        var value = ctor([1, 2, 3]);
        Assert.Equal(1, value.Element0[0]);
        Assert.Equal(2, value.Element0[1]);
        Assert.Equal(3, value.Element0[2]);

        var getEnumerable = shape.GetGetEnumerable();
        Assert.Equal([1, 2, 3], getEnumerable(value).ToArray());
    }

    [Fact]
    public unsafe void FixedArrays_Int()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithFixedArrayInt, int>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithFixedArrayInt>());
        AssertCommonQualities(shape);

        var ctor = shape.GetParameterizedConstructor();
        var value = ctor([1, 2, 3]);
        Assert.Equal(1, value.Element0[0]);
        Assert.Equal(2, value.Element0[1]);
        Assert.Equal(3, value.Element0[2]);

        var getEnumerable = shape.GetGetEnumerable();
        Assert.Equal([1, 2, 3], getEnumerable(value).ToArray());
    }

#if NET
    [Fact]
    public void InlineArrays_Primitive()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithInlineArrayPrimitive, byte>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithInlineArrayPrimitive>());
        AssertCommonQualities(shape);

        var ctor = shape.GetParameterizedConstructor();
        var value = ctor([1, 2, 3]);
        Assert.Equal(1, value[0]);
        Assert.Equal(2, value[1]);
        Assert.Equal(3, value[2]);

        var getEnumerable = shape.GetGetEnumerable();
        Assert.Equal([1, 2, 3], getEnumerable(value).ToArray());
    }

    [Fact]
    public void InlineArrays_String()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithInlineArrayString, string>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithInlineArrayString>());
        AssertCommonQualities(shape);

        var ctor = shape.GetParameterizedConstructor();
        var value = ctor(["a", "b", "c"]);
        Assert.Equal("a", value[0]);
        Assert.Equal("b", value[1]);
        Assert.Equal("c", value[2]);

        var getEnumerable = shape.GetGetEnumerable();
        Assert.Equal(["a", "b", "c"], getEnumerable(value).ToArray());
    }
#endif

    private static void AssertCommonQualities(IEnumerableTypeShape shape)
    {
        Assert.Equal(CollectionConstructionStrategy.Parameterized, shape.ConstructionStrategy);
        Assert.False(shape.IsSetType);
        Assert.False(shape.IsAsyncEnumerable);
        Assert.Equal(CollectionComparerOptions.None, shape.SupportedComparer);
    }

    [GenerateShape]
    public partial struct StructWithFixedArrayByte
    {
        public const int Length = 3;

        public unsafe fixed byte Element0[Length];
    }

    [GenerateShape]
    public partial struct StructWithFixedArrayInt
    {
        public const int Length = 3;

        public unsafe fixed int Element0[Length];
    }

#if NET
    [GenerateShape]
    [InlineArray(Length)]
    public partial struct StructWithInlineArrayPrimitive
    {
        public const int Length = 3;

        private byte element0;
    }

    [GenerateShape]
    [InlineArray(Length)]
    public partial struct StructWithInlineArrayString
    {
        public const int Length = 3;

        private string element0;
    }
#endif


    [GenerateShapeFor<StructWithFixedArrayByte>]
    partial class Witness;

    public sealed class Reflection() : InlineArrayTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : InlineArrayTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : InlineArrayTests(new SourceGenProviderUnderTest(Witness.GeneratedTypeShapeProvider));
}
