using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a constructor parameter shape.
/// </summary>
/// <typeparam name="TArgumentState">The mutable constructor argument state type.</typeparam>
/// <typeparam name="TParameter">The constructor parameter type.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.ParameterShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class SourceGenParameterShape<TArgumentState, TParameter> : IParameterShape<TArgumentState, TParameter>
    where TArgumentState : IArgumentState
{
    /// <inheritdoc/>
    public int Position { get; init; }

    /// <inheritdoc/>
    public string Name { get; init; } = null!;

    /// <inheritdoc/>
    public required ParameterKind Kind { get; init; }

    /// <inheritdoc/>
    public bool IsRequired { get; init; }

    /// <inheritdoc/>
    public bool IsNonNullable { get; init; }

    /// <inheritdoc/>
    public bool IsPublic { get; init; }

    /// <inheritdoc/>
    public required ITypeShape<TParameter> ParameterType { get; init; }

    /// <summary>
    /// Gets the getter for the parameter.
    /// </summary>
    public required Getter<TArgumentState, TParameter> Getter { get; init; }

    /// <summary>
    /// Gets the setter for the parameter.
    /// </summary>
    public required Setter<TArgumentState, TParameter> Setter { get; init; }

    /// <summary>
    /// Gets a constructor delegate for the custom attribute provider of the parameter.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <inheritdoc/>
    public bool HasDefaultValue { get; init; }

    /// <inheritdoc/>
    public TParameter? DefaultValue { get; init; }

    object? IParameterShape.DefaultValue => HasDefaultValue ? DefaultValue : null;
    ITypeShape IParameterShape.ParameterType => ParameterType;
    ICustomAttributeProvider? IParameterShape.AttributeProvider => AttributeProviderFunc?.Invoke();
    object? IParameterShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitParameter(this, state);
    Getter<TArgumentState, TParameter> IParameterShape<TArgumentState, TParameter>.GetGetter() => Getter;
    Setter<TArgumentState, TParameter> IParameterShape<TArgumentState, TParameter>.GetSetter() => Setter;

    private string DebuggerDisplay => $"{{Type = \"{typeof(TParameter)}\", Name = \"{Name}\"}}";
}
