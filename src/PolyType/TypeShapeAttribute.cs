﻿using PolyType.Abstractions;
using PolyType.ReflectionProvider;

namespace PolyType;

/// <summary>
/// Configures the generated <see cref="ITypeShape"/> of the annotated type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class TypeShapeAttribute : Attribute
{
    private readonly TypeShapeKind? _kind;
    private readonly MethodShapeFlags? _includeMethods;

    /// <summary>
    /// Gets a type implementing an <see cref="IMarshaller{T,TSurrogate}"/> to a surrogate type.
    /// </summary>
    /// <remarks>
    /// The type should have a parameterless constructor and must implement <see cref="IMarshaller{T,TSurrogate}"/>
    /// where either of the two generic types should match the annotated type.
    ///
    /// Types that specify a <see cref="Marshaller"/> will be of shape <see cref="ISurrogateTypeShape"/>.
    /// </remarks>
    public Type? Marshaller { get; init; }

    /// <summary>
    /// Gets the kind that should be generated for the annotated type.
    /// </summary>
    /// <remarks>
    /// Passing <see cref="TypeShapeKind.None"/> will result in the generation of
    /// an <see cref="IObjectTypeShape"/> that does not contain any properties or constructors.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The specified value is not a valid <see cref="TypeShapeKind"/>.</exception>
    public TypeShapeKind Kind
    {
        get => _kind ?? TypeShapeKind.None;
        init
        {
            if (!ReflectionHelpers.IsEnumDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The specified value is not a valid TypeShapeKind.");
            }

            _kind = value;
        }
    }

    /// <summary>
    /// Gets the binding flags that determine what method shapes should be included in the type shape.
    /// </summary>
    /// <remarks>
    /// <para>If left unspecified, only methods annotated with <see cref="MethodShapeAttribute"/> will be included.</para>
    /// <para>
    /// This type can only be used to control inclusion of public methods in the shape models.
    /// Non-public methods can only be included individually via explicit <see cref="MethodShapeAttribute"/>
    /// annotations.
    /// </para>
    /// </remarks>
    public MethodShapeFlags IncludeMethods { get => _includeMethods ?? MethodShapeFlags.None; init => _includeMethods = value; }

    internal TypeShapeKind? GetRequestedKind() => _kind;
    internal MethodShapeFlags? GetRequestedIncludeMethods() => _includeMethods;
}
