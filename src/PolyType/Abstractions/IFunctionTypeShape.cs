namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for function types.
/// </summary>
/// <remarks>
/// A function type shape represents a delegate type or an F# function type.
/// </remarks>
[InternalImplementationsOnly]
public interface IFunctionTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the shape of the return type of the method.
    /// </summary>
    ITypeShape ReturnType { get; }

    /// <summary>
    /// Gets a value indicating whether the underlying function returns <see langword="void"/>, <see cref="Task"/>, or <see cref="ValueTask"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/> the <see cref="ReturnType"/> is reported as <see cref="Unit"/>,
    /// </remarks>
    bool IsVoidLike { get; }

    /// <summary>
    /// Gets a value indicating whether the underlying function returns a <see cref="Task"/> or <see cref="ValueTask"/>.
    /// </summary>
    bool IsAsync { get; }

    /// <summary>
    /// Gets the shapes of the parameters accepted by the method.
    /// </summary>
    IReadOnlyList<IParameterShape> Parameters { get; }
}

/// <summary>
/// Provides a strongly typed shape model for function types.
/// </summary>
/// <typeparam name="TFunction">The type of the function.</typeparam>
/// <typeparam name="TArgumentState">The mutable state type used for aggregating function arguments.</typeparam>
/// <typeparam name="TResult">The return type of the underlying function.</typeparam>
/// <remarks>
/// A function type shape represents a delegate type or an F# function type.
/// </remarks>
[InternalImplementationsOnly]
public interface IFunctionTypeShape<TFunction, TArgumentState, TResult> : ITypeShape<TFunction>, IFunctionTypeShape
{
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
    /// Gets a delegate for invoking the function with the provided argument state.
    /// </summary>
    /// <returns>A <see cref="MethodInvoker{TDeclaringType, ArgumentState, TReturnType}"/> delegate.</returns>
    MethodInvoker<TFunction, TArgumentState, TResult> GetFunctionInvoker();

    /// <summary>
    /// Wraps a generic user-defined delegate into an instance of <typeparamref name="TFunction"/>.
    /// </summary>
    /// <param name="innerFunc">The user-defined delegate to wrap.</param>
    /// <returns>A strongly typed delegate of arbitrary arity.</returns>
    /// <exception cref="InvalidOperationException">The underlying delegate is asynchronous.</exception>
    TFunction FromDelegate(RefFunc<TArgumentState, TResult> innerFunc);

    /// <summary>
    /// Wraps a generic user-defined delegate into an instance of <typeparamref name="TFunction"/>.
    /// </summary>
    /// <param name="innerFunc">The user-defined delegate to wrap.</param>
    /// <returns>A strongly typed delegate of arbitrary arity.</returns>
    /// <exception cref="InvalidOperationException">The underlying delegate is not asynchronous.</exception>
    TFunction FromAsyncDelegate(RefFunc<TArgumentState, ValueTask<TResult>> innerFunc);
}