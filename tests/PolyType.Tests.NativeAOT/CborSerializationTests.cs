using PolyType.Examples.CborSerializer;
using PolyType.Examples.StructuralEquality;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for CBOR serialization in Native AOT.
/// </summary>
public class CborSerializationTests
{
    [Test]
    public async Task CanSerializeAndDeserializeSimpleData()
    {
        // Arrange
        var originalData = TestDataFactory.CreateSimpleData();

        // Act
        var cborHex = CborSerializer.EncodeToHex(originalData);
        var deserializedData = CborSerializer.DecodeFromHex<SimpleTestData>(cborHex);

        // Assert
        await Assert.That(StructuralEqualityComparer.Equals(originalData, deserializedData)).IsTrue();
    }

    [Test]
    public async Task CanSerializeAndDeserializeTodosData()
    {
        // Arrange
        var originalTodos = TestDataFactory.CreateSampleTodos();

        // Act
        var cborHex = CborSerializer.EncodeToHex(originalTodos);
        var deserializedTodos = CborSerializer.DecodeFromHex<TestTodos>(cborHex);

        // Assert
        await Assert.That(StructuralEqualityComparer.Equals(originalTodos, deserializedTodos)).IsTrue();
    }
}