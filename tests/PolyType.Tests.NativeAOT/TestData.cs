using PolyType;
using PolyType.Examples.Validation;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Test data models for Native AOT smoke tests.
/// Uses the same structure as SerializationApp.AOT for consistency.
/// </summary>
[GenerateShape]
public partial record TestTodos(TestTodo[] Items);

public record TestTodo(int Id, string? Title, DateOnly? DueBy, TestStatus Status);

public enum TestStatus { NotStarted, InProgress, Done }

/// <summary>
/// Simple test data for basic smoke tests.
/// </summary>
[GenerateShape]
public partial record SimpleTestData(
    int IntValue,
    string StringValue,
    bool BoolValue,
    double DoubleValue,
    DateTime DateValue);

/// <summary>
/// Factory for creating test data instances.
/// </summary>
public static class TestDataFactory
{
    public static TestTodos CreateSampleTodos()
    {
        var today = DateOnly.FromDateTime(new DateTime(2025, 1, 15, 14, 30, 45));
        return new TestTodos([
            new(Id: 1, "Test task 1", today, TestStatus.Done),
            new(Id: 2, "Test task 2", today, TestStatus.InProgress),
            new(Id: 3, "Test task 3", today.AddDays(1), TestStatus.NotStarted)
        ]);
    }

    public static SimpleTestData CreateSimpleData()
    {
        return new SimpleTestData(
            IntValue: 42,
            StringValue: "Hello, Native AOT!",
            BoolValue: true,
            DoubleValue: 3.14159,
            DateValue: new DateTime(2024, 1, 15, 14, 30, 45)
        );
    }

    public static ValidationBindingModel CreateValidBindingModel()
    {
        return new ValidationBindingModel
        {
            Id = "12345",
            Components = ["Item1", "Item2", "Item3"],
            Sample = 0.5,
            PhoneNumber = "+1234567890"
        };
    }

    public static ValidationBindingModel CreateInvalidBindingModel()
    {
        return new ValidationBindingModel
        {
            Id = null, // Required violation
            Components = ["Item1"], // Length violation (min 2, max 5)
            Sample = 1.5, // Range violation (max 1.0)
            PhoneNumber = "invalid" // Regex violation
        };
    }

    public static NestedValidationModel CreateValidNestedModel()
    {
        return new NestedValidationModel
        {
            Name = "ValidModel",
            Binding = CreateValidBindingModel()
        };
    }

    public static NestedValidationModel CreateInvalidNestedModel()
    {
        return new NestedValidationModel
        {
            Name = "InvalidModel",
            Binding = CreateInvalidBindingModel()
        };
    }
}

/// <summary>
/// Validation test model matching the ValidationApp.AOT structure.
/// </summary>
[GenerateShape]
public partial class ValidationBindingModel
{
    [Required]
    public string? Id { get; set; }

    [Length(Min = 2, Max = 5)]
    public List<string>? Components { get; set; }

    [Range<double>(Min = 0, Max = 1)]
    public double Sample { get; set; }

    [RegularExpression(Pattern = @"^\+?[0-9]{7,14}$")]
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Nested validation test model for testing nested validation.
/// </summary>
[GenerateShape]
public partial class NestedValidationModel
{
    [Required]
    public string? Name { get; set; }

    [Required]
    public ValidationBindingModel? Binding { get; set; }
}