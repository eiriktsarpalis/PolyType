using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionConstructorShape<TDeclaringType, TArgumentState>(
    ReflectionTypeShapeProvider provider,
    IObjectTypeShape<TDeclaringType> declaringType,
    IMethodShapeInfo ctorInfo) :
    IConstructorShape<TDeclaringType, TArgumentState>
    where TArgumentState : IArgumentState
{
    private IReadOnlyList<IParameterShape>? _parameters;
    private Func<TArgumentState>? _argumentStateConstructor;
    private Constructor<TArgumentState, TDeclaringType>? _parameterizedConstructor;
    private Func<TDeclaringType>? _defaultConstructor;

    public IObjectTypeShape<TDeclaringType> DeclaringType { get; } = declaringType;
    public ICustomAttributeProvider? AttributeProvider => ctorInfo.AttributeProvider;
    public bool IsPublic => ctorInfo.IsPublic;
    IObjectTypeShape IConstructorShape.DeclaringType => DeclaringType;
    object? IConstructorShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitConstructor(this, state);

    public IReadOnlyList<IParameterShape> Parameters => _parameters ?? CommonHelpers.ExchangeIfNull(ref _parameters, GetParameters().AsReadOnlyList());

    public Func<TArgumentState> GetArgumentStateConstructor()
    {
        if (Parameters is [])
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current constructor shape is not parameterized.");
        }

        return _argumentStateConstructor ?? CommonHelpers.ExchangeIfNull(ref _argumentStateConstructor, provider.MemberAccessor.CreateConstructorArgumentStateCtor<TArgumentState>(ctorInfo));
    }

    public Constructor<TArgumentState, TDeclaringType> GetParameterizedConstructor()
    {
        if (Parameters is [])
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current constructor shape is not parameterized.");
        }

        return _parameterizedConstructor ?? CommonHelpers.ExchangeIfNull(ref _parameterizedConstructor, provider.MemberAccessor.CreateParameterizedConstructor<TArgumentState, TDeclaringType>(ctorInfo));
    }

    public Func<TDeclaringType> GetDefaultConstructor()
    {
        if (Parameters is not [])
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The current constructor shape is not parameterless.");
        }

        return _defaultConstructor ?? CommonHelpers.ExchangeIfNull(ref _defaultConstructor, provider.MemberAccessor.CreateDefaultConstructor<TDeclaringType>(ctorInfo));
    }

    private IEnumerable<IParameterShape> GetParameters()
    {
        for (int i = 0; i < ctorInfo.Parameters.Length; i++)
        {
            yield return provider.CreateParameter(typeof(TArgumentState), ctorInfo, i);
        }
    }
}