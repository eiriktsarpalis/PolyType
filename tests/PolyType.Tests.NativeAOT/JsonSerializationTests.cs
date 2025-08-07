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
        await Assert.That(StructuralEqualityComparer.Equals(originalTodos, deserializedTodos)).IsTrue();
    }

    [Test]
    public async Task CanGenerateJsonSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<TestTodos>();

        // Assert
        await Assert.That(schema).IsNotNull();
    }
}
