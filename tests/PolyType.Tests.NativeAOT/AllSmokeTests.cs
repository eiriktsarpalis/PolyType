using PolyType.Examples.XmlSerializer;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Smoke tests for XML serialization in Native AOT.
/// </summary>
public static class XmlSerializationSmokeTests
{
    public static bool RunAllTests()
    {
        try
        {
            CanSerializeAndDeserializeSimpleData();
            CanSerializeAndDeserializeTodosData();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"XML tests failed: {ex.Message}");
            return false;
        }
    }

    private static void CanSerializeAndDeserializeSimpleData()
    {
        var originalData = TestDataFactory.CreateSimpleData();
        var xml = XmlSerializer.Serialize(originalData);
        var deserializedData = XmlSerializer.Deserialize<SimpleTestData>(xml);

        if (deserializedData == null) throw new Exception("Deserialized data is null");
        if (deserializedData.IntValue != originalData.IntValue) throw new Exception("IntValue mismatch");
    }

    private static void CanSerializeAndDeserializeTodosData()
    {
        var originalTodos = TestDataFactory.CreateSampleTodos();
        var xml = XmlSerializer.Serialize(originalTodos);
        var deserializedTodos = XmlSerializer.Deserialize<TestTodos>(xml);

        if (deserializedTodos == null) throw new Exception("Deserialized todos is null");
        if (deserializedTodos.Items.Length != originalTodos.Items.Length) throw new Exception("Items length mismatch");
    }
}

public static class StructuralEqualitySmokeTests
{
    public static bool RunAllTests()
    {
        try
        {
            CanCompareEqualSimpleData();
            CanCompareDifferentSimpleData();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Structural equality tests failed: {ex.Message}");
            return false;
        }
    }

    private static void CanCompareEqualSimpleData()
    {
        var data1 = TestDataFactory.CreateSimpleData();
        var data2 = TestDataFactory.CreateSimpleData();
        var areEqual = PolyType.Examples.StructuralEquality.StructuralEqualityComparer.Equals(data1, data2);
        if (!areEqual) throw new Exception("Equal data should be equal");
    }

    private static void CanCompareDifferentSimpleData()
    {
        var data1 = TestDataFactory.CreateSimpleData();
        var data2 = new SimpleTestData(999, "Different", false, 2.71828, DateTime.Now);
        var areEqual = PolyType.Examples.StructuralEquality.StructuralEqualityComparer.Equals(data1, data2);
        if (areEqual) throw new Exception("Different data should not be equal");
    }
}

public static class RandomGenerationSmokeTests
{
    public static bool RunAllTests()
    {
        try
        {
            CanGenerateSimpleDataInstances();
            CanGenerateTodosDataInstances();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Random generation tests failed: {ex.Message}");
            return false;
        }
    }

    private static void CanGenerateSimpleDataInstances()
    {
        // Generate multiple values to test the generator
        var instances = PolyType.Examples.RandomGenerator.RandomGenerator.GenerateValues<SimpleTestData>()
            .Take(2)
            .ToList();

        if (instances.Count < 2) throw new Exception("Should generate at least 2 instances");
        if (instances[0] == null) throw new Exception("Generated instance 1 is null");
        if (instances[1] == null) throw new Exception("Generated instance 2 is null");
        if (instances[0].StringValue == null) throw new Exception("Generated string is null");
    }

    private static void CanGenerateTodosDataInstances()
    {
        // Generate multiple values to test the generator
        var todosList = PolyType.Examples.RandomGenerator.RandomGenerator.GenerateValues<TestTodos>()
            .Take(2)
            .ToList();

        if (todosList.Count < 2) throw new Exception("Should generate at least 2 instances");
        if (todosList[0] == null) throw new Exception("Generated todos 1 is null");
        if (todosList[1] == null) throw new Exception("Generated todos 2 is null");
        if (todosList[0].Items == null) throw new Exception("Generated items 1 is null");
        if (todosList[1].Items == null) throw new Exception("Generated items 2 is null");
    }
}

public static class ValidationSmokeTests
{
    public static bool RunAllTests()
    {
        try
        {
            CanValidateValidData();
            CanValidateValidTodos();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Validation tests failed: {ex.Message}");
            return false;
        }
    }

    private static void CanValidateValidData()
    {
        var validData = TestDataFactory.CreateSimpleData();
        // Use the Validate method which throws on validation failure
        PolyType.Examples.Validation.Validator.Validate(validData);
        // If we reach here, validation passed
    }

    private static void CanValidateValidTodos()
    {
        var validTodos = TestDataFactory.CreateSampleTodos();
        // Use the Validate method which throws on validation failure  
        PolyType.Examples.Validation.Validator.Validate(validTodos);
        // If we reach here, validation passed
    }
}

public static class PrettyPrinterSmokeTests
{
    public static bool RunAllTests()
    {
        try
        {
            CanPrettyPrintSimpleData();
            CanPrettyPrintTodosData();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Pretty printer tests failed: {ex.Message}");
            return false;
        }
    }

    private static void CanPrettyPrintSimpleData()
    {
        var data = TestDataFactory.CreateSimpleData();
        var prettyString = PolyType.Examples.PrettyPrinter.PrettyPrinter.Print(data);

        if (string.IsNullOrEmpty(prettyString)) throw new Exception("Pretty string is null or empty");
        if (!prettyString.Contains("SimpleTestData")) throw new Exception("Pretty string doesn't contain type name");
    }

    private static void CanPrettyPrintTodosData()
    {
        var todos = TestDataFactory.CreateSampleTodos();
        var prettyString = PolyType.Examples.PrettyPrinter.PrettyPrinter.Print(todos);

        if (string.IsNullOrEmpty(prettyString)) throw new Exception("Pretty string is null or empty");
        if (!prettyString.Contains("TestTodos")) throw new Exception("Pretty string doesn't contain type name");
    }
}