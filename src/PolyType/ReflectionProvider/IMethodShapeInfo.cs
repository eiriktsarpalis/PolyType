using System.Diagnostics;
using System.Reflection;

namespace PolyType.ReflectionProvider;

internal interface IMethodShapeInfo
{
    Type ReturnType { get; }
    bool IsPublic { get; }
    ICustomAttributeProvider? AttributeProvider { get; }
    IParameterShapeInfo[] Parameters { get; }
}

internal sealed class MethodShapeInfo : IMethodShapeInfo
{
    public MethodShapeInfo(
        Type returnType,
        MethodBase? method,
        MethodParameterShapeInfo[] parameters,
        MemberInitializerShapeInfo[]? memberInitializers = null,
        string? name = null)
    {
        Debug.Assert(method is null or ConstructorInfo or MethodInfo);
        Debug.Assert(method is not null || returnType.IsValueType);
        Debug.Assert((method?.GetParameters().Length ?? 0) == parameters.Length);

        ReturnType = returnType;
        Method = method;
        ConstructorParameters = parameters;

        Name = name ?? method?.Name ?? ".ctor";
        MemberInitializers = memberInitializers ?? [];
        Parameters = MemberInitializers.Length == 0 ? ConstructorParameters : [.. ConstructorParameters, .. MemberInitializers];

        if (method is MethodInfo mI)
        {
            IsVoidLike = mI.GetEffectiveReturnType() is null;
            IsAsync = mI.IsAsyncMethod();
        }
    }

    public Type ReturnType { get; }
    public MethodBase? Method { get; }
    public string Name { get; }
    public bool IsPublic => Method is null or { IsPublic: true };
    public bool IsVoidLike { get; }
    public bool IsAsync { get; }
    public MethodParameterShapeInfo[] ConstructorParameters { get; }
    public MemberInitializerShapeInfo[] MemberInitializers { get; }

    public ICustomAttributeProvider? AttributeProvider => Method;
    public IParameterShapeInfo[] Parameters { get; }
}

internal sealed class TupleConstructorShapeInfo(
    Type constructedType,
    ConstructorInfo constructorInfo,
    MethodParameterShapeInfo[] constructorParameters,
    TupleConstructorShapeInfo? nestedTupleCtor) : IMethodShapeInfo
{
    private IParameterShapeInfo[]? _allParameters;

    public Type ReturnType { get; } = constructedType;
    public ConstructorInfo ConstructorInfo { get; } = constructorInfo;
    public MethodParameterShapeInfo[] ConstructorParameters { get; } = constructorParameters;
    public TupleConstructorShapeInfo? NestedTupleConstructor { get; } = nestedTupleCtor;
    public bool IsValueTuple => ReturnType.IsValueType;

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