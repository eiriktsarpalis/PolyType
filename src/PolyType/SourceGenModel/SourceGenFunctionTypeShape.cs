using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for function type shapes.
/// </summary>
/// <typeparam name="TFunction">The function type.</typeparam>
/// <typeparam name="TArgumentState">The mutable state type used for aggregating function arguments.</typeparam>
/// <typeparam name="TResult">The return type of the underlying method.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.FunctionTypeShapeDebugView))]
public sealed class SourceGenFunctionTypeShape<TFunction, TArgumentState, TResult> : SourceGenTypeShape<TFunction>, IFunctionTypeShape<TFunction, TArgumentState, TResult>
    where TArgumentState : IArgumentState
{
    /// <inheritdoc/>
    public bool IsVoidLike { get; init; }

    /// <inheritdoc/>
    public bool IsAsync { get; init; }

    /// <summary>
    /// Gets a delayed return type shape factory for use with potentially recursive type graphs.
    /// </summary>
    public required Func<ITypeShape<TResult>> ReturnTypeFunc { get; init; }

    /// <inheritdoc/>
    [Obsolete("This member has been marked for deprecation and will be removed in the future.")]
    public ITypeShape<TResult> ReturnType
    {
        get => field ??= ReturnTypeFunc.Invoke();
        init;
    }

    /// <summary>
    /// Gets a factory method for creating parameter shapes.
    /// </summary>
    public Func<IEnumerable<IParameterShape>>? ParametersFactory { get; init; }

    /// <summary>
    /// Gets a delegate for creating argument state constructor.
    /// </summary>
    public required Func<TArgumentState> ArgumentStateConstructor { get; init; }

    /// <summary>
    /// Gets a delegate for invoking the function.
    /// </summary>
    public required MethodInvoker<TFunction, TArgumentState, TResult> FunctionInvoker { get; init; }

    /// <summary>
    /// Gets a delegate wrapping a generic user-defined delegate into an instance of <typeparamref name="TFunction"/>.
    /// </summary>
    public Func<RefFunc<TArgumentState, TResult>, TFunction>? FromDelegate { get; init; }

    /// <summary>
    /// Gets a delegate wrapping a generic user-defined async delegate into an instance of <typeparamref name="TFunction"/>.
    /// </summary>
    public Func<RefFunc<TArgumentState, ValueTask<TResult>>, TFunction>? FromAsyncDelegate { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Function;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitFunction(this, state);

    IReadOnlyList<IParameterShape> IFunctionTypeShape.Parameters => field ?? CommonHelpers.ExchangeIfNull(ref field, (ParametersFactory?.Invoke()).AsReadOnlyList());

#pragma warning disable CS0618 // Type or member is obsolete
    ITypeShape IFunctionTypeShape.ReturnType => ReturnType;
#pragma warning restore CS0618

    Func<TArgumentState> IFunctionTypeShape<TFunction, TArgumentState, TResult>.GetArgumentStateConstructor() => ArgumentStateConstructor;

    MethodInvoker<TFunction, TArgumentState, TResult> IFunctionTypeShape<TFunction, TArgumentState, TResult>.GetFunctionInvoker() => FunctionInvoker;

    TFunction IFunctionTypeShape<TFunction, TArgumentState, TResult>.FromDelegate(RefFunc<TArgumentState, TResult> innerFunc)
    {
        if (FromDelegate is not { } wrapper)
        {
            throw new InvalidOperationException("The underlying delegate is asynchronous.");
        }

        return wrapper(innerFunc);
    }

    TFunction IFunctionTypeShape<TFunction, TArgumentState, TResult>.FromAsyncDelegate(RefFunc<TArgumentState, ValueTask<TResult>> innerFunc)
    {
        if (FromAsyncDelegate is not { } wrapper)
        {
            throw new InvalidOperationException("The underlying delegate is not asynchronous.");
        }

        return wrapper(innerFunc);
    }
}
