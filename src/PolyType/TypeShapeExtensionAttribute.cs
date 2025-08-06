using PolyType.ReflectionProvider;

namespace PolyType;

/// <summary>
/// An assembly-level attribute that can extend an existing type's generated shape,
/// as if <see cref="TypeShapeAttribute"/> had been applied to the target type.
/// </summary>
/// <param name="target">The type to be extended.</param>
/// <remarks>
/// While this attribute may be applied to an assembly multiple times,
/// and may even be specified for a given <paramref name="target"/> multiple times,
/// the <see cref="Marshaller"/> property for a given <paramref name="target"/> must either be <see langword="null"/>
/// or must agree with other non-<see langword="null" /> properties on the attribute.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class TypeShapeExtensionAttribute(Type target) : Attribute
{
    private TypeShapeKind? _kind;
    private MethodShapeFlags? _includeMethods;

    /// <summary>
    /// Gets the target type.
    /// </summary>
    /// <remarks>
    /// This is the type that is being extended.
    /// If the type is declared in the same assembly that contains this attribute,
    /// consider removing this attribute in favor of applying <see cref="TypeShapeAttribute"/>
    /// directly to the target type.
    /// </remarks>
    public Type Target => target;

    /// <inheritdoc cref="TypeShapeAttribute.Marshaller"/>
    public Type? Marshaller { get; init; }

    /// <summary>
    /// Gets the elements of the generated shape that are required at runtime.
    /// </summary>
    /// <value>The default value is <see cref="TypeShapeRequirements.Full"/>.</value>
    /// <remarks>
    /// This property only impacts generation of object shapes.
    /// All other shapes (e.g. collections, enums, unions) are always completely defined.
    /// </remarks>
    public TypeShapeRequirements Requirements { get; init; } = TypeShapeRequirements.Full;

    /// <inheritdoc cref="TypeShapeAttribute.Kind" />
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

    /// <inheritdoc cref="TypeShapeAttribute.IncludeMethods" />
    public MethodShapeFlags IncludeMethods { get => _includeMethods ?? MethodShapeFlags.None; init => _includeMethods = value; }

    /// <summary>
    /// Types for which a shape should be generated when a type shape is generated for <see cref="Target"/>.
    /// </summary>
    /// <remarks>
    /// If <see cref="Target"/> is a generic type definition, each type in this array must also be a generic type definition
    /// with the same number of generic type parameters.
    /// </remarks>
    public Type[] AssociatedTypes { get; init; } = [];

    internal TypeShapeKind? GetRequestedKind() => _kind;
    internal MethodShapeFlags? GetRequestedIncludeMethods() => _includeMethods;
}
