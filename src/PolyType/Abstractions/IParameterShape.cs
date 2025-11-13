using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET method parameter,
/// representing either an actual parameter or a member initializer.
/// </summary>
[InternalImplementationsOnly]
public interface IParameterShape
{
    /// <summary>
    /// Gets the 0-indexed position of the current method parameter.
    /// </summary>
    int Position { get; }

    /// <summary>
    /// Gets the shape of the method parameter type.
    /// </summary>
    ITypeShape ParameterType { get; }

    /// <summary>
    /// Gets the name of the method parameter.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets specifies the kind of the current parameter.
    /// </summary>
    ParameterKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter has a default value.
    /// </summary>
    bool HasDefaultValue { get; }

    /// <summary>
    /// Gets the default value specified by the parameter, if applicable.
    /// </summary>
    object? DefaultValue { get; }

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
    bool IsRequired { get; }

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
    bool IsNonNullable { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter is a public property or field initializer.
    /// </summary>
    bool IsPublic { get; }

    /// <summary>
    /// Gets the underlying <see cref="System.Reflection.ParameterInfo"/> representing the parameter, if available.
    /// </summary>
    /// <remarks>
    /// Returns a value when the parameter kind is <see cref="ParameterKind.MethodParameter" />,
    /// or <see langword="null"/> otherwise.
    /// </remarks>
    ParameterInfo? ParameterInfo { get; }

    /// <summary>
    /// Gets the underlying <see cref="System.Reflection.MemberInfo"/> representing the parameter, if available.
    /// </summary>
    /// <remarks>
    /// Returns a <see cref="PropertyInfo"/> or <see cref="FieldInfo"/> when the parameter kind is <see cref="ParameterKind.MemberInitializer" />,
    /// or <see langword="null"/> otherwise.
    /// </remarks>
    MemberInfo? MemberInfo { get; }

    /// <summary>
    /// Gets the provider used for parameter-level attribute resolution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides fast attribute resolution when using the source generator,
    /// otherwise this is wrapping standard reflection-based attribute resolution.
    /// </para>
    /// <para>
    /// When using the source generator, the following categories of attributes are excluded to reduce trimmed application size:
    /// <list type="bullet">
    /// <item><description><c>System.Runtime.CompilerServices.*</c> - Compiler-generated attributes</description></item>
    /// <item><description><c>System.Runtime.InteropServices.*</c> - COM interop attributes</description></item>
    /// <item><description><c>System.Diagnostics.*</c> - Diagnostic attributes</description></item>
    /// <item><description><c>System.Reflection.DefaultMemberAttribute</c> - Default member metadata</description></item>
    /// <item><description><c>System.CLSCompliantAttribute</c> - CLS compliance marker</description></item>
    /// <item><description><c>Microsoft.FSharp.Core.*</c> - F# compiler generated attributes</description></item>
    /// <item><description>Attributes marked with <see cref="ConditionalAttribute" /></description></item>
    /// </list>
    /// Users requiring complete attribute resolution can use the <see cref="ParameterInfo"/> or <see cref="MemberInfo"/> properties
    /// to access standard reflection-based attribute APIs, though this will be slower.
    /// </para>
    /// </remarks>
    IGenericCustomAttributeProvider AttributeProvider { get; }

    /// <summary>
    /// Accepts an <see cref="TypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(TypeShapeVisitor visitor, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET method parameter,
/// representing either an actual method parameter or a required or init-only property.
/// </summary>
/// <typeparam name="TArgumentState">The state type used for aggregating method arguments.</typeparam>
/// <typeparam name="TParameterType">The type of the underlying method parameter.</typeparam>
[InternalImplementationsOnly]
public interface IParameterShape<TArgumentState, TParameterType> : IParameterShape
{
    /// <summary>
    /// Gets the shape of the method parameter type.
    /// </summary>
    new ITypeShape<TParameterType> ParameterType { get; }

    /// <summary>
    /// Gets the default value specified by the parameter, if applicable.
    /// </summary>
    new TParameterType? DefaultValue { get; }

    /// <summary>
    /// Creates a getter delegate extracting a parameter value from a state object.
    /// </summary>
    /// <returns>A <see cref="Getter{TDeclaringType, TPropertyType}"/> delegate.</returns>
    Getter<TArgumentState, TParameterType> GetGetter();

    /// <summary>
    /// Creates a setter delegate for configuring a state object
    /// with a value for the current argument.
    /// </summary>
    /// <returns>A <see cref="Setter{TDeclaringType, TPropertyType}"/> delegate.</returns>
    Setter<TArgumentState, TParameterType> GetSetter();
}