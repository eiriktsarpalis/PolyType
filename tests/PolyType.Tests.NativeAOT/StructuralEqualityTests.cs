using PolyType.Examples.StructuralEquality;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for structural equality in Native AOT.
/// </summary>
public class StructuralEqualityTests
{
    [Test]
    public async ValueTask CanCompareEqualSimpleData()
    {
        var data1 = TestDataFactory.CreateSimpleData();
        var data2 = TestDataFactory.CreateSimpleData();
        await Assert.That(data1).IsNotSameReferenceAs(data2);
        await Assert.That(StructuralEqualityComparer.Equals(data1, data2)).IsTrue();
    }

    [Test]
    public async ValueTask CanCompareDifferentSimpleData()
    {
        var data1 = TestDataFactory.CreateSimpleData();
        var data2 = data1 with { IntValue = data1.IntValue + 1 };
        await Assert.That(StructuralEqualityComparer.Equals(data1, data2)).IsFalse();
    }

    [Test]
    public async ValueTask CanCompareEqualTodosData()
    {
        var todos1 = TestDataFactory.CreateSampleTodos();
        var todos2 = TestDataFactory.CreateSampleTodos();
        await Assert.That(todos1).IsNotSameReferenceAs(todos2);
        await Assert.That(StructuralEqualityComparer.Equals(todos1, todos2)).IsTrue();
    }

    [Test]
    public async ValueTask CanCompareDifferentTodosData()
    {
        var todos1 = TestDataFactory.CreateSampleTodos();
        var todos2 = todos1 with { Items = todos1.Items.Select(t => t with { Id = t.Id + 1 }).ToArray() };
        await Assert.That(StructuralEqualityComparer.Equals(todos1, todos2)).IsFalse();
    }
}