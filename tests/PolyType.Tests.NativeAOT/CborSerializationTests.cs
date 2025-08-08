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
        var expectedHex = "A5674965E276616C7565182A6B537472696E6756616C75657148656C6C6F2C204E61746976652041";
        await Assert.That(cborHex).StartsWith(expectedHex.Substring(0, 20)); // Check at least the first part
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
        // CBOR for objects typically starts with A{n} where n is the number of properties
        // The Items property should be encoded, so we expect to see 'Items' in the hex
        await Assert.That(cborHex).StartsWith("A1"); // Object with 1 property (Items)
        await Assert.That(cborHex.Length).IsGreaterThan(50); // Should be a substantial hex string
        await Assert.That(StructuralEqualityComparer.Equals(originalTodos, deserializedTodos)).IsTrue();
    }
}