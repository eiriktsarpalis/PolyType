using PolyType.Examples.RandomGenerator;
using TUnit.Core;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for random generation in Native AOT.
/// </summary>
public class RandomGenerationTests
{
    [Test]
    public async Task CanGenerateSimpleDataInstances()
    {
        // Generate multiple values to test the generator
        var instances = RandomGenerator.GenerateValues<SimpleTestData>()
            .Take(10)
            .ToArray();

        await Assert.That(instances).HasCount().EqualTo(10);
    }

    [Test]
    public async Task CanGenerateTodosDataInstances()
    {
        // Generate multiple values to test the generator
        var todosList = RandomGenerator.GenerateValues<TestTodos>()
            .Take(10)
            .ToArray();

        await Assert.That(todosList).HasCount().EqualTo(10);
    }
}