using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using PolyType.Utilities;

namespace PolyType.Tests.Utilities;

public class AggregatingTypeShapeProviderTests
{
    [Fact]
    public void Ctor_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new AggregatingTypeShapeProvider(null!));
    }

    [Fact]
    public void Ctor_NullElement()
    {
        Assert.Throws<ArgumentException>(() => new AggregatingTypeShapeProvider([null!]));
    }

    [Fact]
    public void EmptyList()
    {
        AggregatingTypeShapeProvider aggregate = new();
        Assert.Null(aggregate.GetShape(typeof(int)));
    }

    [Fact]
    public void One_NullShape()
    {
        AggregatingTypeShapeProvider aggregate = new(new MockTypeShapeProvider());
        Assert.Null(aggregate.GetShape(typeof(int)));
    }


    [Fact]
    public void One_NonNullShape()
    {
        MockTypeShapeProvider first = new()
        {
            Shapes =
            {
                [typeof(int)] = new MockTypeShape<int>(),
            },
        };
        AggregatingTypeShapeProvider aggregate = new(first);
        Assert.Same(first.Shapes[typeof(int)], aggregate.GetShape(typeof(int)));
    }

    [Fact]
    public void ManyProviders()
    {
        MockTypeShapeProvider first = new()
        {
            Shapes =
            {
                [typeof(int)] = new MockTypeShape<int>(),
            },
        };
        MockTypeShapeProvider second = new()
        {
            Shapes =
            {
                [typeof(string)] = new MockTypeShape<int>(),
            },
        };
        AggregatingTypeShapeProvider aggregate = new(first, second);
        Assert.Same(first.Shapes[typeof(int)], aggregate.GetShape(typeof(int)));
        Assert.Same(second.Shapes[typeof(string)], aggregate.GetShape(typeof(string)));
        Assert.Null(aggregate.GetShape(typeof(bool)));
    }

    [Fact]
    public void DefensiveCopy()
    {
        ITypeShapeProvider[] providers =
        [
            new MockTypeShapeProvider(),
        ];
        AggregatingTypeShapeProvider aggregate = new(providers);
        providers[0] = new MockTypeShapeProvider
        {
            Shapes =
            {
                [typeof(int)] = new MockTypeShape<int>(),
            },
        };

        // Verify that the new provider is not used.
        Assert.Null(aggregate.GetShape(typeof(int)));
    }

    [Fact]
    public void IterateOverProviders()
    {
        AggregatingTypeShapeProvider provider = new(new MockTypeShapeProvider());
        Assert.Single(provider.Providers);
    }

    /// <summary>
    /// Validates that the aggregating provider does not cache results.
    /// </summary>
    /// <remarks>
    /// Whether it caches or not is something of an arbitrary design decision.
    /// This test is to ensure that the behavior is documented and intentional.
    /// </remarks>
    [Fact]
    public void CachePolicy_ShapeNotCached()
    {
        MockTypeShapeProvider first = new()
        {
            Shapes =
            {
                [typeof(int)] = new MockTypeShape<int>(),
            },
        };
        AggregatingTypeShapeProvider aggregate = new(first);
        Assert.Same(first.Shapes[typeof(int)], aggregate.GetShape(typeof(int)));

        // Remove the shape, and verify that the aggregator no longer returns it.
        first.Shapes.Remove(typeof(int));
        Assert.Null(aggregate.GetShape(typeof(int)));
    }

    /// <summary>
    /// Validates that the aggregating provider does not cache the provider that was used previously.
    /// </summary>
    /// <remarks>
    /// Whether it caches or not is something of an arbitrary design decision.
    /// This test is to ensure that the behavior is documented and intentional.
    /// </remarks>
    [Fact]
    public void CachePolicy_ShapeProviderNotCached()
    {
        MockTypeShapeProvider first = new()
        {
            Shapes =
            {
                [typeof(int)] = new MockTypeShape<int>(),
            },
        };
        MockTypeShapeProvider second = new()
        {
            Shapes =
            {
                [typeof(string)] = new MockTypeShape<string>(),
            },
        };
        AggregatingTypeShapeProvider aggregate = new(first, second);
        Assert.NotNull(aggregate.GetShape(typeof(string)));

        // Add a new string shape to the first provider.
        first.Shapes[typeof(string)] = new MockTypeShape<string>();

        // The aggregator should return the new shape.
        Assert.Same(first.Shapes[typeof(string)], aggregate.GetShape(typeof(string)));
    }

    private class MockTypeShapeProvider : ITypeShapeProvider
    {
        internal Dictionary<Type, ITypeShape> Shapes { get; } = new();

        public ITypeShape? GetShape(Type type) => Shapes.TryGetValue(type, out ITypeShape? shape) ? shape : null;
    }

    [ExcludeFromCodeCoverage]
    private class MockTypeShape<T> : ITypeShape<T>
    {
        public Type Type => throw new NotImplementedException();

        public TypeShapeKind Kind => throw new NotImplementedException();

        public ITypeShapeProvider Provider => throw new NotImplementedException();

        public ICustomAttributeProvider? AttributeProvider => throw new NotImplementedException();

        public object? Accept(ITypeShapeVisitor visitor, object? state = null)
        {
            throw new NotImplementedException();
        }

        public Func<object>? GetAssociatedTypeFactory(Type associatedType) => throw new NotImplementedException();

        public object? Invoke(ITypeShapeFunc func, object? state = null)
        {
            throw new NotImplementedException();
        }
    }
}
