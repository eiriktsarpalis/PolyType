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
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <summary>
    /// Gets the parameter shapes for the constructor.
    /// </summary>
    public Func<IEnumerable<IParameterShape>>? GetParametersFunc { get; init; }

    /// <summary>
    /// Gets the default constructor for the declaring type.
    /// </summary>
    public Func<TDeclaringType>? DefaultConstructorFunc { get; init; }

    /// <summary>
    /// Gets the argument state constructor for the constructor.
    /// </summary>
    public Func<TArgumentState>? ArgumentStateConstructorFunc { get; init; }

    /// <summary>
    /// Gets the parameterized constructor for the constructor.
    /// </summary>
    public Constructor<TArgumentState, TDeclaringType>? ParameterizedConstructorFunc { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<IParameterShape> Parameters => _parameters ?? CommonHelpers.ExchangeIfNull(ref _parameters, (GetParametersFunc?.Invoke()).AsReadOnlyList());

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IReadOnlyList<IParameterShape>? _parameters;

    Func<TDeclaringType> IConstructorShape<TDeclaringType, TArgumentState>.GetDefaultConstructor() =>
        DefaultConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a default constructor.");

    Func<TArgumentState> IConstructorShape<TDeclaringType, TArgumentState>.GetArgumentStateConstructor() =>
        ArgumentStateConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a parameterized constructor.");

    Constructor<TArgumentState, TDeclaringType> IConstructorShape<TDeclaringType, TArgumentState>.GetParameterizedConstructor() =>
        ParameterizedConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a parameterized constructor.");

    object? IConstructorShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitConstructor(this, state);
    ICustomAttributeProvider? IConstructorShape.AttributeProvider => AttributeProviderFunc?.Invoke();
    IObjectTypeShape IConstructorShape.DeclaringType => DeclaringType;

    private string DebuggerDisplay => $".ctor({string.Join(", ", Parameters.Select(p => $"{p.ParameterType} {p.Name}"))})";
}
