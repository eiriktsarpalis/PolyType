using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a constructor parameter shape.
/// </summary>
/// <typeparam name="TArgumentState">The mutable constructor argument state type.</typeparam>
/// <typeparam name="TParameter">The constructor parameter type.</typeparam>
public sealed class SourceGenParameterShape<TArgumentState, TParameter> : IParameterShape<TArgumentState, TParameter>
    where TArgumentState : IArgumentState
{
    /// <inheritdoc/>
    public required int Position { get; init; }

    /// <inheritdoc/>
    public required string Name { get; init; }

    /// <inheritdoc/>
    public required ParameterKind Kind { get; init; }

    /// <inheritdoc/>
    public required bool IsRequired { get; init; }

    /// <inheritdoc/>
    public required bool IsNonNullable { get; init; }

    /// <inheritdoc/>
    public required bool IsPublic { get; init; }

    /// <inheritdoc/>
    public required ITypeShape<TParameter> ParameterType { get; init; }

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
    Setter<TArgumentState, TParameter> IParameterShape<TArgumentState, TParameter>.GetSetter() => Setter;
}
