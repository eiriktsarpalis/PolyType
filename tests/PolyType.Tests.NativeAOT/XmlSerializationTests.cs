using PolyType.Examples.XmlSerializer;
using TUnit.Core;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for XML serialization in Native AOT.
/// </summary>
public class XmlSerializationTests
{
    [Test]
    public void CanSerializeAndDeserializeSimpleData()
    {
        var originalData = TestDataFactory.CreateSimpleData();
        var xml = XmlSerializer.Serialize(originalData);
        var deserializedData = XmlSerializer.Deserialize<SimpleTestData>(xml);

        Assert.That(deserializedData).IsNotNull();
        Assert.That(deserializedData.IntValue).IsEqualTo(originalData.IntValue);
    }

    [Test]
    public void CanSerializeAndDeserializeTodosData()
    {
        var originalTodos = TestDataFactory.CreateSampleTodos();
        var xml = XmlSerializer.Serialize(originalTodos);
        var deserializedTodos = XmlSerializer.Deserialize<TestTodos>(xml);

        Assert.That(deserializedTodos).IsNotNull();
        Assert.That(deserializedTodos.Items.Length).IsEqualTo(originalTodos.Items.Length);
    }
}