using PolyType;
using PolyType.Examples.AsyncValidation;
using PolyType.Examples.Validation;

// ---------------------------------------------------------------
// Async Validation Example — User Registration
//
// Demonstrates how PolyType's async validator composes synchronous
// validation attributes (Required, Length, Range, RegularExpression)
// with asynchronous validation attributes (UsernameAvailable,
// EmailNotRegistered) that simulate I/O-bound checks such as
// querying a database or calling an external service.
// ---------------------------------------------------------------

Console.WriteLine("=== Async Validation: User Registration ===");
Console.WriteLine();

// 1. A fully valid registration.
var validUser = new UserRegistration
{
    Username = "alice",
    Email = "alice@example.com",
    Password = "str0ngP@ss!",
    Age = 28,
};

List<string>? errors = await AsyncValidator.TryValidateAsync(validUser);
Console.WriteLine($"Valid user   → errors: {FormatErrors(errors)}");

// 2. A registration where the username is already taken (async check).
var takenUsername = new UserRegistration
{
    Username = "admin",
    Email = "new-admin@example.com",
    Password = "securePass1!",
    Age = 35,
};

errors = await AsyncValidator.TryValidateAsync(takenUsername);
Console.WriteLine($"Taken user   → errors: {FormatErrors(errors)}");

// 3. An already-registered email address (async check).
var takenEmail = new UserRegistration
{
    Username = "bob",
    Email = "admin@example.com",
    Password = "b0bP@ssword",
    Age = 30,
};

errors = await AsyncValidator.TryValidateAsync(takenEmail);
Console.WriteLine($"Taken email  → errors: {FormatErrors(errors)}");

// 4. Multiple violations — both sync and async.
var badRegistration = new UserRegistration
{
    Username = "root",       // async: taken
    Email = "not-an-email",  // sync: bad format; async: not registered, passes
    Password = "short",      // sync: too short
    Age = 10,                // sync: below minimum
};

errors = await AsyncValidator.TryValidateAsync(badRegistration);
Console.WriteLine($"Bad register → errors: {FormatErrors(errors)}");

// 5. Validate-and-throw path.
Console.WriteLine();
try
{
    await AsyncValidator.ValidateAsync(badRegistration);
}
catch (AsyncValidationException ex)
{
    Console.WriteLine("ValidateAsync threw as expected:");
    Console.WriteLine(ex.Message);
}

static string FormatErrors(List<string>? errors) =>
    errors is null ? "(none)" : $"[{string.Join("; ", errors)}]";

// ---------------------------------------------------------------
// Model and custom async validation attributes
// ---------------------------------------------------------------

[GenerateShape]
public partial class UserRegistration
{
    [Required]
    [Length(Min = 3, Max = 20)]
    [UsernameAvailable]
    public string? Username { get; set; }

    [Required]
    [RegularExpression(Pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    [EmailNotRegistered]
    public string? Email { get; set; }

    [Required]
    [Length(Min = 8, Max = 100)]
    public string? Password { get; set; }

    [Range<int>(Min = 13, Max = 120)]
    public int Age { get; set; }
}

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
            new Func<string, ValueTask<bool>>(async username =>
            {
                // Simulate an async lookup (e.g. database round-trip).
                await Task.Delay(10).ConfigureAwait(false);
                return username is not null && !TakenUsernames.Contains(username);
            });
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
            new Func<string, ValueTask<bool>>(async email =>
            {
                // Simulate an async lookup (e.g. database round-trip).
                await Task.Delay(10).ConfigureAwait(false);
                return email is not null && !RegisteredEmails.Contains(email);
            });
    }
}
