namespace PolyType;

/// <summary>
/// Configures the shape of a property or field for a given type.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class PropertyShapeAttribute : Attribute
{
    private bool? _isRequired;

    /// <summary>
    /// Gets a custom name to be used for the particular property or field.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the order in which the property or field should be included in the shape model.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets a value indicating whether the annotated property or field should be ignored in the shape model.
    /// </summary>
    public bool Ignore { get; init; }

    /// <summary>
    /// Gets a value indicating whether an argument should be supplied from the data source
    /// rather than relying on the default value of the parameter or its type.
    /// </summary>
    public bool IsRequired
    {
        get => _isRequired ?? false;
        set => _isRequired = value;
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="IsRequired"/> was explicitly set.
    /// </summary>
    internal bool IsRequiredSpecified => _isRequired.HasValue;
}
