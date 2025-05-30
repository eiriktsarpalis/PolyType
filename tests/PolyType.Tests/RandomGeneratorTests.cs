﻿using PolyType.Abstractions;
using PolyType.Examples.RandomGenerator;
using PolyType.Examples.StructuralEquality;
using Xunit;

namespace PolyType.Tests;

public abstract class RandomGeneratorTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void ProducesDeterministicRandomValues<T>(TestCase<T> testCase)
    {
        (RandomGenerator<T> generator, IEqualityComparer<T> comparer) = GetGeneratorAndEqualityComparer(testCase);

        if (!providerUnderTest.HasConstructor(testCase))
        {
            Assert.Throws<NotSupportedException>(() => generator.GenerateValue(10));
            return;
        }

        const int Seed = 42;
        IEnumerable<T> firstRandomSequence = generator.GenerateValues(Seed).Take(10);
        IEnumerable<T> secondRandomSequence = generator.GenerateValues(Seed).Take(10);
        Assert.Equal(firstRandomSequence, secondRandomSequence, comparer);
    }

    private (RandomGenerator<T>, IEqualityComparer<T>) GetGeneratorAndEqualityComparer<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        return (RandomGenerator.Create(shape), StructuralEqualityComparer.Create(shape));
    }
}

public sealed class RandomGeneratorTests_Reflection() : RandomGeneratorTests(ReflectionProviderUnderTest.NoEmit);
public sealed class RandomGeneratorTests_ReflectionEmit() : RandomGeneratorTests(ReflectionProviderUnderTest.Emit);
public sealed class RandomGeneratorTests_SourceGen() : RandomGeneratorTests(SourceGenProviderUnderTest.Default);