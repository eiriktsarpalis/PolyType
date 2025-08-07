using PolyType.Examples.JsonSerializer;
using PolyType.Examples.JsonSchema;
using PolyType.Examples.CborSerializer;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Smoke tests for JSON serialization in Native AOT.
/// </summary>
public static class JsonSerializationSmokeTests
{
    public static bool RunAllTests()
    {
        try
        {
            CanSerializeAndDeserializeSimpleData();
            CanSerializeAndDeserializeTodosData();
            CanGenerateJsonSchema();
            JsonSerializationHandlesNullValues();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JSON tests failed: {ex.Message}");
            return false;
        }
    }

    private static void CanSerializeAndDeserializeSimpleData()
    {
        // Arrange
        var originalData = TestDataFactory.CreateSimpleData();

        // Act
        var json = JsonSerializerTS.Serialize(originalData);
        var deserializedData = JsonSerializerTS.Deserialize<SimpleTestData>(json);

        // Assert
        if (deserializedData == null) throw new Exception("Deserialized data is null");
        if (deserializedData.IntValue != originalData.IntValue) throw new Exception("IntValue mismatch");
        if (deserializedData.StringValue != originalData.StringValue) throw new Exception("StringValue mismatch");
        if (deserializedData.BoolValue != originalData.BoolValue) throw new Exception("BoolValue mismatch");
        if (Math.Abs(deserializedData.DoubleValue - originalData.DoubleValue) > 0.0001) throw new Exception("DoubleValue mismatch");
        if (deserializedData.DateValue != originalData.DateValue) throw new Exception("DateValue mismatch");
    }

    private static void CanSerializeAndDeserializeTodosData()
    {
        // Arrange
        var originalTodos = TestDataFactory.CreateSampleTodos();

        // Act
        var json = JsonSerializerTS.Serialize(originalTodos);
        var deserializedTodos = JsonSerializerTS.Deserialize<TestTodos>(json);

        // Assert
        if (deserializedTodos == null) throw new Exception("Deserialized todos is null");
        if (deserializedTodos.Items.Length != originalTodos.Items.Length) throw new Exception("Items length mismatch");
        
        for (int i = 0; i < originalTodos.Items.Length; i++)
        {
            var original = originalTodos.Items[i];
            var deserialized = deserializedTodos.Items[i];
            
            if (deserialized.Id != original.Id) throw new Exception($"Todo {i} Id mismatch");
            if (deserialized.Title != original.Title) throw new Exception($"Todo {i} Title mismatch");
            if (deserialized.DueBy != original.DueBy) throw new Exception($"Todo {i} DueBy mismatch");
            if (deserialized.Status != original.Status) throw new Exception($"Todo {i} Status mismatch");
        }
    }

    private static void CanGenerateJsonSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<TestTodos>();

        // Assert
        if (schema == null) throw new Exception("Schema is null");
        var schemaJson = schema.ToJsonString();
        if (string.IsNullOrEmpty(schemaJson)) throw new Exception("Schema JSON is empty");
    }

    private static void JsonSerializationHandlesNullValues()
    {
        // Arrange
        var dataWithNulls = new TestTodos([
            new(Id: 1, Title: null, DueBy: null, Status: TestStatus.NotStarted)
        ]);

        // Act
        var json = JsonSerializerTS.Serialize(dataWithNulls);
        var deserialized = JsonSerializerTS.Deserialize<TestTodos>(json);

        // Assert
        if (deserialized == null) throw new Exception("Deserialized data is null");
        if (deserialized.Items.Length != 1) throw new Exception("Items length mismatch");
        if (deserialized.Items[0].Title != null) throw new Exception("Title should be null");
        if (deserialized.Items[0].DueBy != null) throw new Exception("DueBy should be null");
    }
}

/// <summary>
/// Smoke tests for CBOR serialization in Native AOT.
/// </summary>
public static class CborSerializationSmokeTests
{
    public static bool RunAllTests()
    {
        try
        {
            CanSerializeAndDeserializeSimpleData();
            CanSerializeAndDeserializeTodosData();
            CborSerializationHandlesNullValues();
            CborSerializationProducesValidHexString();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CBOR tests failed: {ex.Message}");
            return false;
        }
    }

    private static void CanSerializeAndDeserializeSimpleData()
    {
        // Arrange
        var originalData = TestDataFactory.CreateSimpleData();

        // Act
        var cborHex = CborSerializer.EncodeToHex(originalData);
        var deserializedData = CborSerializer.DecodeFromHex<SimpleTestData>(cborHex);

        // Assert
        if (deserializedData == null) throw new Exception("Deserialized data is null");
        if (deserializedData.IntValue != originalData.IntValue) throw new Exception("IntValue mismatch");
        if (deserializedData.StringValue != originalData.StringValue) throw new Exception("StringValue mismatch");
        if (deserializedData.BoolValue != originalData.BoolValue) throw new Exception("BoolValue mismatch");
        if (Math.Abs(deserializedData.DoubleValue - originalData.DoubleValue) > 0.0001) throw new Exception("DoubleValue mismatch");
        if (deserializedData.DateValue != originalData.DateValue) throw new Exception("DateValue mismatch");
    }

    private static void CanSerializeAndDeserializeTodosData()
    {
        // Arrange
        var originalTodos = TestDataFactory.CreateSampleTodos();

        // Act
        var cborHex = CborSerializer.EncodeToHex(originalTodos);
        var deserializedTodos = CborSerializer.DecodeFromHex<TestTodos>(cborHex);

        // Assert
        if (deserializedTodos == null) throw new Exception("Deserialized todos is null");
        if (deserializedTodos.Items.Length != originalTodos.Items.Length) throw new Exception("Items length mismatch");
        
        for (int i = 0; i < originalTodos.Items.Length; i++)
        {
            var original = originalTodos.Items[i];
            var deserialized = deserializedTodos.Items[i];
            
            if (deserialized.Id != original.Id) throw new Exception($"Todo {i} Id mismatch");
            if (deserialized.Title != original.Title) throw new Exception($"Todo {i} Title mismatch");
            if (deserialized.DueBy != original.DueBy) throw new Exception($"Todo {i} DueBy mismatch");
            if (deserialized.Status != original.Status) throw new Exception($"Todo {i} Status mismatch");
        }
    }

    private static void CborSerializationHandlesNullValues()
    {
        // Arrange
        var dataWithNulls = new TestTodos([
            new(Id: 1, Title: null, DueBy: null, Status: TestStatus.NotStarted)
        ]);

        // Act
        var cborHex = CborSerializer.EncodeToHex(dataWithNulls);
        var deserialized = CborSerializer.DecodeFromHex<TestTodos>(cborHex);

        // Assert
        if (deserialized == null) throw new Exception("Deserialized data is null");
        if (deserialized.Items.Length != 1) throw new Exception("Items length mismatch");
        if (deserialized.Items[0].Title != null) throw new Exception("Title should be null");
        if (deserialized.Items[0].DueBy != null) throw new Exception("DueBy should be null");
    }

    private static void CborSerializationProducesValidHexString()
    {
        // Arrange
        var data = TestDataFactory.CreateSimpleData();

        // Act
        var cborHex = CborSerializer.EncodeToHex(data);

        // Assert
        if (string.IsNullOrEmpty(cborHex)) throw new Exception("CBOR hex is null or empty");
        if (!cborHex.All(c => "0123456789ABCDEFabcdef".Contains(c))) throw new Exception("CBOR hex contains invalid characters");
    }
}