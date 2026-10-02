using PolyType.Abstractions;
using PolyType.Examples.RandomGenerator;
using PolyType.Examples.StructuralEquality;
using Xunit;

namespace PolyType.Tests;

public abstract class RandomGeneratorTests(ProviderUnderTest providerUnderTest)
{
#if NET
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    public void ShapeableEntryPoints_PreserveSeedsAndSizeBounds(int size)
    {
        var generator = RandomGenerator.Create<SimpleRecord>(providerUnderTest.Provider);
        Assert.Equal(generator.GenerateValue(size, seed: 42), RandomGenerator.GenerateValue<SimpleRecord>(size, seed: 42));
        Assert.Equal(generator.GenerateValues(seed: 42, minSize: size, maxSize: size).Take(3),
            RandomGenerator.GenerateValues<SimpleRecord>(seed: 42, minSize: size, maxSize: size).Take(3));
        var primitiveGenerator = RandomGenerator.Create<int>(providerUnderTest.Provider);
        Assert.Equal(primitiveGenerator.GenerateValue(size, seed: 42), RandomGenerator.GenerateValue<int, Witness>(size, seed: 42));
        Assert.Equal(primitiveGenerator.GenerateValues(seed: 42, minSize: size, maxSize: size).Take(3),
            RandomGenerator.GenerateValues<int, Witness>(seed: 42, minSize: size, maxSize: size).Take(3));
    }
#endif

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    public void InvalidSizeBounds_AreRejected(int minSize, int maxSize)
    {
        var generator = RandomGenerator.Create<int>(providerUnderTest.Provider);
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.GenerateValue(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.GenerateValues(minSize: minSize, maxSize: maxSize).First());
    }

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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void NullableUnionCaseUsesItsPayloadGeneratorAtEverySize(int size)
    {
        var shape = providerUnderTest.ResolveShape(TestCase.Create(new CSharpClassUnion(42)));
        var payloadGenerator = RandomGenerator.Create<string>(providerUnderTest.Provider);
        CSharpClassUnion value = RandomGenerator.Create(shape)(new LastChoiceRandom(), size);

        Assert.NotNull(value);
        Assert.Equal(payloadGenerator(new LastChoiceRandom(), size), value.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void NonNullableUnionCaseDoesNotOverridePayloadSize(int size)
    {
        var shape = providerUnderTest.ResolveShape(TestCase.Create(new CSharpConstructorUnion(new CSharpDog("Rex", 10))));
        var payloadGenerator = RandomGenerator.Create<CSharpDog>(providerUnderTest.Provider);
        CSharpConstructorUnion value = RandomGenerator.Create(shape)(new LastChoiceRandom(), size);

        Assert.Equal(payloadGenerator(new LastChoiceRandom(), size), value.Value);
        Assert.Equal(nameof(CSharpDog), value.ConstructorUsed);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 3)]
    [InlineData(10, 3)]
    public void RecursiveUnionGenerationFollowsCaseSelection(int size, int depth)
    {
        ITypeShape<CSharpRecursiveUnion> shape = providerUnderTest.ResolveShape(TestCase.Create(new CSharpRecursiveUnion(true)));
        RandomGenerator<CSharpRecursiveUnion> generator = RandomGenerator.Create(shape);
        var random = new SequenceRandom([.. Enumerable.Repeat(1, depth), 0, 1]);
        CSharpRecursiveUnion value = generator(random, size);

        for (int i = 0; i < depth; i++)
        {
            value = Assert.IsType<CSharpRecursiveUnion>(value.Value);
        }

        Assert.True(Assert.IsType<bool>(value.Value));
        Assert.Equal(0, random.RemainingChoices);
    }

    private (RandomGenerator<T>, IEqualityComparer<T>) GetGeneratorAndEqualityComparer<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        return (RandomGenerator.Create(shape), StructuralEqualityComparer.Create(shape));
    }

    private sealed class LastChoiceRandom() : Random(42)
    {
        public override int Next(int maxValue) => maxValue - 1;
        public override int Next(int minValue, int maxValue) => maxValue > minValue ? maxValue - 1 : minValue;
    }

    private sealed class SequenceRandom(int[] choices) : Random
    {
        private readonly Queue<int> _choices = new(choices);
        public int RemainingChoices => _choices.Count;

        public override int Next(int maxValue) => Next(0, maxValue);

        public override int Next(int minValue, int maxValue)
        {
            Assert.NotEmpty(_choices);
            int choice = _choices.Dequeue();
            Assert.InRange(choice, minValue, maxValue - 1);
            return choice;
        }
    }
}

public sealed class RandomGeneratorTests_Reflection() : RandomGeneratorTests(ReflectionProviderUnderTest.NoEmit);
public sealed class RandomGeneratorTests_ReflectionEmit() : RandomGeneratorTests(ReflectionProviderUnderTest.Emit);
public sealed class RandomGeneratorTests_SourceGen() : RandomGeneratorTests(SourceGenProviderUnderTest.Default);