using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET method.
/// </summary>
[InternalImplementationsOnly]
public interface IMethodShape
{
    /// <summary>
    /// Gets the shape of the declaring type for the method.
    /// </summary>
    ITypeShape DeclaringType { get; }

    /// <summary>
    /// Gets the shape of the return type of the method.
    /// </summary>
    ITypeShape ReturnType { get; }

    /// <summary>
    /// Gets the name of the method.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the method is declared public.
    /// </summary>
    bool IsPublic { get; }

    /// <summary>
    /// Gets a value indicating whether the method is static.
    /// </summary>
    bool IsStatic { get; }

    /// <summary>
    /// Gets a value indicating whether the underlying method returns <see langword="void"/>, <see cref="Task"/>, or <see cref="ValueTask"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/> the <see cref="ReturnType"/> is reported as <see cref="Unit"/>,
    /// </remarks>
    bool IsVoidLike { get; }

    /// <summary>
    /// Gets a value indicating whether the underlying method returns a <see cref="Task"/> or <see cref="ValueTask"/>.
    /// </summary>
    bool IsAsync { get; }

    /// <summary>
    /// Gets the provider used for method-level attribute resolution.
    /// </summary>
    ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Gets the shapes of the parameters accepted by the method.
    /// </summary>
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
/// Provides a strongly typed shape model for a given .NET method.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type for the method.</typeparam>
/// <typeparam name="TArgumentState">The mutable state type used for aggregating method arguments.</typeparam>
/// <typeparam name="TResult">The return type of the underlying method.</typeparam>
/// <remarks>
/// For underlying methods returning <see langword="void"/>, <see cref="Task"/>, or <see cref="ValueTask"/>
/// the <typeparamref name="TResult"/> type is represented with <see cref="Unit"/>.
/// </remarks>
[InternalImplementationsOnly]
public interface IMethodShape<TDeclaringType, TArgumentState, TResult> : IMethodShape
{
    /// <summary>
    /// Gets the shape of the declaring type for the method.
    /// </summary>
    new ITypeShape<TDeclaringType> DeclaringType { get; }

    /// <summary>
    /// Gets the shape of the return type of the method.
    /// </summary>
    new ITypeShape<TResult> ReturnType { get; }

    /// <summary>
    /// Gets a delegate for creating a default argument state instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <see cref="IMethodShape.Parameters"/> property of the method is empty.</exception>
    /// <returns>A delegate for creating an argument state instance with default parameter values.</returns>
    Func<TArgumentState> GetArgumentStateConstructor();

    /// <summary>
    /// Gets a delegate for invoking the method with the provided argument state.
    /// </summary>
    /// <returns>A <see cref="MethodInvoker{TDeclaringType, ArgumentState, TReturnType}"/> delegate.</returns>
    MethodInvoker<TDeclaringType?, TArgumentState, TResult> GetMethodInvoker();
}
