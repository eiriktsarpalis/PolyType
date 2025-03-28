namespace PolyType;

/// <summary>
/// Configures the shape of a parameter for a given constructor.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public class ParameterShapeAttribute : Attribute
{
    private bool? _isRequired;

    /// <summary>
    /// Gets a custom name to be used for the particular parameter.
    /// </summary>
    public string? Name { get; init; }

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
    public bool IsRequiredSpecified => _isRequired.HasValue;
}
