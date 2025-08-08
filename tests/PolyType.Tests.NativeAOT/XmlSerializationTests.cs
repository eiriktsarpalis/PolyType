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
        const string ExpectedXml = """
            <?xml version="1.0" encoding="utf-16"?>
            <value>
              <IntValue>42</IntValue>
              <StringValue>Hello, Native AOT!</StringValue>
              <BoolValue>true</BoolValue>
              <DoubleValue>3.14159</DoubleValue>
              <DateValue>2024-01-15T14:30:45</DateValue>
            </value>
            """;

        var originalData = TestDataFactory.CreateSimpleData();
        var xml = XmlSerializer.Serialize(originalData);
        var deserializedData = XmlSerializer.Deserialize<SimpleTestData>(xml);

        // Assert expected XML structure
        await Assert.That(xml).IsEqualTo(ExpectedXml);
        await Assert.That(StructuralEqualityComparer.Equals(originalData, deserializedData)).IsTrue();
    }

    [Test]
    public async Task CanSerializeAndDeserializeTodosData()
    {
        const string ExpectedXml = """
            <?xml version="1.0" encoding="utf-16"?>
            <value>
              <Items>
                <element>
                  <Id>1</Id>
                  <Title>Test task 1</Title>
                  <DueBy>2025-01-15</DueBy>
                  <Status>Done</Status>
                </element>
                <element>
                  <Id>2</Id>
                  <Title>Test task 2</Title>
                  <DueBy>2025-01-15</DueBy>
                  <Status>InProgress</Status>
                </element>
                <element>
                  <Id>3</Id>
                  <Title>Test task 3</Title>
                  <DueBy>2025-01-16</DueBy>
                  <Status>NotStarted</Status>
                </element>
              </Items>
            </value>
            """;

        var originalTodos = TestDataFactory.CreateSampleTodos();
        var xml = XmlSerializer.Serialize(originalTodos);
        var deserializedTodos = XmlSerializer.Deserialize<TestTodos>(xml);

        // Assert expected XML structure
        await Assert.That(xml).IsEqualTo(ExpectedXml);
        await Assert.That(StructuralEqualityComparer.Equals(originalTodos, deserializedTodos)).IsTrue();
    }
}