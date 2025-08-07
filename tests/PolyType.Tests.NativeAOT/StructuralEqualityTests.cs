using TUnit.Core;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for structural equality in Native AOT.
/// </summary>
public class StructuralEqualityTests
{
    [Test]
    public void CanCompareEqualSimpleData()
    {
        var data1 = TestDataFactory.CreateSimpleData();
        var data2 = TestDataFactory.CreateSimpleData();
        var areEqual = PolyType.Examples.StructuralEquality.StructuralEqualityComparer.Equals(data1, data2);
        Assert.That(areEqual).IsTrue();
    }

    [Test]
    public void CanCompareDifferentSimpleData()
    {
        var data1 = TestDataFactory.CreateSimpleData();
        var data2 = new SimpleTestData(999, "Different", false, 2.71828, DateTime.Now);
        var areEqual = PolyType.Examples.StructuralEquality.StructuralEqualityComparer.Equals(data1, data2);
        Assert.That(areEqual).IsFalse();
    }
}