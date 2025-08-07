using TUnit.Core;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for random generation in Native AOT.
/// </summary>
public class RandomGenerationTests
{
    [Test]
    public void CanGenerateSimpleDataInstances()
    {
        // Generate multiple values to test the generator
        var instances = PolyType.Examples.RandomGenerator.RandomGenerator.GenerateValues<SimpleTestData>()
            .Take(2)
            .ToList();

        Assert.That(instances.Count).IsGreaterThanOrEqualTo(2);
        Assert.That(instances[0]).IsNotNull();
        Assert.That(instances[1]).IsNotNull();
        Assert.That(instances[0].StringValue).IsNotNull();
    }

    [Test]
    public void CanGenerateTodosDataInstances()
    {
        // Generate multiple values to test the generator
        var todosList = PolyType.Examples.RandomGenerator.RandomGenerator.GenerateValues<TestTodos>()
            .Take(2)
            .ToList();

        Assert.That(todosList.Count).IsGreaterThanOrEqualTo(2);
        Assert.That(todosList[0]).IsNotNull();
        Assert.That(todosList[1]).IsNotNull();
        Assert.That(todosList[0].Items).IsNotNull();
        Assert.That(todosList[1].Items).IsNotNull();
    }
}