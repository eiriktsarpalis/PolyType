namespace PolyType.Abstractions;

/// <summary>
/// Specifies the kind of constructor parameter.
/// </summary>
public enum ConstructorParameterKind
{
    /// <summary>
    /// Represents a constructor parameter.
    /// </summary>
    ConstructorParameter,

    /// <summary>
    /// Represents a member initializer.
    /// </summary>
    MemberInitializer,
}