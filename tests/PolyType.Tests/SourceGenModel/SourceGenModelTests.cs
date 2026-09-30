using PolyType.SourceGenModel;
using System.Runtime.CompilerServices;

namespace PolyType.Tests.SourceGenModel;

public static class SourceGenModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void DistinctInstancesRemainUnequalForTheSameType(bool sameProvider)
    {
        var provider = new TestProvider();
        var first = new SourceGenObjectTypeShape<int> { Provider = provider };
        var second = new SourceGenSurrogateTypeShape<int, int>
        {
            Provider = sameProvider ? provider : new TestProvider(),
            SurrogateTypeFactory = () => first,
            Marshaler = SubtypeMarshaler<int, int>.Instance,
        };
        var matchingModel = new SourceGenObjectTypeShape<int> { Provider = provider };
        var differentType = new SourceGenObjectTypeShape<long> { Provider = provider };
        var firstBody = new SourceGenObjectTypeShape<int> { Provider = provider, IsContextual = true };
        var secondBody = new SourceGenObjectTypeShape<int> { Provider = provider, IsContextual = true };

        Assert.NotSame(first, second);
        Assert.NotEqual(first.Kind, second.Kind);
        Assert.False(first.Equals(second));
        Assert.False(second.Equals(first));
        Assert.False(first.Equals(matchingModel));
        Assert.False(matchingModel.Equals(first));
        Assert.False(first.Equals(differentType));
        Assert.False(differentType.Equals(first));
        Assert.False(first.Equals(firstBody));
        Assert.False(firstBody.Equals(first));
        Assert.False(firstBody.Equals(secondBody));
        Assert.Equal(RuntimeHelpers.GetHashCode(firstBody), firstBody.GetHashCode());
        Assert.Equal(6, new HashSet<ITypeShape> { first, second, matchingModel, differentType, firstBody, secondBody }.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnsetIsContextualUsesUnversionedProviderLookup(bool returnsSameInstance)
    {
        ITypeShape? lookupResult = null;
        int lookupCount = 0;
        var provider = new TestSourceGenProvider(type =>
        {
            Assert.Equal(typeof(int), type);
            Assert.NotNull(lookupResult);
            lookupCount++;
            return lookupResult;
        });
        var shape = new SourceGenObjectTypeShape<int> { Provider = provider };
        lookupResult = returnsSameInstance ? shape : new SourceGenObjectTypeShape<int> { Provider = provider };

        Assert.Null(provider.SourceGeneratorVersion);
        Assert.Equal(0, lookupCount);
        Assert.Equal(!returnsSameInstance, shape.IsContextual);
        Assert.Equal(1, lookupCount);
        Assert.Equal(!returnsSameInstance, shape.IsContextual);
        Assert.Equal(1, lookupCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ExplicitIsContextualDoesNotUseProviderLookup(bool isContextual)
    {
        var provider = new TestSourceGenProvider(_ => throw new InvalidOperationException("Provider lookup must not be called."));
        var shape = new SourceGenObjectTypeShape<int> { Provider = provider, IsContextual = isContextual };

        Assert.Equal(isContextual, shape.IsContextual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnsetIsContextualDefaultsToFalseForOtherProviders(bool versionedSourceGenProvider)
    {
        ITypeShapeProvider provider = versionedSourceGenProvider
            ? new TestSourceGenProvider(_ => throw new InvalidOperationException("Provider lookup must not be called."))
            {
                SourceGeneratorVersion = "1.4.0",
            }
            : new TestProvider();
        var shape = new SourceGenObjectTypeShape<int> { Provider = provider };

        Assert.False(shape.IsContextual);
        Assert.False(shape.IsContextual);
    }

    [Fact]
    public static void UnsetUnionKindDefaultsToUnknown()
    {
        // Invoke the CLR constructor without C# required-member initialization.
        var shape = Activator.CreateInstance<SourceGenUnionTypeShape<int>>();

        Assert.Equal(0, (int)UnionTypeShapeKind.Unknown);
        Assert.Equal(UnionTypeShapeKind.Unknown, shape.UnionKind);
        Assert.Equal(UnionTypeShapeKind.Unknown, ((IUnionTypeShape)shape).UnionKind);
    }

    [Theory]
    [InlineData(UnionTypeShapeKind.Unknown)]
    [InlineData(UnionTypeShapeKind.TypeHierarchy)]
    [InlineData(UnionTypeShapeKind.FSharpUnion)]
    [InlineData(UnionTypeShapeKind.CSharpUnion)]
    public static void ExplicitUnionKindDoesNotEvaluateFactories(UnionTypeShapeKind kind)
    {
        var shape = new SourceGenUnionTypeShape<int>
        {
            Provider = new TestProvider(),
            UnionKind = kind,
            BaseTypeFactory = () => throw new InvalidOperationException("BaseTypeFactory must not be called."),
            UnionCasesFactory = () => throw new InvalidOperationException("UnionCasesFactory must not be called."),
            GetUnionCaseIndex = (ref _) => 0,
        };

        Assert.Equal(kind, shape.UnionKind);
    }

    [Fact]
    public static void UnsetUnionCaseNullabilityDefaultsToFalse()
    {
        var shape = Activator.CreateInstance<SourceGenUnionCaseShape<string, object>>();

        Assert.False(shape.IsNullable);
        Assert.False(((IUnionCaseShape)shape).IsNullable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ExplicitUnionCaseNullabilityDoesNotEvaluateFactories(bool isNullable)
    {
        var shape = new SourceGenUnionCaseShape<string, object>
        {
            Name = nameof(String),
            Tag = 0,
            Index = 0,
            IsNullable = isNullable,
            UnionCaseTypeFactory = () => throw new InvalidOperationException("UnionCaseTypeFactory must not be called."),
            Marshaler = SubtypeMarshaler<string, object>.Instance,
        };

        Assert.Equal(isNullable, shape.IsNullable);
    }

    private sealed class TestSourceGenProvider(Func<Type, ITypeShape?> lookup) : SourceGenTypeShapeProvider
    {
        public override ITypeShape? GetTypeShape(Type type) => lookup(type);
    }

    private sealed class TestProvider : ITypeShapeProvider
    {
        public ITypeShape? GetTypeShape(Type type) =>
            throw new InvalidOperationException("Provider lookup must not be called.");
    }
}
