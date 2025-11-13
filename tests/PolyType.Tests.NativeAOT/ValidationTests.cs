using PolyType.Examples.Validation;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for Validation in Native AOT.
/// </summary>
public class ValidationTests
{
    [Test]
    public async Task CanValidateValidInstance()
    {
        // Arrange
        var validData = TestDataFactory.CreateValidBindingModel();

        // Act
        bool isValid = validData.TryValidate(out var errors);

        // Assert
        await Assert.That(isValid).IsTrue();
        await Assert.That(errors).IsNull();
    }

    [Test]
    public async Task CanDetectValidationErrors()
    {
        // Arrange
        var invalidData = TestDataFactory.CreateInvalidBindingModel();

        // Act
        bool isValid = invalidData.TryValidate(out var errors);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!.Count).IsGreaterThanOrEqualTo(4); // Expecting at least 4 validation errors
    }

    [Test]
    public async Task ValidateThrowsOnInvalidInstance()
    {
        // Arrange
        var invalidData = TestDataFactory.CreateInvalidBindingModel();

        // Act & Assert
        var exception = await Assert.That(async () =>
        {
            Validator.Validate(invalidData);
            await Task.CompletedTask;
        }).ThrowsException();

        await Assert.That(exception).IsTypeOf<ValidationException>();
    }

    [Test]
    public async Task CanValidateNestedObjects()
    {
        // Arrange
        var validData = TestDataFactory.CreateValidNestedModel();

        // Act
        bool isValid = validData.TryValidate(out var errors);

        // Assert
        await Assert.That(isValid).IsTrue();
        await Assert.That(errors).IsNull();
    }

    [Test]
    public async Task CanDetectNestedValidationErrors()
    {
        // Arrange
        var invalidData = TestDataFactory.CreateInvalidNestedModel();

        // Act
        bool isValid = invalidData.TryValidate(out var errors);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task CanValidateRequiredProperty()
    {
        // Arrange
        var data = new ValidationBindingModel
        {
            Id = null, // Required violation
            Components = ["Item1", "Item2", "Item3"],
            Sample = 0.5,
            PhoneNumber = "+1234567890"
        };

        // Act
        bool isValid = data.TryValidate(out var errors);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!).Contains("$.Id: value is null or empty.");
    }

    [Test]
    public async Task CanValidateLengthConstraint()
    {
        // Arrange
        var data = new ValidationBindingModel
        {
            Id = "ValidId",
            Components = ["Item1"], // Length violation: min 2
            Sample = 0.5,
            PhoneNumber = "+1234567890"
        };

        // Act
        bool isValid = data.TryValidate(out var errors);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!).Contains("$.Components: contains less than 2 or more than 5 elements.");
    }

    [Test]
    public async Task CanValidateRangeConstraint()
    {
        // Arrange
        var data = new ValidationBindingModel
        {
            Id = "ValidId",
            Components = ["Item1", "Item2", "Item3"],
            Sample = 1.5, // Range violation: max 1.0
            PhoneNumber = "+1234567890"
        };

        // Act
        bool isValid = data.TryValidate(out var errors);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!).Contains("$.Sample: value is either less than 0 or greater than 1.");
    }

    [Test]
    public async Task CanValidateRegularExpressionConstraint()
    {
        // Arrange
        var data = new ValidationBindingModel
        {
            Id = "ValidId",
            Components = ["Item1", "Item2", "Item3"],
            Sample = 0.5,
            PhoneNumber = "invalid" // Regex violation
        };

        // Act
        bool isValid = data.TryValidate(out var errors);

        // Assert
        await Assert.That(isValid).IsFalse();
        await Assert.That(errors).IsNotNull();
        await Assert.That(errors!).Contains(@"$.PhoneNumber: value does not match regex pattern '^\+?[0-9]{7,14}$'.");
    }
}
