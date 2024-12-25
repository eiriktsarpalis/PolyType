﻿using System.Diagnostics;
using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionConstructorShape<TDeclaringType, TArgumentState>(
    ReflectionTypeShapeProvider provider,
    IConstructorShapeInfo ctorInfo) :
    IConstructorShape<TDeclaringType, TArgumentState>
{
    public IObjectTypeShape<TDeclaringType> DeclaringType => (IObjectTypeShape<TDeclaringType>)provider.GetShape<TDeclaringType>();
    public ICustomAttributeProvider? AttributeProvider => ctorInfo.AttributeProvider;
    public bool IsPublic => ctorInfo.IsPublic;
    IObjectTypeShape IConstructorShape.DeclaringType => DeclaringType;
    object? IConstructorShape.Accept(ITypeShapeVisitor visitor, object? state) => visitor.VisitConstructor(this, state);

    public IReadOnlyList<IConstructorParameterShape> Parameters => _parameters ??= GetParameters().AsReadOnlyList();
    private IReadOnlyList<IConstructorParameterShape>? _parameters;

    public Func<TArgumentState> GetArgumentStateConstructor()
    {
        if (Parameters is [])
        {
            throw new InvalidOperationException("The current constructor shape is not parameterized.");
        }

        return provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(ctorInfo);
    }

    public Constructor<TArgumentState, TDeclaringType> GetParameterizedConstructor()
    {
        if (Parameters is [])
        {
            throw new InvalidOperationException("The current constructor shape is not parameterized.");
        }

        return provider.MemberAccessor.CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ctorInfo);
    }

    public Func<TDeclaringType> GetDefaultConstructor()
    {
        if (Parameters is not [])
        {
            throw new InvalidOperationException("The current constructor shape is not parameterless.");
        }

        return provider.MemberAccessor.CreateDefaultConstructor<TDeclaringType>(ctorInfo);
    }

    private IEnumerable<IConstructorParameterShape> GetParameters()
    {
        for (int i = 0; i < ctorInfo.Parameters.Length; i++)
        {
            yield return provider.CreateConstructorParameter(typeof(TArgumentState), ctorInfo, i);
        }
    }
}

internal interface IConstructorShapeInfo
{
    Type ConstructedType { get; }
    bool IsPublic { get; }
    ICustomAttributeProvider? AttributeProvider { get; }
    IParameterShapeInfo[] Parameters { get; }
}

internal sealed class MethodConstructorShapeInfo : IConstructorShapeInfo
{
    public MethodConstructorShapeInfo(
        Type constructedType,
        MethodBase? constructorMethod,
        MethodParameterShapeInfo[] parameters,
        MemberInitializerShapeInfo[]? memberInitializers = null)
    {
        Debug.Assert(constructorMethod is null or ConstructorInfo or MethodInfo { IsStatic: true });
        Debug.Assert(constructorMethod != null || constructedType.IsValueType);
        Debug.Assert((constructorMethod?.GetParameters().Length ?? 0) == parameters.Length);

        ConstructedType = constructedType;
        ConstructorMethod = constructorMethod;
        ConstructorParameters = parameters;

        MemberInitializers = memberInitializers ?? [];
        Parameters = [ ..ConstructorParameters, ..MemberInitializers ];
    }

    public Type ConstructedType { get; }
    public MethodBase? ConstructorMethod { get; }
    public bool IsPublic => ConstructorMethod is null or { IsPublic: true };
    public MethodParameterShapeInfo[] ConstructorParameters { get; }
    public MemberInitializerShapeInfo[] MemberInitializers { get; }

    public ICustomAttributeProvider? AttributeProvider => ConstructorMethod;
    public IParameterShapeInfo[] Parameters { get; }
}

internal sealed class TupleConstructorShapeInfo(
    Type constructedType,
    ConstructorInfo constructorInfo,
    MethodParameterShapeInfo[] constructorParameters,
    TupleConstructorShapeInfo? nestedTupleCtor) : IConstructorShapeInfo
{
    private IParameterShapeInfo[]? _allParameters;

    public Type ConstructedType { get; } = constructedType;
    public ConstructorInfo ConstructorInfo { get; } = constructorInfo;
    public MethodParameterShapeInfo[] ConstructorParameters { get; } = constructorParameters;
    public TupleConstructorShapeInfo? NestedTupleConstructor { get; } = nestedTupleCtor;
    public bool IsValueTuple => ConstructedType.IsValueType;

    public ICustomAttributeProvider? AttributeProvider => ConstructorInfo;
    public IParameterShapeInfo[] Parameters => _allParameters ??= GetAllParameters().ToArray();
    public bool IsPublic => true;

    private IEnumerable<IParameterShapeInfo> GetAllParameters()
    {
        for (TupleConstructorShapeInfo? curr = this; curr != null; curr = curr.NestedTupleConstructor)
        {
            foreach (IParameterShapeInfo param in curr.ConstructorParameters)
            {
                yield return param;
            }
        }
    }
}