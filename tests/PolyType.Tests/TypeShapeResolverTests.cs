using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.Tests;

public static partial class TypeShapeResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ResolveDynamic_PropagatesProviderConstructorException(bool useWitness)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => useWitness
            ? TypeShapeResolver.ResolveDynamic<ThrowingProviderType, ThrowingProviderWitness>()
            : TypeShapeResolver.ResolveDynamic<ThrowingProviderType>());

        Assert.Equal("User-thrown provider exception.", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains(nameof(ThrowingProvider), exception.StackTrace);
    }

    [Fact]
    public static void ResolveDynamic_ShapeableType_ReturnsExpectedSingleton()
    {
        ITypeShape<ResolverShapeable>? s1 = TypeShapeResolver.ResolveDynamic<ResolverShapeable>();
        ITypeShape<ResolverShapeable>? s2 = TypeShapeResolver.ResolveDynamic<ResolverShapeable>();
        Assert.NotNull(s1);
        Assert.Same(s1, s2);
    }

    [Fact]
    public static void ResolveDynamic_WithProvider_ReturnsExpectedSingleton()
    {
        ITypeShape<ResolverShapeable>? s1 = TypeShapeResolver.ResolveDynamic<ResolverShapeable, ResolverShapeProvider>();
        ITypeShape<ResolverShapeable>? s2 = TypeShapeResolver.ResolveDynamic<ResolverShapeable, ResolverShapeProvider>();
        Assert.NotNull(s1);
        Assert.Same(s1, s2);
    }

    [Fact]
    public static void ResolveDynamicOrThrow_ReturnsSameInstance()
    {
        ITypeShape<ResolverShapeable> s1 = TypeShapeResolver.ResolveDynamicOrThrow<ResolverShapeable>();
        ITypeShape<ResolverShapeable> s2 = TypeShapeResolver.ResolveDynamicOrThrow<ResolverShapeable>();
        Assert.Same(s1, s2);
    }

    [Fact]
    public static void ResolveDynamicOrThrow_WithProvider_ReturnsSameInstance()
    {
        ITypeShape<ResolverShapeable> s1 = TypeShapeResolver.ResolveDynamicOrThrow<ResolverShapeable, ResolverShapeProvider>();
        ITypeShape<ResolverShapeable> s2 = TypeShapeResolver.ResolveDynamicOrThrow<ResolverShapeable, ResolverShapeProvider>();
        Assert.Same(s1, s2);
    }

    [Fact]
    public static void ResolveDynamic_ReturnsNullForNonShapeable()
    {
        Assert.Null(TypeShapeResolver.ResolveDynamic<Unannotated>());
        Assert.Null(TypeShapeResolver.ResolveDynamic<Unannotated, ResolverShapeProvider>()); // provider does not expose Unannotated
        Assert.Throws<NotSupportedException>(() => TypeShapeResolver.ResolveDynamicOrThrow<Unannotated>());
        Assert.Throws<NotSupportedException>(() => TypeShapeResolver.ResolveDynamicOrThrow<Unannotated, ResolverShapeProvider>());
    }

    [Fact]
    public static void ResolveDynamic_SelfReferentialEnumerable_DoesNotStackOverflow()
    {
        // Regression test: self-referential enumerable types previously caused stack overflow
        // when source-generated shapes were resolved because child shapes were eagerly evaluated.
        ITypeShape<SelfReferentialList>? shape = TypeShapeResolver.ResolveDynamic<SelfReferentialList>();
        Assert.NotNull(shape);
        Assert.IsAssignableFrom<PolyType.Abstractions.IEnumerableTypeShape>(shape);
    }

    [Fact]
    public static void ResolveDynamic_SelfReferentialDictionary_DoesNotStackOverflow()
    {
        // Regression test: self-referential dictionary types previously caused stack overflow
        // when source-generated shapes were resolved because child shapes were eagerly evaluated.
        ITypeShape<SelfReferentialDictionary>? shape = TypeShapeResolver.ResolveDynamic<SelfReferentialDictionary>();
        Assert.NotNull(shape);
        Assert.IsAssignableFrom<PolyType.Abstractions.IDictionaryTypeShape>(shape);
    }

    // Shapeable type under test.
    [GenerateShape]
    public partial record ResolverShapeable(int X, string Y);

    // Separate provider type generating a shape for ResolverShapeable (used with Resolve<T, TProvider>()).
    [GenerateShapeFor(typeof(ResolverShapeable))]
    public partial class ResolverShapeProvider;

    private sealed class Unannotated;

    [TypeShapeProvider(typeof(ThrowingProvider))]
    public sealed class ThrowingProviderType;

    [TypeShapeProvider(typeof(ThrowingProvider))]
    public sealed class ThrowingProviderWitness;

    public sealed class ThrowingProvider : ITypeShapeProvider
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ThrowingProvider() => throw new TargetInvocationException("User-thrown provider exception.", new InvalidOperationException());

        public ITypeShape? GetTypeShape(Type type) => null;
    }
}
