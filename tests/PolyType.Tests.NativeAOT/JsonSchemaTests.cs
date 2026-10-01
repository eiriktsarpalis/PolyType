using PolyType.Examples.JsonSchema;

namespace PolyType.Tests.NativeAOT;

public class JsonSchemaTests
{
    [Test]
    public async Task CanGenerateJsonSchema()
    {
        var schema = JsonSchemaGenerator.Generate<TestTodos>();
        await Assert.That(schema).IsNotNull();
    }
}
