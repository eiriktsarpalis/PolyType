using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a constructor shape.
/// </summary>
/// <typeparam name="TDeclaringType">The type being constructed.</typeparam>
/// <typeparam name="TArgumentState">The mutable argument state for the constructor.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.ConstructorShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class SourceGenConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape<TDeclaringType, TArgumentState>
    where TArgumentState : IArgumentState
{
    /// <inheritdoc/>
    public bool IsPublic { get; init; }

    /// <inheritdoc/>
    public required IObjectTypeShape<TDeclaringType> DeclaringType { get; init; }

    /// <summary>
    /// Gets the attribute provider for the constructor.
    /// </summary>
    public Func<SourceGenAttributeInfo[]>? AttributeFactory { get; init; }

    /// <summary>
    /// Gets the method base resolver factory.
    /// </summary>
    public Func<MethodBase?>? MethodBaseFactory { get; init; }

    /// <summary>
    /// Gets the parameter shapes for the constructor.
    /// </summary>
    public Func<IEnumerable<IParameterShape>>? ParametersFactory { get; init; }

    /// <summary>
    /// Gets the default constructor for the declaring type.
    /// </summary>
    public Func<TDeclaringType>? DefaultConstructor { get; init; }

    /// <summary>
    /// Gets the argument state constructor for the constructor.
    /// </summary>
    public Func<TArgumentState>? ArgumentStateConstructor { get; init; }

    /// <summary>
    /// Gets the parameterized constructor for the constructor.
    /// </summary>
    public Constructor<TArgumentState, TDeclaringType>? ParameterizedConstructor { get; init; }

    IReadOnlyList<IParameterShape> IConstructorShape.Parameters => field ?? CommonHelpers.ExchangeIfNull(ref field, (ParametersFactory?.Invoke()).AsReadOnlyList());

    Func<TDeclaringType> IConstructorShape<TDeclaringType, TArgumentState>.GetDefaultConstructor() =>
        DefaultConstructor ?? throw new InvalidOperationException("Constructor shape does not specify a default constructor.");

    Func<TArgumentState> IConstructorShape<TDeclaringType, TArgumentState>.GetArgumentStateConstructor() =>
        ArgumentStateConstructor ?? throw new InvalidOperationException("Constructor shape does not specify a parameterized constructor.");

    Constructor<TArgumentState, TDeclaringType> IConstructorShape<TDeclaringType, TArgumentState>.GetParameterizedConstructor() =>
        ParameterizedConstructor ?? throw new InvalidOperationException("Constructor shape does not specify a parameterized constructor.");

    object? IConstructorShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitConstructor(this, state);
    IObjectTypeShape IConstructorShape.DeclaringType => DeclaringType;

    MethodBase? IConstructorShape.MethodBase => field ??= MethodBaseFactory?.Invoke();

    IGenericCustomAttributeProvider IConstructorShape.AttributeProvider => field ?? CommonHelpers.ExchangeIfNull(ref field, SourceGenCustomAttributeProvider.Create(AttributeFactory));

    private string DebuggerDisplay => $".ctor({string.Join(", ", ((IConstructorShape)this).Parameters.Select(p => $"{p.ParameterType} {p.Name}"))})";
}
