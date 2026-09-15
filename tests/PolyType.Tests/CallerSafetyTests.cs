using PolyType.ReflectionProvider;

namespace PolyType.Tests;

public class CallerSafetyTests
{
    public static IEnumerable<object[]> UnsafeTypes()
    {
        Type[] types =
        [
            typeof(UnsafeConstructor),
            typeof(UnsafeMethods<string>),
            typeof(UnsafeProperty),
            typeof(UnsafeField),
            typeof(UnsafeEvent<string>),
            typeof(UnsafeInitOnlyProperty),
            typeof(UnsafePrivateInitializer),
            typeof(UnsafeSurrogate),
            typeof(UnsafeCollectionConstructor),
            typeof(UnsafeCollectionAppender),
            typeof(SafeGetter),
            typeof(IgnoredUnsafeMember),
            typeof(DerivedUnsafeMember),
        ];

        foreach (Type type in types)
        {
            yield return [type, false];
            yield return [type, true];
        }
    }

    [Theory]
    [MemberData(nameof(UnsafeTypes))]
    public void CallerUnsafeMembers_RejectContainingType(Type type, bool useReflectionEmit)
    {
        ReflectionTypeShapeProvider provider = ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = useReflectionEmit });
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => provider.GetTypeShape(type));
        Assert.Contains("caller-unsafe", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SafeMemberWithUnsafeBlock_IsSupported(bool useReflectionEmit)
    {
        ReflectionTypeShapeProvider provider = ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = useReflectionEmit });
        var shape = (IObjectTypeShape<SafeWrapper>)provider.GetTypeShapeOrThrow<SafeWrapper>();
        var property = Assert.IsAssignableFrom<IPropertyShape<SafeWrapper, int>>(Assert.Single(shape.Properties));
        var value = new SafeWrapper();
        Assert.Equal(42, property.GetGetter()(ref value));
        Assert.False(property.HasSetter);
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallerUnsafeCollectionBuilder_IsRejected(bool useReflectionEmit)
    {
        ReflectionTypeShapeProvider provider = ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = useReflectionEmit });
        var shape = (IEnumerableTypeShape<BuilderCollection, int>)provider.GetTypeShape<BuilderCollection>();
        Assert.Throws<NotSupportedException>(() => shape.GetParameterizedConstructor());
    }

    [System.Runtime.CompilerServices.CollectionBuilder(typeof(UnsafeBuilder), nameof(UnsafeBuilder.Create))]
    public sealed class BuilderCollection : List<int>
    {
        private BuilderCollection() { }
        internal static BuilderCollection New() => new();
    }

    public static class UnsafeBuilder
    {
        public static unsafe BuilderCollection Create(ReadOnlySpan<int> values) => BuilderCollection.New();
    }
#endif

    public sealed class UnsafeConstructor
    {
        [ConstructorShape]
        private unsafe UnsafeConstructor() { }
    }

    public sealed class UnsafeMethods<T>
    {
        [MethodShape]
        [System.Runtime.CompilerServices.CompilerGenerated]
        private unsafe T Read(T value) => value;
    }

    public sealed class UnsafeProperty
    {
        [PropertyShape]
        private unsafe int Value { get; set; }
    }

    public sealed class UnsafeInitOnlyProperty
    {
        public int Value { get => 42; unsafe init { } }
    }

    public sealed class UnsafePrivateInitializer
    {
        public int Value { get => 42; private unsafe init { } }
    }

    public sealed class UnsafeField
    {
        public unsafe int Value;
    }

    public sealed class UnsafeEvent<T>
    {
        [EventShape]
        private unsafe event Action<T> Changed { add { } remove { } }
    }

    [TypeShape(Marshaler = typeof(UnsafeMarshaler))]
    public sealed class UnsafeSurrogate;

    public sealed class UnsafeMarshaler : IMarshaler<UnsafeSurrogate, int>
    {
        public unsafe UnsafeMarshaler() { }
        public int Marshal(UnsafeSurrogate? value) => 0;
        public UnsafeSurrogate Unmarshal(int value) => new();
    }

    public sealed class UnsafeCollectionConstructor : List<int>
    {
        public unsafe UnsafeCollectionConstructor() { }
    }

    public sealed class UnsafeCollectionAppender : List<int>
    {
        public new unsafe void Add(int item) { }
    }

    public sealed class SafeGetter
    {
        public int Value { get => 42; private unsafe set { } }
    }

    public sealed class IgnoredUnsafeMember
    {
        [MethodShape(Ignore = true)]
        public unsafe int Read(int value) => value;
    }

    public class UnusedUnsafeMember
    {
        private unsafe int Read(int value) => value;
    }

    public sealed class DerivedUnsafeMember : UnusedUnsafeMember;

    public sealed class SafeWrapper
    {
        public int Value
        {
            get
            {
                unsafe int ReadCore(int* address)
                {
                    unsafe { return *address; }
                }

                int value = 42;
                unsafe { return ReadCore(&value); }
            }
        }
    }
}
