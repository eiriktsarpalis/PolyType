using PolyType.Abstractions;
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
    private readonly bool? _inferClosedTypePolymorphism;

    /// <summary>
    /// Gets a type implementing an <see cref="IMarshaler{T,TSurrogate}"/> to a surrogate type.
    /// </summary>
    /// <remarks>
    /// The type should have a parameterless constructor and must implement <see cref="IMarshaler{T,TSurrogate}"/>
    /// where either of the two generic types should match the annotated type.
    ///
    /// Types that specify a <see cref="Marshaler"/> will be of shape <see cref="ISurrogateTypeShape"/>.
    /// </remarks>
    public Type? Marshaler { get; init; }

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
    /// Gets a value indicating whether compiler-provided closed-class metadata should be used to infer a polymorphic type hierarchy.
    /// </summary>
    /// <value>The default value is <see langword="false"/>.</value>
    /// <remarks>
    /// <para>
    /// When enabled, the annotated type must be a C# closed class and its shape is a union,
    /// including when the hierarchy contains no cases. Closed subclasses are expanded recursively;
    /// only terminal, non-closed subclasses are registered. Configuration on intermediate closed
    /// subclasses applies to their own shapes and does not affect inference for the annotated type.
    /// </para>
    /// <para>
    /// Explicit <see cref="DerivedTypeShapeAttribute"/> registrations take precedence over
    /// <see cref="System.Runtime.Serialization.KnownTypeAttribute"/> registrations, which take precedence
    /// over inference. Inferred cases preserve compiler metadata order and receive implicit tags.
    /// Those tags are not stable across changes to the hierarchy.
    /// </para>
    /// <para>
    /// This setting cannot be combined with a <see cref="Marshaler"/> or an explicitly requested
    /// <see cref="Kind"/> other than <see cref="TypeShapeKind.Union"/>.
    /// </para>
    /// </remarks>
    public bool InferClosedTypePolymorphism
    {
        get => _inferClosedTypePolymorphism ?? false;
        init => _inferClosedTypePolymorphism = value;
    }

    /// <summary>
    /// Gets the binding flags that determine what method or event shapes should be included in the type shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property controls how the <see cref="ITypeShape.Methods"/> and <see cref="ITypeShape.Events"/>
    /// collections will be populated. If left unspecified, only methods annotated with <see cref="MethodShapeAttribute"/> or
    /// <see cref="EventShapeAttribute"/> will be included.</para>
    /// <para>
    /// This type can only be used to control inclusion of public methods in the shape models.
    /// Non-public methods can only be included individually via explicit attribute annotations.
    /// </para>
    /// </remarks>
    public MethodShapeFlags IncludeMethods { get => _includeMethods ?? MethodShapeFlags.None; init => _includeMethods = value; }

    internal TypeShapeKind? GetRequestedKind() => _kind;
    internal MethodShapeFlags? GetRequestedIncludeMethods() => _includeMethods;
    internal bool? GetRequestedInferClosedTypePolymorphism() => _inferClosedTypePolymorphism;
}
