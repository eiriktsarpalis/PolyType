namespace PolyType;

#if IS_MAIN_POLYTYPE_PROJECT
/// <summary>
/// Specifies flags that control what method or event shapes should be included in a type shape.
/// </summary>
/// <remarks>
/// Can only be used to control inclusion of public methods in the shape models.
/// Non-public methods can only be included individually via explicit
/// <see cref="MethodShapeAttribute"/> or <see cref="EventShapeAttribute"/> annotations.
/// annotations.
/// </remarks>
#endif
[Flags]
public enum MethodShapeFlags
{
    /// <summary>
    /// Specifies that no methods or events should be included in the shape by default.
    /// </summary>
    None = 0x0,

    /// <summary>
    /// Specifies that public instance methods and events should be included in the shape by default.
    /// </summary>
    PublicInstance = 0x1,

    /// <summary>
    /// Specifies that public static methods and events should be included in the shape by default.
    /// </summary>
    PublicStatic = 0x2,

    /// <summary>
    /// Specifies that all public methods and events should be included in the shape by default.
    /// </summary>
    AllPublic = PublicInstance | PublicStatic,
}
