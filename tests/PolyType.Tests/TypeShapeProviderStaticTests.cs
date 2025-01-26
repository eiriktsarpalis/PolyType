using System.Reflection;

namespace PolyType.Tests;

public class TypeShapeProviderStaticTests
{
    [Fact]
    public void Combine_EmptyList()
    {
        ITypeShapeProvider aggregate = TypeShapeProvider.Combine();
        Assert.NotNull(aggregate);
        Assert.Null(aggregate.GetShape(typeof(int)));
    }

    [Fact]
    public void Combine_One_Null()
    {
        ITypeShapeProvider aggregate = TypeShapeProvider.Combine(new MockTypeShapeProvider());
        Assert.Null(aggregate.GetShape(typeof(int)));
    }


    [Fact]
    public void Combine_One_NonNull()
    {
        MockTypeShapeProvider first = new()
        {
            Shapes =
            {
                [typeof(int)] = new MockTypeShape<int>(),
            },
        };
        ITypeShapeProvider aggregate = TypeShapeProvider.Combine(first);
        Assert.Same(first.Shapes[typeof(int)], aggregate.GetShape(typeof(int)));
    }

    [Fact]
    public void Combine_Many()
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
        ITypeShapeProvider aggregate = TypeShapeProvider.Combine(first, second);
        Assert.Same(first.Shapes[typeof(int)], aggregate.GetShape(typeof(int)));
        Assert.Same(second.Shapes[typeof(string)], aggregate.GetShape(typeof(string)));
        Assert.Null(aggregate.GetShape(typeof(bool)));
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
        ITypeShapeProvider aggregate = TypeShapeProvider.Combine(first);
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
        ITypeShapeProvider aggregate = TypeShapeProvider.Combine(first, second);
        Assert.NotNull(aggregate.GetShape(typeof(string)));

        // Add a new string shape to the first provider.
        first.Shapes[typeof(string)] = new MockTypeShape<string>();

        // The aggregator should return the new shape.
        Assert.Same(first.Shapes[typeof(string)], aggregate.GetShape(typeof(string)));
    }

    private class MockTypeShapeProvider : ITypeShapeProvider
    {
        internal Dictionary<Type, ITypeShape> Shapes { get; } = new();

        public ITypeShape? GetShape(Type type) => this.Shapes.TryGetValue(type, out ITypeShape? shape) ? shape : null;
    }

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

        public object? Invoke(ITypeShapeFunc func, object? state = null)
        {
            throw new NotImplementedException();
        }
    }
}
