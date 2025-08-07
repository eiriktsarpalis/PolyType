using PolyType.Examples.JsonSerializer;
using PolyType.Examples.JsonSchema;
using PolyType.Examples.CborSerializer;
using TUnit.Core;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for JSON serialization in Native AOT.
/// </summary>
public class JsonSerializationTests
{
    [Test]
    public void CanSerializeAndDeserializeSimpleData()
    {
        // Arrange
        var originalData = TestDataFactory.CreateSimpleData();

        // Act
        var json = JsonSerializerTS.Serialize(originalData);
        var deserializedData = JsonSerializerTS.Deserialize<SimpleTestData>(json);

        // Assert
        Assert.That(deserializedData).IsNotNull();
        Assert.That(deserializedData.IntValue).IsEqualTo(originalData.IntValue);
        Assert.That(deserializedData.StringValue).IsEqualTo(originalData.StringValue);
        Assert.That(deserializedData.BoolValue).IsEqualTo(originalData.BoolValue);
        Assert.That(Math.Abs(deserializedData.DoubleValue - originalData.DoubleValue)).IsLessThanOrEqualTo(0.0001);
        Assert.That(deserializedData.DateValue).IsEqualTo(originalData.DateValue);
    }

    [Test]
    public void CanSerializeAndDeserializeTodosData()
    {
        // Arrange
        var originalTodos = TestDataFactory.CreateSampleTodos();

        // Act
        var json = JsonSerializerTS.Serialize(originalTodos);
        var deserializedTodos = JsonSerializerTS.Deserialize<TestTodos>(json);

        // Assert
        Assert.That(deserializedTodos).IsNotNull();
        Assert.That(deserializedTodos.Items.Length).IsEqualTo(originalTodos.Items.Length);
        
        for (int i = 0; i < originalTodos.Items.Length; i++)
        {
            var original = originalTodos.Items[i];
            var deserialized = deserializedTodos.Items[i];
            
            Assert.That(deserialized.Id).IsEqualTo(original.Id);
            Assert.That(deserialized.Title).IsEqualTo(original.Title);
            Assert.That(deserialized.DueBy).IsEqualTo(original.DueBy);
            Assert.That(deserialized.Status).IsEqualTo(original.Status);
        }
    }

    [Test]
    public void CanGenerateJsonSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<TestTodos>();

        // Assert
        Assert.That(schema).IsNotNull();
        var schemaJson = schema.ToJsonString();
        Assert.That(schemaJson).IsNotNullOrWhiteSpace();
    }

    [Test]
    public void JsonSerializationHandlesNullValues()
    {
        // Arrange
        var dataWithNulls = new TestTodos([
            new(Id: 1, Title: null, DueBy: null, Status: TestStatus.NotStarted)
        ]);

        // Act
        var json = JsonSerializerTS.Serialize(dataWithNulls);
        var deserialized = JsonSerializerTS.Deserialize<TestTodos>(json);

        // Assert
        Assert.That(deserialized).IsNotNull();
        Assert.That(deserialized.Items.Length).IsEqualTo(1);
        Assert.That(deserialized.Items[0].Title).IsNull();
        Assert.That(deserialized.Items[0].DueBy).IsNull();
    }
}

/// <summary>
/// Tests for CBOR serialization in Native AOT.
/// </summary>
public class CborSerializationTests
{
    [Test]
    public void CanSerializeAndDeserializeSimpleData()
    {
        // Arrange
        var originalData = TestDataFactory.CreateSimpleData();

        // Act
        var cborHex = CborSerializer.EncodeToHex(originalData);
        var deserializedData = CborSerializer.DecodeFromHex<SimpleTestData>(cborHex);

        // Assert
        Assert.That(deserializedData).IsNotNull();
        Assert.That(deserializedData.IntValue).IsEqualTo(originalData.IntValue);
        Assert.That(deserializedData.StringValue).IsEqualTo(originalData.StringValue);
        Assert.That(deserializedData.BoolValue).IsEqualTo(originalData.BoolValue);
        Assert.That(Math.Abs(deserializedData.DoubleValue - originalData.DoubleValue)).IsLessThanOrEqualTo(0.0001);
        Assert.That(deserializedData.DateValue).IsEqualTo(originalData.DateValue);
    }

    [Test]
    public void CanSerializeAndDeserializeTodosData()
    {
        // Arrange
        var originalTodos = TestDataFactory.CreateSampleTodos();

        // Act
        var cborHex = CborSerializer.EncodeToHex(originalTodos);
        var deserializedTodos = CborSerializer.DecodeFromHex<TestTodos>(cborHex);

        // Assert
        Assert.That(deserializedTodos).IsNotNull();
        Assert.That(deserializedTodos.Items.Length).IsEqualTo(originalTodos.Items.Length);
        
        for (int i = 0; i < originalTodos.Items.Length; i++)
        {
            var original = originalTodos.Items[i];
            var deserialized = deserializedTodos.Items[i];
            
            Assert.That(deserialized.Id).IsEqualTo(original.Id);
            Assert.That(deserialized.Title).IsEqualTo(original.Title);
            Assert.That(deserialized.DueBy).IsEqualTo(original.DueBy);
            Assert.That(deserialized.Status).IsEqualTo(original.Status);
        }
    }

    [Test]
    public void CborSerializationHandlesNullValues()
    {
        // Arrange
        var dataWithNulls = new TestTodos([
            new(Id: 1, Title: null, DueBy: null, Status: TestStatus.NotStarted)
        ]);

        // Act
        var cborHex = CborSerializer.EncodeToHex(dataWithNulls);
        var deserialized = CborSerializer.DecodeFromHex<TestTodos>(cborHex);

        // Assert
        Assert.That(deserialized).IsNotNull();
        Assert.That(deserialized.Items.Length).IsEqualTo(1);
        Assert.That(deserialized.Items[0].Title).IsNull();
        Assert.That(deserialized.Items[0].DueBy).IsNull();
    }

    [Test]
    public void CborSerializationProducesValidHexString()
    {
        // Arrange
        var data = TestDataFactory.CreateSimpleData();

        // Act
        var cborHex = CborSerializer.EncodeToHex(data);

        // Assert
        Assert.That(cborHex).IsNotNullOrWhiteSpace();
        Assert.That(cborHex.All(c => "0123456789ABCDEFabcdef".Contains(c))).IsTrue();
    }
}