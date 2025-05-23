using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET constructor.
/// </summary>
[InternalImplementationsOnly]
public interface IConstructorShape
{
    /// <summary>
    /// Gets the shape of the declaring type for the constructor.
    /// </summary>
    IObjectTypeShape DeclaringType { get; }

    /// <summary>
    /// Gets a value indicating whether the constructor is declared public.
    /// </summary>
    bool IsPublic { get; }

    /// <summary>
    /// Gets the provider used for method-level attribute resolution.
    /// </summary>
    ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Gets the shapes of the parameters accepted by the constructor.
    /// </summary>
    /// <remarks>
    /// Includes all formal parameters of the underlying constructor,
    /// as well as any member that can be specified in a member initializer expression.
    /// </remarks>
    IReadOnlyList<IParameterShape> Parameters { get; }

    /// <summary>
    /// Accepts an <see cref="TypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(TypeShapeVisitor visitor, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET constructor.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type of the underlying constructor.</typeparam>
/// <typeparam name="TArgumentState">The state type used for aggregating constructor arguments.</typeparam>
[InternalImplementationsOnly]
public interface IConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape
{
    /// <summary>
    /// Gets the shape of the declaring type for the constructor.
    /// </summary>
    new IObjectTypeShape<TDeclaringType> DeclaringType { get; }

    /// <summary>
    /// Creates a delegate wrapping a parameterless constructor, if applicable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <see cref="IConstructorShape.Parameters"/> property of the constructor is not empty.</exception>
    /// <returns>A parameterless delegate creating a default instance of <typeparamref name="TArgumentState"/>.</returns>
    Func<TDeclaringType> GetDefaultConstructor();

    /// <summary>
    /// Creates a constructor delegate for creating a default argument state instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <see cref="IConstructorShape.Parameters"/> property of the constructor is empty.</exception>
    /// <returns>A delegate for constructing new <typeparamref name="TArgumentState"/> instances.</returns>
    Func<TArgumentState> GetArgumentStateConstructor();

    /// <summary>
    /// Creates a constructor delegate parameterized on an argument state object.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <see cref="IConstructorShape.Parameters"/> property of the constructor is empty.</exception>
    /// <returns>A parameterized delegate returning an instance of <typeparamref name="TDeclaringType"/>.</returns>
    Constructor<TArgumentState, TDeclaringType> GetParameterizedConstructor();
}