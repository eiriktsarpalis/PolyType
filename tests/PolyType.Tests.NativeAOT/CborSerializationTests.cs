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
        const string ExpectedCbor = 
            "A568496E7456616C7565182A6B537472696E6756616C75657248656C6C6F2C204E617469766520414F542169426F6F6C56616C7565" +
            "F56B446F75626C6556616C7565FB400921F9F01B866E694461746556616C7565C073323032342D30312D31355431343A33303A3435";

        // Arrange
        var originalData = TestDataFactory.CreateSimpleData();

        // Act
        var cborHex = CborSerializer.EncodeToHex(originalData);
        var deserializedData = CborSerializer.DecodeFromHex<SimpleTestData>(cborHex);

        // Assert
        await Assert.That(cborHex).IsEqualTo(ExpectedCbor);
        await Assert.That(StructuralEqualityComparer.Equals(originalData, deserializedData)).IsTrue();
    }

    [Test]
    public async Task CanSerializeAndDeserializeTodosData()
    {
        const string ExpectedCbor = 
            "A1654974656D7383A462496401655469746C656B54657374207461736B2031654475654279C074323032352D30312D313554" +
            "30303A30303A30305A6653746174757364446F6E65A462496402655469746C656B54657374207461736B2032654475654279" +
            "C074323032352D30312D31355430303A30303A30305A665374617475736A496E50726F6772657373A462496403655469746C" + 
            "656B54657374207461736B2033654475654279C074323032352D30312D31365430303A30303A30305A665374617475736A4E" +
            "6F7453746172746564";

        // Arrange
        var originalTodos = TestDataFactory.CreateSampleTodos();

        // Act
        var cborHex = CborSerializer.EncodeToHex(originalTodos);
        var deserializedTodos = CborSerializer.DecodeFromHex<TestTodos>(cborHex);

        // Assert
        await Assert.That(cborHex).IsEqualTo(ExpectedCbor); // Should be a substantial hex string
        await Assert.That(StructuralEqualityComparer.Equals(originalTodos, deserializedTodos)).IsTrue();
    }
}