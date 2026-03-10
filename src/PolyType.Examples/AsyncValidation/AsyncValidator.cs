#if NET
using System.Threading.Tasks;
using PolyType.Abstractions;
using PolyType.Utilities;

namespace PolyType.Examples.AsyncValidation;

/// <summary>
/// Delegate containing a recursive async validator that walks the object graph
/// for both synchronous <see cref="Validation.ValidationAttribute"/> and
/// asynchronous <see cref="AsyncValidationAttribute"/> annotations.
/// </summary>
/// <param name="value">The value to validate.</param>
/// <param name="path">Mutable path used for error location tracking.</param>
/// <param name="errors">Mutable list that collects error messages.</param>
public delegate ValueTask AsyncValidator<in T>(T? value, List<string> path, List<string> errors);

/// <summary>
/// Exception thrown when async validation fails.
/// </summary>
/// <param name="message">The validation error message.</param>
public sealed class AsyncValidationException(string message) : Exception(message);

/// <summary>Provides an async object validator for .NET types built on top of PolyType.</summary>
public static partial class AsyncValidator
{
    private static readonly MultiProviderTypeCache s_cache = new()
    {
        DelayedValueFactory = new DelayedAsyncValidatorFactory(),
        ValueBuilderFactory = ctx => new Builder(ctx),
    };

    /// <summary>
    /// Builds an async validator delegate using a type shape as input.
    /// </summary>
    public static AsyncValidator<T> Create<T>(ITypeShape<T> type) =>
        (AsyncValidator<T>?)s_cache.GetOrAdd(type) ?? Builder.CreateNullValidator<T>();

    /// <summary>
    /// Builds an async validator delegate using a shape provider as input.
    /// </summary>
    public static AsyncValidator<T> Create<T>(ITypeShapeProvider typeShapeProvider) =>
        (AsyncValidator<T>?)s_cache.GetOrAdd(typeof(T), typeShapeProvider) ?? Builder.CreateNullValidator<T>();

    /// <summary>
    /// Runs async validation against the provided value.
    /// </summary>
    /// <returns>
    /// A list of validation errors, or <see langword="null"/> if the value is valid.
    /// </returns>
    public static async ValueTask<List<string>?> TryValidateAsync<T>(this AsyncValidator<T> validator, T? value)
    {
        var errors = new List<string>();
        var path = new List<string>();
        await validator(value, path, errors).ConfigureAwait(false);

        return errors.Count > 0 ? errors : null;
    }

    /// <summary>
    /// Runs async validation against the provided value.
    /// Throws <see cref="AsyncValidationException"/> on failure.
    /// </summary>
    public static async ValueTask ValidateAsync<T>(this AsyncValidator<T> validator, T? value)
    {
        List<string>? errors = await validator.TryValidateAsync(value).ConfigureAwait(false);
        if (errors is not null)
        {
            throw new AsyncValidationException($"Found validation errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }
    }

    /// <summary>
    /// Runs async validation against the provided value.
    /// </summary>
    public static ValueTask<List<string>?> TryValidateAsync<T>(this T? value) where T : IShapeable<T>
        => AsyncValidatorCache<T, T>.Value.TryValidateAsync(value);

    /// <summary>
    /// Runs async validation against the provided value.
    /// </summary>
    public static ValueTask ValidateAsync<T>(T? value) where T : IShapeable<T>
        => AsyncValidatorCache<T, T>.Value.ValidateAsync(value);

    /// <summary>
    /// Runs async validation against the provided value.
    /// </summary>
    public static ValueTask<List<string>?> TryValidateAsync<T, TProvider>(T? value) where TProvider : IShapeable<T>
        => AsyncValidatorCache<T, TProvider>.Value.TryValidateAsync(value);

    /// <summary>
    /// Runs async validation against the provided value.
    /// </summary>
    public static ValueTask ValidateAsync<T, TProvider>(T? value) where TProvider : IShapeable<T>
        => AsyncValidatorCache<T, TProvider>.Value.ValidateAsync(value);

    private static class AsyncValidatorCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static AsyncValidator<T> Value => s_value ??= Create(TProvider.GetTypeShape());
        private static AsyncValidator<T>? s_value;
    }
}
#endif
