using PolyType.Examples.StructuralEquality;
using PolyType.Examples.XmlSerializer;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for XML serialization in Native AOT.
/// </summary>
public class XmlSerializationTests
{
    [Test]
    public async Task CanSerializeAndDeserializeSimpleData()
    {
        var originalData = TestDataFactory.CreateSimpleData();
        var xml = XmlSerializer.Serialize(originalData);
        var deserializedData = XmlSerializer.Deserialize<SimpleTestData>(xml);

        // Assert expected XML structure
        await Assert.That(xml).Contains("<IntValue>42</IntValue>");
        await Assert.That(xml).Contains("<StringValue>Hello, Native AOT!</StringValue>");
        await Assert.That(xml).Contains("<BoolValue>true</BoolValue>");
        await Assert.That(xml).Contains("<DoubleValue>3.14159</DoubleValue>");
        await Assert.That(xml).Contains("<DateValue>2024-01-15T14:30:45</DateValue>");
        await Assert.That(StructuralEqualityComparer.Equals(originalData, deserializedData)).IsTrue();
    }

    [Test]
    public async Task CanSerializeAndDeserializeTodosData()
    {
        var originalTodos = TestDataFactory.CreateSampleTodos();
        var xml = XmlSerializer.Serialize(originalTodos);
        var deserializedTodos = XmlSerializer.Deserialize<TestTodos>(xml);

        // Assert expected XML structure
        await Assert.That(xml).Contains("<Items>");
        await Assert.That(xml).Contains("<Id>1</Id>");
        await Assert.That(xml).Contains("<Title>Test task 1</Title>");
        await Assert.That(xml).Contains("<Status>Done</Status>");
        await Assert.That(xml).Contains("<Status>InProgress</Status>");
        await Assert.That(xml).Contains("<Status>NotStarted</Status>");
        await Assert.That(StructuralEqualityComparer.Equals(originalTodos, deserializedTodos)).IsTrue();
    }
}