namespace PolyType;

/// <summary>
/// Annotates a member of an enum to provide additional metadata for its shape representation.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class EnumMemberShapeAttribute : Attribute
{
    /// <summary>
    /// Gets a custom name to be used for the particular enum member in its containing <see cref="Abstractions.IEnumTypeShape"/>.
    /// </summary>
    public string? Name { get; init; }
}
