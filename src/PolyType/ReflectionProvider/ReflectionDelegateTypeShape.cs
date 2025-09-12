using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.FunctionTypeShapeDebugView))]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionDelegateTypeShape<TDelegate, TArgumentState, TResult>(MethodShapeInfo methodShapeInfo, ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionTypeShape<TDelegate>(provider, options), IFunctionTypeShape<TDelegate, TArgumentState, TResult>
    where TDelegate : Delegate
    where TArgumentState : IArgumentState
{
    private IReadOnlyList<IParameterShape>? _parameters;
    private Func<TArgumentState>? _argumentStateConstructor;
    private MethodInvoker<TDelegate, TArgumentState, TResult>? _functionInvoker;
    private Func<RefFunc<TArgumentState, TResult>, TDelegate>? _fromDelegate;
    private Func<RefFunc<TArgumentState, ValueTask<TResult>>, TDelegate>? _fromAsyncDelegate;

    public bool IsVoidLike => methodShapeInfo.IsVoidLike;
    public bool IsAsync => methodShapeInfo.IsAsync;
    public IReadOnlyList<IParameterShape> Parameters => _parameters ?? CommonHelpers.ExchangeIfNull(ref _parameters, GetParameters().AsReadOnlyList());
    public override TypeShapeKind Kind => TypeShapeKind.Function;
    public ITypeShape<TResult> ReturnType => Provider.GetTypeShape<TResult>();
    ITypeShape IFunctionTypeShape.ReturnType => ReturnType;

    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitFunction(this, state);

    public Func<TArgumentState> GetArgumentStateConstructor()
    {
        return _argumentStateConstructor ?? CommonHelpers.ExchangeIfNull(ref _argumentStateConstructor, Provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(methodShapeInfo));
    }

    public MethodInvoker<TDelegate, TArgumentState, TResult> GetFunctionInvoker()
    {
        return _functionInvoker ?? CommonHelpers.ExchangeIfNull(ref _functionInvoker, Provider.MemberAccessor.CreateMethodInvoker<TDelegate, TArgumentState, TResult>(methodShapeInfo)!);
    }

    private IEnumerable<IParameterShape> GetParameters()
    {
        for (int i = 0; i < methodShapeInfo.Parameters.Length; i++)
        {
            yield return Provider.CreateParameter(typeof(TArgumentState), methodShapeInfo, i);
        }
    }

    public TDelegate FromDelegate(RefFunc<TArgumentState, TResult> innerFunc)
    {
        if (IsAsync)
        {
            throw new InvalidOperationException($"The underlying delegate type '{typeof(TDelegate)}' is asynchronous.");
        }

        _fromDelegate ??= Provider.MemberAccessor.CreateDelegateWrapper<TDelegate, TArgumentState, TResult>(methodShapeInfo);
        return _fromDelegate(innerFunc);
    }

    public TDelegate FromAsyncDelegate(RefFunc<TArgumentState, ValueTask<TResult>> innerFunc)
    {
        if (!IsAsync)
        {
            throw new InvalidOperationException($"The underlying delegate type '{typeof(TDelegate)}' is not asynchronous.");
        }

        _fromAsyncDelegate ??= Provider.MemberAccessor.CreateDelegateWrapper<TDelegate, TArgumentState, ValueTask<TResult>>(methodShapeInfo);
        return _fromAsyncDelegate(innerFunc);
    }
}
