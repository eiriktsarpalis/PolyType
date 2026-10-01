using PolyType.Abstractions;
using PolyType.Examples.StructuralEquality;
using PolyType.Examples.YamlSerializer;

namespace PolyType.Tests.NativeAOT;

public class YamlSerializationTests
{
    [Test]
    public async Task CanSerializeAndDeserializeSimpleData()
    {
        var originalData = TestDataFactory.CreateSimpleData();
        string yaml = YamlSerializer.Serialize(originalData);
        SimpleTestData? deserializedData = YamlSerializer.Deserialize<SimpleTestData>(yaml);
        string expectedYaml = """
            IntValue: 42
            StringValue: Hello, Native AOT!
            BoolValue: true
            DoubleValue: 3.14159
            DateValue: 2024-01-15T14:30:45.0000000
            """.ReplaceLineEndings("\n");

        await Assert.That(yaml).IsEqualTo(expectedYaml);
        await Assert.That(StructuralEqualityComparer.Equals(originalData, deserializedData)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CanSerializeAndDeserializeTodosData(bool nullValues)
    {
        var originalTodos = TestDataFactory.CreateSampleTodos();
        if (nullValues)
        {
            originalTodos.Items[0] = originalTodos.Items[0] with { Title = null, DueBy = null };
        }

        string yaml = YamlSerializer.Serialize(originalTodos);
        TestTodos? deserializedTodos = YamlSerializer.Deserialize<TestTodos>(yaml);
        string expectedYaml = $$"""
            Items:
            - Id: 1
              Title: {{(nullValues ? "null" : "Test task 1")}}
              DueBy: {{(nullValues ? "null" : "2025-01-15")}}
              Status: Done
            - Id: 2
              Title: Test task 2
              DueBy: 2025-01-15
              Status: InProgress
            - Id: 3
              Title: Test task 3
              DueBy: 2025-01-16
              Status: NotStarted
            """.ReplaceLineEndings("\n");

        await Assert.That(yaml).IsEqualTo(expectedYaml);
        await Assert.That(StructuralEqualityComparer.Equals(originalTodos, deserializedTodos)).IsTrue();
    }

    [Test]
    public async Task CanSerializeAndDeserializeUnion()
    {
        ScalarUnion value = new("text");
        var converter = YamlSerializer.CreateConverter(TypeShapeResolver.Resolve<ScalarUnion>());
        const string Yaml = "_type: String\n_value: text";

        await Assert.That(converter.Serialize(value)).IsEqualTo(Yaml);
        await Assert.That(StructuralEqualityComparer.Equals(value, converter.Deserialize(Yaml))).IsTrue();
    }
}
