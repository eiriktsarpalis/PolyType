using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionConstructorShape<TDeclaringType, TArgumentState>(
    ReflectionTypeShapeProvider provider,
    IObjectTypeShape<TDeclaringType> declaringType,
    IConstructorShapeInfo ctorInfo) :
    IConstructorShape<TDeclaringType, TArgumentState>
{
    private IReadOnlyList<IParameterShape>? _parameters;
    private Func<TArgumentState>? _argumentStateConstructor;
    private Constructor<TArgumentState, TDeclaringType>? _parameterizedConstructor;
    private Func<TDeclaringType>? _defaultConstructor;

    public override IObjectTypeShape<TDeclaringType> DeclaringType => declaringType;
    public override ICustomAttributeProvider? AttributeProvider => ctorInfo.AttributeProvider;
    public override bool IsPublic => ctorInfo.IsPublic;

    public override IReadOnlyList<IParameterShape> Parameters => _parameters ??= GetParameters().AsReadOnlyList();

    public override Func<TArgumentState> GetArgumentStateConstructor()
    {
        if (Parameters is [])
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current constructor shape is not parameterized.");
        }

        return _argumentStateConstructor ??= provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(ctorInfo);
    }

    public override Constructor<TArgumentState, TDeclaringType> GetParameterizedConstructor()
    {
        if (Parameters is [])
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current constructor shape is not parameterized.");
        }

        return _parameterizedConstructor ??= provider.MemberAccessor.CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ctorInfo);
    }

    public override Func<TDeclaringType> GetDefaultConstructor()
    {
        if (Parameters is not [])
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current constructor shape is not parameterless.");
        }

        return _defaultConstructor ??= provider.MemberAccessor.CreateDefaultConstructor<TDeclaringType>(ctorInfo);
    }

    private IEnumerable<IParameterShape> GetParameters()
    {
        for (int i = 0; i < ctorInfo.Parameters.Length; i++)
        {
            yield return provider.CreateParameter(typeof(TArgumentState), ctorInfo, i);
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