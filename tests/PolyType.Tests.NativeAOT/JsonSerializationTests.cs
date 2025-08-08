using PolyType.Examples.JsonSchema;
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.StructuralEquality;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for JSON serialization in Native AOT.
/// </summary>
public class JsonSerializationTests
{
    [Test]
    public async Task CanSerializeAndDeserializeSimpleData()
    {
        // Arrange
        var originalData = TestDataFactory.CreateSimpleData();

        // Act
        var json = JsonSerializerTS.Serialize(originalData);
        var deserializedData = JsonSerializerTS.Deserialize<SimpleTestData>(json);

        // Assert
        var expectedJson = """{"IntValue":42,"StringValue":"Hello, Native AOT!","BoolValue":true,"DoubleValue":3.14159,"DateValue":"2024-01-15T14:30:45"}""";
        await Assert.That(json).IsEqualTo(expectedJson);
        await Assert.That(StructuralEqualityComparer.Equals(originalData, deserializedData)).IsTrue();
    }

    [Test]
    public async Task CanSerializeAndDeserializeTodosData()
    {
        // Arrange
        var originalTodos = TestDataFactory.CreateSampleTodos();

        // Act
        var json = JsonSerializerTS.Serialize(originalTodos);
        var deserializedTodos = JsonSerializerTS.Deserialize<TestTodos>(json);

        // Assert
        var expectedJson = """{"Items":[{"Id":1,"Title":"Test task 1","DueBy":"2025-01-15","Status":"Done"},{"Id":2,"Title":"Test task 2","DueBy":"2025-01-15","Status":"InProgress"},{"Id":3,"Title":"Test task 3","DueBy":"2025-01-16","Status":"NotStarted"}]}""";
        await Assert.That(json).IsEqualTo(expectedJson);
        await Assert.That(StructuralEqualityComparer.Equals(originalTodos, deserializedTodos)).IsTrue();
    }

    [Test]
    public async Task CanGenerateJsonSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<TestTodos>();

        // Assert
        await Assert.That(schema).IsNotNull();
        await Assert.That(schema).Contains("\"type\":");
        await Assert.That(schema).Contains("\"properties\":");
    }
}
