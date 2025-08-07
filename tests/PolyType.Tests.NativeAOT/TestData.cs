using PolyType;

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
        var today = DateOnly.FromDateTime(DateTime.Now);
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
}