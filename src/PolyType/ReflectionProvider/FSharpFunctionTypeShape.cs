using PolyType.Abstractions;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.FunctionTypeShapeDebugView))]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class FSharpFunctionTypeShape<TFunction, TArgumentState, TResult>(
    FSharpFuncInfo fSharpFuncInfo,
    ReflectionTypeShapeOptions options,
    ReflectionTypeShapeProvider provider)
    : ReflectionTypeShape<TFunction>(provider, options),
    IFunctionTypeShape<TFunction, TArgumentState, TResult>
    where TArgumentState : IArgumentState
{
    private IReadOnlyList<IParameterShape>? _parameters;
    private Func<TArgumentState>? _argumentStateConstructor;
    private MethodInvoker<TFunction, TArgumentState, TResult>? _functionInvoker;

    public ITypeShape<TResult> ReturnType => Provider.GetTypeShape<TResult>();
    public bool IsVoidLike => fSharpFuncInfo.IsVoidLike;
    public bool IsAsync => fSharpFuncInfo.IsAsync;
    public IReadOnlyList<IParameterShape> Parameters => _parameters ?? CommonHelpers.ExchangeIfNull(ref _parameters, GetParameters().AsReadOnlyList());
    public override TypeShapeKind Kind => TypeShapeKind.Function;
    ITypeShape IFunctionTypeShape.ReturnType => ReturnType;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitFunction(this, state);
    public TFunction FromAsyncDelegate(RefFunc<TArgumentState, ValueTask<TResult>> innerFunc)
    {
        if (!IsAsync)
        {
            throw new InvalidOperationException($"The underlying function type '{typeof(TFunction)}' is not asynchronous.");
        }

        throw new NotSupportedException("Constructing F# functions from delegate types is currently not supported.");
    }

    public TFunction FromDelegate(RefFunc<TArgumentState, TResult> innerFunc)
    {
        if (IsAsync)
        {
            throw new InvalidOperationException($"The underlying function type '{typeof(TFunction)}' is asynchronous.");
        }

        throw new NotSupportedException("Constructing F# functions from delegate types is currently not supported.");
    }

    public Func<TArgumentState> GetArgumentStateConstructor()
    {
        return _argumentStateConstructor ?? CommonHelpers.ExchangeIfNull(ref _argumentStateConstructor, Provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(fSharpFuncInfo));
    }

    public MethodInvoker<TFunction, TArgumentState, TResult> GetFunctionInvoker()
    {
        return _functionInvoker ?? CommonHelpers.ExchangeIfNull(ref _functionInvoker, Provider.MemberAccessor.CreateFSharpFunctionInvoker<TFunction, TArgumentState, TResult>(fSharpFuncInfo)!);
    }

    private IEnumerable<IParameterShape> GetParameters()
    {
        for (int i = 0; i < fSharpFuncInfo.Parameters.Length; i++)
        {
            yield return Provider.CreateParameter(typeof(TArgumentState), fSharpFuncInfo, i);
        }
    }
}
