using PolyType.Abstractions;
using PolyType.ReflectionProvider;

namespace PolyType;

/// <summary>
/// Configures the generated <see cref="ITypeShape"/> of the annotated type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class TypeShapeAttribute : Attribute
{
    private const TypeShapeKind Undefined = (TypeShapeKind)(-1);
    private readonly TypeShapeKind _kind = Undefined;

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
        get => _kind;
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
    /// Types for which a factory should be generated when a type shape is generated for
    /// the type this attribute is applied to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each type must declare a public default constructor.
    /// </para>
    /// <para>
    /// If the type this attribute is applied to is a generic type definition,
    /// each type in this array must also be a generic type definition
    /// with the same number of generic type parameters.
    /// </para>
    /// </remarks>
    public Type[] AssociatedTypes { get; } = [];

    internal TypeShapeKind? GetRequestedKind() => _kind == Undefined ? null : _kind;
}