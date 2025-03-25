namespace PolyType.Abstractions;

/// <summary>
/// Specifies the kind of method parameter.
/// </summary>
public enum ParameterKind
{
    /// <summary>
    /// Represents a method parameter.
    /// </summary>
    MethodParameter,

    /// <summary>
    /// Represents a member initializer.
    /// </summary>
    MemberInitializer,
}