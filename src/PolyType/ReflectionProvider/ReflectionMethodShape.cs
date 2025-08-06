﻿using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionMethodShape<TDeclaringType, TArgumentState, TResult>
    : IMethodShape<TDeclaringType, TArgumentState, TResult>
    where TArgumentState : IArgumentState
{
    private readonly ReflectionTypeShapeProvider _provider;
    private readonly MethodShapeInfo _methodShapeInfo;
    private IReadOnlyList<IParameterShape>? _parameters;
    private Func<TArgumentState>? _argumentStateConstructor;
    private MethodInvoker<TDeclaringType, TArgumentState, TResult>? _methodInvoker;

    public ReflectionMethodShape(MethodShapeInfo methodShapeInfo, ReflectionTypeShapeProvider provider)
    {
        DebugExt.Assert(methodShapeInfo.Method != null);
        _provider = provider;
        _methodShapeInfo = methodShapeInfo;
    }

    public ITypeShape<TDeclaringType> DeclaringType => _provider.GetShape<TDeclaringType>();
    public ITypeShape<TResult> ReturnType => _provider.GetShape<TResult>();
    public string Name => _methodShapeInfo.Name;
    public bool IsPublic => _methodShapeInfo.IsPublic;
    public bool IsStatic => _methodShapeInfo.Method!.IsStatic;
    public bool IsVoidLike => _methodShapeInfo.IsVoidLike;
    public bool IsAsync => _methodShapeInfo.IsAsync;
    public ICustomAttributeProvider? AttributeProvider => _methodShapeInfo.Method;
    public IReadOnlyList<IParameterShape> Parameters => _parameters ?? CommonHelpers.ExchangeIfNull(ref _parameters, GetParameters().AsReadOnlyList());
    ITypeShape IMethodShape.DeclaringType => DeclaringType;
    ITypeShape IMethodShape.ReturnType => ReturnType;

    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitMethod(this, state);

    public Func<TArgumentState> GetArgumentStateConstructor()
    {
        return _argumentStateConstructor ?? CommonHelpers.ExchangeIfNull(ref _argumentStateConstructor, _provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(_methodShapeInfo));
    }

    public MethodInvoker<TDeclaringType, TArgumentState, TResult> GetMethodInvoker()
    {
        return _methodInvoker ?? CommonHelpers.ExchangeIfNull(ref _methodInvoker, _provider.MemberAccessor.CreateMethodInvoker<TDeclaringType, TArgumentState, TResult>(_methodShapeInfo));
    }

    private IEnumerable<IParameterShape> GetParameters()
    {
        for (int i = 0; i < _methodShapeInfo.Parameters.Length; i++)
        {
            yield return _provider.CreateParameter(typeof(TArgumentState), _methodShapeInfo, i);
        }
    }
}
