namespace PolyType.Tests;

public static partial class TypeShapeProviderExtensionsTests
{
    [Fact]
    public static void GetTypeShapeOrThrow_Succeeds()
    {
        var provider = CreateProvider();
        ITypeShape shape1 = provider.GetTypeShapeOrThrow(typeof(AvailableType));
        Assert.NotNull(shape1);
        Assert.Same(shape1, provider.GetTypeShape(typeof(AvailableType)));
        Assert.Same(shape1, provider.GetTypeShapeOrThrow<AvailableType>()); // generic variant
        Assert.Same(shape1, provider.GetTypeShape<AvailableType>()); // nullable path returns same instance
    }

    [Fact]
    public static void GetTypeShape_ReturnsNullIfMissing()
    {
        var provider = CreateProvider();
        Assert.Null(provider.GetTypeShape<UnAvailableType>()); // generic extension should return null
        Assert.Null(provider.GetTypeShape(typeof(UnAvailableType))); // underlying GetTypeShape returns null
    }

    [Fact]
    public static void GetTypeShapeOrThrow_ThrowsWhenMissing()
    {
        var provider = CreateProvider();
        Assert.Throws<NotSupportedException>(() => provider.GetTypeShapeOrThrow(typeof(UnAvailableType)));
        Assert.Throws<NotSupportedException>(() => provider.GetTypeShapeOrThrow<UnAvailableType>());
    }

    private static ITypeShapeProvider CreateProvider() => Witness.GeneratedTypeShapeProvider;

    public class AvailableType;
    public class UnAvailableType;

    [GenerateShapeFor<AvailableType>]
    partial class Witness;
}
