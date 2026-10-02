using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace PolyType.Tests;

public abstract partial class InlineArrayTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void FixedArrays_ConstructorRejectsIncorrectLength(int length)
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithFixedArrayInt, int>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithFixedArrayInt>());
        var constructor = shape.GetParameterizedConstructor();
        Assert.Throws<ArgumentException>(() => constructor(new int[length]));
    }

    [Fact]
    public void FixedArrays_Byte()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithFixedArrayByte, byte>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithFixedArrayByte>());
        AssertCommonQualities(shape);

        var ctor = shape.GetParameterizedConstructor();
        var value = ctor([1, 2, 3]);
        unsafe
        {
            Assert.Equal(1, value.Element0[0]);
            Assert.Equal(2, value.Element0[1]);
            Assert.Equal(3, value.Element0[2]);
        }

        var enumerable = shape.GetGetEnumerable()(value);
        Assert.Equal([1, 2, 3], enumerable.ToArray());
        AssertReadOnlyCollectionView<byte>(enumerable, [1, 2, 3]);
    }

    [Fact]
    public void FixedArrays_Count()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithFixedArrayInt, int>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithFixedArrayInt>());

        StructWithFixedArrayInt value = new();
        unsafe
        {
            value.Element0[0] = 1;
            value.Element0[1] = 2;
            value.Element0[2] = 3;
        }

        var enumerable = shape.GetGetEnumerable()(value);

        Assert.True(enumerable.TryGetNonEnumeratedCount(out int count));
        Assert.Equal(3, count);
    }

    [Fact]
    public void FixedArrays_Int()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithFixedArrayInt, int>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithFixedArrayInt>());
        AssertCommonQualities(shape);

        var ctor = shape.GetParameterizedConstructor();
        var value = ctor([1, 2, 3]);
        unsafe
        {
            Assert.Equal(1, value.Element0[0]);
            Assert.Equal(2, value.Element0[1]);
            Assert.Equal(3, value.Element0[2]);
        }

        var enumerable = shape.GetGetEnumerable()(value);
        Assert.Equal([1, 2, 3], enumerable.ToArray());
        AssertReadOnlyCollectionView(enumerable, [1, 2, 3]);
    }

    [Fact]
    public void FixedArrays_EnumeratorsHaveOwnState()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithFixedArrayInt, int>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithFixedArrayInt>());

        StructWithFixedArrayInt value = new();
        unsafe
        {
            value.Element0[0] = 1;
            value.Element0[1] = 2;
            value.Element0[2] = 3;
        }

        var enumerable = shape.GetGetEnumerable()(value);
        var enumerator1 = enumerable.GetEnumerator();
        var enumerator2 = enumerable.GetEnumerator();

        Assert.True(enumerator1.MoveNext());
        Assert.True(enumerator1.MoveNext());

        Assert.True(enumerator2.MoveNext());

        Assert.Equal(2, enumerator1.Current);
        Assert.Equal(1, enumerator2.Current);
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

        var enumerable = shape.GetGetEnumerable()(value);
        Assert.Equal([1, 2, 3], enumerable.ToArray());
        AssertReadOnlyCollectionView<byte>(enumerable, [1, 2, 3]);
    }

#if NET
    [Fact]
    public void InlineArrays_Count()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithInlineArrayPrimitive, byte>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithInlineArrayPrimitive>());

        StructWithInlineArrayPrimitive value = new();
        value[0] = 1;
        value[1] = 2;
        value[2] = 3;

        var enumerable = shape.GetGetEnumerable()(value);
        Assert.True(enumerable.TryGetNonEnumeratedCount(out int count));
        Assert.Equal(3, count);
    }
#endif

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
        AssertReadOnlyCollectionView(getEnumerable(value), ["a", "b", "c"]);
    }

    [Fact]
    public void InlineArrays_EnumeratorsHaveOwnState()
    {
        var shape = Assert.IsAssignableFrom<IEnumerableTypeShape<StructWithInlineArrayPrimitive, byte>>(
            providerUnderTest.Provider.GetTypeShapeOrThrow<StructWithInlineArrayPrimitive>());

        StructWithInlineArrayPrimitive value = new();
        value[0] = 1;
        value[1] = 2;
        value[2] = 3;

        var enumerable = shape.GetGetEnumerable()(value);
        var enumerator1 = enumerable.GetEnumerator();
        var enumerator2 = enumerable.GetEnumerator();

        Assert.True(enumerator1.MoveNext());
        Assert.True(enumerator1.MoveNext());

        Assert.True(enumerator2.MoveNext());

        Assert.Equal(2, enumerator1.Current);
        Assert.Equal(1, enumerator2.Current);
    }
#endif

    private static void AssertCommonQualities(IEnumerableTypeShape shape)
    {
        Assert.Equal(CollectionConstructionStrategy.Parameterized, shape.ConstructionStrategy);
        Assert.False(shape.IsSetType);
        Assert.False(shape.IsAsyncEnumerable);
        Assert.Equal(CollectionComparerOptions.None, shape.SupportedComparer);
    }

    private static void AssertReadOnlyCollectionView<TElement>(IEnumerable<TElement> enumerable, TElement[] expected)
    {
        var collection = Assert.IsAssignableFrom<ICollection<TElement>>(enumerable);
        Assert.Equal(expected.Length, collection.Count);
        Assert.True(collection.IsReadOnly);
        Assert.All(expected, element => Assert.True(collection.Contains(element)));
        Assert.False(collection.Contains(default!));

        TElement[] destination = new TElement[expected.Length + 2];
        collection.CopyTo(destination, 1);
        Assert.Equal(expected, destination.Skip(1).Take(expected.Length));
        Assert.Equal(default, destination[0]);
        Assert.Equal(default, destination[destination.Length - 1]);

        Assert.Throws<ArgumentNullException>(() => collection.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => collection.CopyTo(destination, -1));
        Assert.Throws<ArgumentException>(() => collection.CopyTo(new TElement[expected.Length], 1));
        Assert.Throws<NotSupportedException>(() => collection.Add(default!));
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Throws<NotSupportedException>(() => collection.Remove(default!));

        IEnumerator enumerator = ((IEnumerable)enumerable).GetEnumerator();
        Assert.Throws<InvalidOperationException>(() => enumerator.Current);
        foreach (TElement element in expected)
        {
            Assert.True(enumerator.MoveNext());
            Assert.Equal(element, enumerator.Current);
        }

        Assert.False(enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Current);
        enumerator.Reset();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(expected[0], enumerator.Current);
        ((IDisposable)enumerator).Dispose();
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
