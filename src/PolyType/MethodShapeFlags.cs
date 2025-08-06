namespace PolyType;

#if IS_MAIN_POLYTYPE_PROJECT
/// <summary>
/// Specifies flags that control what methods should be included in a generated type shape.
/// </summary>
/// <remarks>
/// This type can only be used to control inclusion of public methods in the shape models.
/// Non-public methods can only be included individually via explicit <see cref="MethodShapeAttribute"/>
/// annotations.
/// </remarks>
#endif
[Flags]
public enum MethodShapeFlags
{
    /// <summary>
    /// Specifies that no methods should be included in the shape, except those explicitly annotated with <see cref="PropertyShapeAttribute"/>.
    /// </summary>
    None = 0x0,

    /// <summary>
    /// Specifies that public instance methods should be included in the shape.
    /// </summary>
    PublicInstance = 0x1,

    /// <summary>
    /// Specifies that public static methods should be included in the shape.
    /// </summary>
    PublicStatic = 0x2,

    /// <summary>
    /// Specifies that both public and static methods should be included in the shape.
    /// </summary>
    AllPublic = PublicStatic | PublicInstance,
}
