using TUnit.Core;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for validation in Native AOT.
/// </summary>
public class ValidationTests
{
    [Test]
    public void CanValidateValidData()
    {
        var validData = TestDataFactory.CreateSimpleData();
        // Use the Validate method which throws on validation failure
        PolyType.Examples.Validation.Validator.Validate(validData);
        // If we reach here, validation passed
    }

    [Test]
    public void CanValidateValidTodos()
    {
        var validTodos = TestDataFactory.CreateSampleTodos();
        // Use the Validate method which throws on validation failure  
        PolyType.Examples.Validation.Validator.Validate(validTodos);
        // If we reach here, validation passed
    }
}