#if NET
using System.Threading.Tasks;

namespace PolyType.Examples.AsyncValidation;

/// <summary>
/// Defines an abstract async validation attribute whose predicate may perform I/O.
/// </summary>
/// <remarks>
/// Async validation attributes complement the synchronous
/// <see cref="Validation.ValidationAttribute"/> type. Both kinds of attribute
/// are recognised by <see cref="AsyncValidator"/> when building an async
/// validator for a type.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
public abstract class AsyncValidationAttribute : Attribute
{
    /// <summary>The error message to surface on validation error.</summary>
    public abstract string ErrorMessage { get; }

    /// <summary>
    /// Creates an async validation predicate for a given member type.
    /// Returns <see langword="null"/> when the attribute does not apply to <typeparamref name="TMemberType"/>.
    /// </summary>
    public abstract Func<TMemberType, ValueTask<bool>>? CreateAsyncValidationPredicate<TMemberType>();
}
#endif
