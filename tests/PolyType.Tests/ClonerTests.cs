using PolyType.Abstractions;
using PolyType.Examples.Cloner;
using PolyType.Examples.StructuralEquality;
using Xunit;

namespace PolyType.Tests;

public abstract class ClonerTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void Cloner_ProducesEqualCopy<T>(TestCase<T> testCase)
    {
        (Func<T?, T?> cloner, IEqualityComparer<T> comparer) = GetClonerAndEqualityComparer(testCase);

        if (testCase.Value is not null && !providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => cloner(testCase.Value));
            return;
        }

        T? clonedValue = cloner(testCase.Value);

        if (testCase.Value is null)
        {
            Assert.Null(clonedValue);
            return;
        }

        if (typeof(T) != typeof(string) && testCase.CustomKind is not TypeShapeKind.None && !testCase.IsUnion)
        {
            Assert.NotSame((object?)testCase.Value, (object?)clonedValue);
        }

        if (testCase.IsStack)
        {
            clonedValue = cloner(clonedValue);
        }

        Assert.Equal(testCase.Value, clonedValue, comparer!);
    }

    private (Func<T?, T?>, IEqualityComparer<T>) GetClonerAndEqualityComparer<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        return (Cloner.CreateCloner(shape), StructuralEqualityComparer.Create(shape));
    }
}

public sealed class ClonerTests_Reflection() : ClonerTests(ReflectionProviderUnderTest.NoEmit);
public sealed class ClonerTests_ReflectionEmit() : ClonerTests(ReflectionProviderUnderTest.Emit);
public sealed class ClonerTests_SourceGen() : ClonerTests(SourceGenProviderUnderTest.Default);