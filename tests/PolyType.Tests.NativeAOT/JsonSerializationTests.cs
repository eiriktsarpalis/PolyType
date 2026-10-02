using PolyType.Abstractions;
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
    [Arguments(0, "42")]
    [Arguments(1, "\"text\"")]
    [Arguments(2, "null")]
    public async Task CanSerializeAndDeserializeUnion(int valueKind, string expectedJson)
    {
        ScalarUnion value = valueKind switch
        {
            0 => new(42),
            1 => new("text"),
            _ => default,
        };

        var shape = (IUnionTypeShape<ScalarUnion>)TypeShapeResolver.Resolve<ScalarUnion>();
        await Assert.That(shape.UnionKind).IsEqualTo(UnionTypeShapeKind.CSharpUnion);
        await Assert.That(shape.UnionCases.Count).IsEqualTo(2);
        await Assert.That(JsonSerializerTS.Serialize(value)).IsEqualTo(expectedJson);
        ScalarUnion roundtrip = JsonSerializerTS.Deserialize<ScalarUnion>(expectedJson);
        await Assert.That(StructuralEqualityComparer.Equals(value, roundtrip)).IsTrue();
    }

    [Test]
    public async Task CanSerializeAndDeserializeRecursiveUnion()
    {
        RecursiveUnion value = new(new RecursiveUnion[] { new(true), new(new RecursiveUnion[] { new(false) }) });
        string json = JsonSerializerTS.Serialize(value);
        RecursiveUnion roundtrip = JsonSerializerTS.Deserialize<RecursiveUnion>(json);

        await Assert.That(json).IsEqualTo("[true,[false]]");
        await Assert.That(StructuralEqualityComparer.Equals(value, roundtrip)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CanSerializeAndDeserializeClosedHierarchy(bool nested)
    {
        ClosedShape value = nested ? new ClosedAntelope { Number = 43 } : new ClosedZebra { Number = 42 };
        var shape = (IUnionTypeShape<ClosedShape>)TypeShapeResolver.Resolve<ClosedShape>();
        await Assert.That(shape.UnionKind).IsEqualTo(UnionTypeShapeKind.TypeHierarchy);
        await Assert.That(shape.UnionCases.Count).IsEqualTo(2);
        await Assert.That(shape.UnionCases[0].Name).IsEqualTo(nameof(ClosedZebra));
        await Assert.That(shape.UnionCases[1].Name).IsEqualTo(nameof(ClosedAntelope));
        await Assert.That(shape.UnionCases[0].IsTagSpecified).IsFalse();
        await Assert.That(shape.UnionCases[1].IsTagSpecified).IsFalse();

        string json = JsonSerializerTS.Serialize(value);
        ClosedShape roundtrip = JsonSerializerTS.Deserialize<ClosedShape>(json)
            ?? throw new InvalidOperationException("Closed hierarchy deserialization returned null.");
        await Assert.That(roundtrip.GetType()).IsEqualTo(value.GetType());
        await Assert.That(roundtrip.Number).IsEqualTo(value.Number);
    }
}
