#if IS_MAIN_POLYTYPE_PROJECT
using PolyType.Abstractions;
#endif

namespace PolyType;

/// <summary>
/// Defines the kind of union represented by an <see cref="IUnionTypeShape"/>.
/// </summary>
#if IS_MAIN_POLYTYPE_PROJECT
public
#else
internal
#endif
enum UnionKind
{
    /// <summary>
    /// A class or interface hierarchy with derived types declared
    /// via <see cref="DerivedTypeShapeAttribute"/> or equivalent annotations.
    /// </summary>
    ClassHierarchy = 0,

    /// <summary>
    /// An F# discriminated union.
    /// </summary>
    FSharpUnion = 1,

    /// <summary>
    /// A C# union type declared with <c>[Union]</c> and implementing <c>IUnion</c>.
    /// Case types are extracted from single-parameter constructors or
    /// <c>IUnionMembers</c> factory methods.
    /// </summary>
    CSharpUnion = 2,
}
