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
    /// <summary>
    /// Gets the position of the parameter in the constructor signature.
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the kind of the parameter.
    /// </summary>
    public required ParameterKind Kind { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter is required.
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter is non-nullable.
    /// </summary>
    public required bool IsNonNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter is declared public.
    /// </summary>
    public required bool IsPublic { get; init; }

    /// <summary>
    /// Gets the type shape of the parameter.
    /// </summary>
    public required ITypeShape<TParameter> ParameterType { get; init; }

    /// <summary>
    /// Gets the setter for the parameter.
    /// </summary>
    public required Setter<TArgumentState, TParameter> Setter { get; init; }

    /// <summary>
    /// Gets a constructor delegate for the custom attribute provider of the parameter.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter has a default value.
    /// </summary>
    public bool HasDefaultValue { get; init; }

    /// <summary>
    /// Gets the default value of the parameter.
    /// </summary>
    public TParameter? DefaultValue { get; init; }

    object? IParameterShape.DefaultValue => HasDefaultValue ? DefaultValue : null;
    ITypeShape IParameterShape.ParameterType => ParameterType;
    ICustomAttributeProvider? IParameterShape.AttributeProvider => AttributeProviderFunc?.Invoke();
    object? IParameterShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitParameter(this, state);
    Setter<TArgumentState, TParameter> IParameterShape<TArgumentState, TParameter>.GetSetter() => Setter;
}
