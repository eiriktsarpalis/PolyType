using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a constructor parameter shape.
/// </summary>
/// <typeparam name="TArgumentState">The mutable constructor argument state type.</typeparam>
/// <typeparam name="TParameter">The constructor parameter type.</typeparam>
public sealed class SourceGenParameterShape<TArgumentState, TParameter>(ITypeShapeProvider provider) : IParameterShape<TArgumentState, TParameter>(provider)
{
    /// <summary>
    /// Gets the position of the parameter in the constructor signature.
    /// </summary>
    public required int PositionSetter { private get; init; }

    /// <inheritdoc/>
    public override int Position => PositionSetter;

    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public required string NameSetter { private get; init; }

    /// <inheritdoc/>
    public override string Name => NameSetter;

    /// <summary>
    /// Gets the kind of the parameter.
    /// </summary>
    public required ParameterKind KindSetter { private get; init; }

    /// <inheritdoc/>
    public override ParameterKind Kind => KindSetter;

    /// <summary>
    /// Gets a value indicating whether the parameter is required.
    /// </summary>
    public required bool IsRequiredSetter { private get; init; }

    /// <inheritdoc/>
    public override bool IsRequired => IsRequiredSetter;

    /// <summary>
    /// Gets a value indicating whether the parameter is non-nullable.
    /// </summary>
    public required bool IsNonNullableSetter { private get; init; }

    /// <inheritdoc/>
    public override bool IsNonNullable => IsNonNullableSetter;

    /// <summary>
    /// Gets a value indicating whether the parameter is declared public.
    /// </summary>
    public required bool IsPublicSetter { private get; init; }

    /// <inheritdoc/>
    public override bool IsPublic => IsPublicSetter;

    /// <summary>
    /// Gets the type shape of the parameter.
    /// </summary>
    public required ITypeShape<TParameter> ParameterTypeSetter { get; init; }

    /// <inheritdoc/>
    public override ITypeShape<TParameter> ParameterType => ParameterTypeSetter;

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
    public bool HasDefaultValueSetter { get; init; }

    /// <inheritdoc/>
    public override bool HasDefaultValue => HasDefaultValueSetter;

    /// <summary>
    /// Gets the default value of the parameter.
    /// </summary>
    public TParameter? DefaultValueSetter { get; init; }

    /// <inheritdoc/>
    public override TParameter? DefaultValue => DefaultValueSetter;

    /// <inheritdoc/>
    public override ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc?.Invoke();

    /// <inheritdoc/>
    public override Setter<TArgumentState, TParameter> GetSetter() => Setter;
}
