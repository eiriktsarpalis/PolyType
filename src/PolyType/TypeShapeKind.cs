#if IS_MAIN_POLYTYPE_PROJECT
using PolyType.Abstractions;
#endif

namespace PolyType;

/// <summary>
/// Defines kinds of an <see cref="ITypeShape{T}"/> instance.
/// </summary>
#if IS_MAIN_POLYTYPE_PROJECT
public
#else
internal
#endif
enum TypeShapeKind
{
    /// <summary>
    /// Default value not representing any specific shape.
    /// </summary>
    None = 0,

    /// <summary>
    /// Shape represents an object type using <see cref="IObjectTypeShape"/>.
    /// </summary>
    Object = 1,

    /// <summary>
    /// Shape represents an enum type using <see cref="IEnumTypeShape"/>.
    /// </summary>
    Enum = 2,

    /// <summary>
    /// Shape represents an optional type using <see cref="IOptionalTypeShape"/>.
    /// </summary>
    Optional = 3,

    /// <summary>
    /// Shape represents an enumerable type using <see cref="IEnumerableTypeShape"/>.
    /// </summary>
    Enumerable = 4,

    /// <summary>
    /// Shape represents a dictionary type using <see cref="IDictionaryTypeShape"/>.
    /// </summary>
    Dictionary = 5,

    /// <summary>
    /// Shape that maps to a surrogate type using <see cref="ISurrogateTypeShape"/>.
    /// </summary>
    Surrogate = 6,

    /// <summary>
    /// Shape that maps to a union type using <see cref="IUnionTypeShape"/>.
    /// </summary>
    Union = 7,

    /// <summary>
    /// Shape that maps to a delegate or function type using <see cref="IFunctionTypeShape"/>.
    /// </summary>
    Function = 8,
}
