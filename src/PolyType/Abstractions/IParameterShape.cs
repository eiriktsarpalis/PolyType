using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET method parameter,
/// representing either an actual parameter or a member initializer.
/// </summary>
public abstract class IParameterShape(ITypeShapeProvider provider)
{
    /// <summary>
    /// Gets the type shape provider that created this object.
    /// </summary>
    public ITypeShapeProvider Provider => provider;

    /// <summary>
    /// Gets the 0-indexed position of the current method parameter.
    /// </summary>
    public abstract int Position { get; }

    /// <summary>
    /// Gets the shape of the method parameter type.
    /// </summary>
    public ITypeShape ParameterType => ParameterTypeNonGeneric;

    /// <summary>
    /// Gets the name of the method parameter.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets specifies the kind of the current parameter.
    /// </summary>
    public abstract ParameterKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter has a default value.
    /// </summary>
    public abstract bool HasDefaultValue { get; }

    /// <summary>
    /// Gets the default value specified by the parameter, if applicable.
    /// </summary>
    public object? DefaultValue => HasDefaultValue ? this.DefaultValueNonGeneric : null;

    /// <inheritdoc cref="DefaultValue"/>
    protected abstract object? DefaultValueNonGeneric { get; }

    /// <summary>
    /// Gets a value indicating whether a value is required for the current parameter.
    /// </summary>
    /// <remarks>
    /// A parameter is reported as required if it is either a
    /// parameter without a default value or related to a property declared with the <see langword="required" /> modifier
    /// where the constructor is not annotated with <see cref="SetsRequiredMembersAttribute"/>.
    /// This value will switch to the value set by <see cref="PropertyShapeAttribute.IsRequired"/>
    /// or <see cref="ParameterShapeAttribute.IsRequired"/> (successively) if they are set.
    /// </remarks>
    public abstract bool IsRequired { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter requires non-null values.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="true" /> if the parameter type is a non-nullable struct, a non-nullable reference type
    /// or the parameter has been annotated with the <see cref="DisallowNullAttribute"/>.
    ///
    /// Conversely, it could return <see langword="false"/> if a non-nullable parameter
    /// has been annotated with <see cref="AllowNullAttribute"/>.
    /// </remarks>
    public abstract bool IsNonNullable { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter is a public property or field initializer.
    /// </summary>
    public abstract bool IsPublic { get; }

    /// <summary>
    /// Gets the provider used for parameter-level attribute resolution.
    /// </summary>
    public abstract ICustomAttributeProvider? AttributeProvider { get; }

    /// <inheritdoc cref="ParameterType"/>
    protected abstract ITypeShape ParameterTypeNonGeneric { get; }

    /// <summary>
    /// Accepts an <see cref="TypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET method parameter,
/// representing either an actual method parameter or a required or init-only property.
/// </summary>
/// <typeparam name="TArgumentState">The state type used for aggregating method arguments.</typeparam>
/// <typeparam name="TParameterType">The type of the underlying method parameter.</typeparam>
public abstract class IParameterShape<TArgumentState, TParameterType>(ITypeShapeProvider provider) : IParameterShape(provider)
{
    /// <summary>
    /// Gets the shape of the method parameter type.
    /// </summary>
    public new virtual ITypeShape<TParameterType> ParameterType => Provider.Resolve<TParameterType>();

    /// <inheritdoc/>
    protected override ITypeShape ParameterTypeNonGeneric => this.ParameterType;

    /// <inheritdoc cref="IParameterShape.DefaultValue"/>
    public new abstract TParameterType? DefaultValue { get; }

    /// <inheritdoc/>
    protected override object? DefaultValueNonGeneric => DefaultValue;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitParameter(this, state);

    /// <summary>
    /// Creates a setter delegate for configuring a state object
    /// with a value for the current argument.
    /// </summary>
    /// <returns>A <see cref="Setter{TDeclaringType, TPropertyType}"/> delegate.</returns>
    public abstract Setter<TArgumentState, TParameterType> GetSetter();
}