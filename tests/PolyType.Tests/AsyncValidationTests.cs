#if NET
using PolyType.Examples.AsyncValidation;
using PolyType.Examples.Validation;

namespace PolyType.Tests;

public abstract partial class AsyncValidationTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(GetAsyncValidatorScenaria))]
    public async Task SimpleAsyncValidationScenaria<T>(TestCase<T> testCase, List<string>? expectedErrors)
    {
        AsyncValidator<T> validator = GetAsyncValidatorUnderTest(testCase);

        bool expectedResult = expectedErrors is null;
        List<string>? errors = await validator.TryValidateAsync(testCase.Value);

        Assert.Equal(expectedResult, errors is null);
        Assert.Equal(expectedErrors, errors);
    }

    [Theory]
    [MemberData(nameof(GetAsyncOnlyScenaria))]
    public async Task AsyncOnlyValidationScenaria<T>(TestCase<T> testCase, List<string>? expectedErrors)
    {
        AsyncValidator<T> validator = GetAsyncValidatorUnderTest(testCase);

        List<string>? errors = await validator.TryValidateAsync(testCase.Value);

        Assert.Equal(expectedErrors is null, errors is null);
        Assert.Equal(expectedErrors, errors);
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public async Task TypeWithoutAttributeAnnotations_PassesAsyncValidation<T>(TestCase<T> testCase)
    {
        AsyncValidator<T> validator = GetAsyncValidatorUnderTest(testCase);
        List<string>? errors = await validator.TryValidateAsync(testCase.Value);

        Assert.Null(errors);
    }

    [Fact]
    public async Task ValidateAsync_ThrowsOnInvalidValue()
    {
        var invalidModel = new AsyncBindingModel
        {
            Id = null,
            Components = ["1"],
            Sample = 1.1,
            PhoneNumber = "NaN",
        };

        var testCase = TestCase.Create(invalidModel);
        AsyncValidator<AsyncBindingModel> validator = GetAsyncValidatorUnderTest(testCase);

        await Assert.ThrowsAsync<AsyncValidationException>(() => validator.ValidateAsync(invalidModel).AsTask());
    }

    public static IEnumerable<object?[]> GetAsyncValidatorScenaria()
    {
        ModelProvider provider = new();
        var validModel = new AsyncBindingModel
        {
            Id = "id",
            Components = ["1", "2", "3"],
            Sample = 0.517,
            PhoneNumber = "+447777777777",
        };

        yield return Create(TestCase.Create(validModel));
        yield return Create(TestCase.Create(validModel with { Id = null }), ["$.Id: value is null or empty."]);
        yield return Create(TestCase.Create(validModel with { Components = ["1"] }), ["$.Components: contains less than 2 or more than 5 elements."]);
        yield return Create(TestCase.Create(validModel with { Components = ["1", "2", "3", "4", "5", "6"] }), ["$.Components: contains less than 2 or more than 5 elements."]);
        yield return Create(TestCase.Create(validModel with { Sample = -1 }), ["$.Sample: value is either less than 0 or greater than 1."]);
        yield return Create(TestCase.Create(validModel with { Sample = 5 }), ["$.Sample: value is either less than 0 or greater than 1."]);
        yield return Create(TestCase.Create(validModel with { PhoneNumber = "NaN" }), [@"$.PhoneNumber: value does not match regex pattern '^\+?[0-9]{7,14}$'."]);

        yield return Create(TestCase.Create(new AsyncBindingModel
        {
            Id = null,
            Components = ["1"],
            Sample = 1.1,
            PhoneNumber = "NaN",
        }),
        expectedErrors:
        [
            "$.Id: value is null or empty.",
            "$.Components: contains less than 2 or more than 5 elements.",
            "$.Sample: value is either less than 0 or greater than 1.",
            @"$.PhoneNumber: value does not match regex pattern '^\+?[0-9]{7,14}$'.",
        ]);

        yield return Create(
            TestCase.Create(new GenericRecord<AsyncBindingModel>(validModel with { Id = null }), provider),
            expectedErrors: ["$.value.Id: value is null or empty."]);

        yield return Create(
            TestCase.Create(new List<AsyncBindingModel> { validModel with { Id = null } }, provider),
            expectedErrors: ["$.[0].Id: value is null or empty."]);

        yield return Create(
            TestCase.Create(new Dictionary<string, AsyncBindingModel> { ["key"] = validModel with { Id = null } }, provider),
            expectedErrors: ["$.key.Id: value is null or empty."]);

        static object?[] Create<T>(TestCase<T> value, List<string>? expectedErrors = null) => [value, expectedErrors];
    }

    public static IEnumerable<object?[]> GetAsyncOnlyScenaria()
    {
        // Model with async-only attributes.
        var validModel = new AsyncOnlyModel
        {
            Username = "alice",
            Email = "alice@example.com",
        };

        yield return Create(TestCase.Create(validModel));

        // Taken username (async check).
        yield return Create(
            TestCase.Create(validModel with { Username = "admin" }),
            expectedErrors: ["$.Username: username is already taken."]);

        // Registered email (async check).
        yield return Create(
            TestCase.Create(validModel with { Email = "admin@example.com" }),
            expectedErrors: ["$.Email: email address is already registered."]);

        // Mixed sync + async: required violation + async violation.
        yield return Create(
            TestCase.Create(new AsyncOnlyModel { Username = null, Email = "root@example.com" }),
            expectedErrors:
            [
                "$.Username: value is null or empty.",
                "$.Username: username is already taken.",
                "$.Email: email address is already registered.",
            ]);

        static object?[] Create<T>(TestCase<T> value, List<string>? expectedErrors = null) => [value, expectedErrors];
    }

    private AsyncValidator<T> GetAsyncValidatorUnderTest<T>(TestCase<T> testCase) =>
        AsyncValidator.Create(providerUnderTest.ResolveShape(testCase));

    // Reuses same sync attributes as the sync ValidationTests.
    [GenerateShape]
    public partial record AsyncBindingModel
    {
        [Required]
        public string? Id { get; set; }

        [Length(Min = 2, Max = 5)]
        public List<string>? Components { get; set; }

        [RangeDouble(Min = 0, Max = 1)]
        public double Sample { get; set; }

        [RegularExpression(Pattern = @"^\+?[0-9]{7,14}$")]
        public string? PhoneNumber { get; set; }
    }

    // Model with both sync and async validation attributes.
    [GenerateShape]
    public partial record AsyncOnlyModel
    {
        [Required]
        [UsernameAvailable]
        public string? Username { get; set; }

        [Required]
        [EmailNotRegistered]
        public string? Email { get; set; }
    }

    // Workaround for .NET Framework not supporting generic attributes.
    public class RangeDoubleAttribute : RangeAttribute<double>;

    [GenerateShapeFor<GenericRecord<AsyncBindingModel>>]
    [GenerateShapeFor<List<AsyncBindingModel>>]
    [GenerateShapeFor<Dictionary<string, AsyncBindingModel>>]
    public partial class ModelProvider;

    /// <summary>
    /// Async validation attribute that checks whether a username is available.
    /// Simulates a database or service call.
    /// </summary>
    public sealed class UsernameAvailableAttribute : AsyncValidationAttribute
    {
        private static readonly HashSet<string> TakenUsernames =
            new(StringComparer.OrdinalIgnoreCase) { "admin", "root", "system", "test" };

        public override string ErrorMessage => "username is already taken.";

        public override Func<TMemberType, ValueTask<bool>>? CreateAsyncValidationPredicate<TMemberType>()
        {
            if (typeof(TMemberType) != typeof(string))
            {
                return null;
            }

            return (Func<TMemberType, ValueTask<bool>>)(object)
                new Func<string, ValueTask<bool>>(username =>
                    new ValueTask<bool>(username is not null && !TakenUsernames.Contains(username)));
        }
    }

    /// <summary>
    /// Async validation attribute that checks whether an email is not already registered.
    /// Simulates a database or service call.
    /// </summary>
    public sealed class EmailNotRegisteredAttribute : AsyncValidationAttribute
    {
        private static readonly HashSet<string> RegisteredEmails =
            new(StringComparer.OrdinalIgnoreCase) { "admin@example.com", "root@example.com" };

        public override string ErrorMessage => "email address is already registered.";

        public override Func<TMemberType, ValueTask<bool>>? CreateAsyncValidationPredicate<TMemberType>()
        {
            if (typeof(TMemberType) != typeof(string))
            {
                return null;
            }

            return (Func<TMemberType, ValueTask<bool>>)(object)
                new Func<string, ValueTask<bool>>(email =>
                    new ValueTask<bool>(email is not null && !RegisteredEmails.Contains(email)));
        }
    }
}

public sealed class AsyncValidationTests_Reflection() : AsyncValidationTests(ReflectionProviderUnderTest.NoEmit);
public sealed class AsyncValidationTests_ReflectionEmit() : AsyncValidationTests(ReflectionProviderUnderTest.Emit);
public sealed class AsyncValidationTests_SourceGen() : AsyncValidationTests(SourceGenProviderUnderTest.Default);
#endif
