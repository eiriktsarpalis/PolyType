namespace PolyType;

/// <summary>
/// Specifies a known union case to be included in the type hierarchy of the current type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public class DerivedTypeShapeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DerivedTypeShapeAttribute"/> class.
    /// </summary>
    /// <param name="type">The derived type associated with the current union case.</param>
    public DerivedTypeShapeAttribute(Type type)
    {
        Throw.IfNull(type);
        Type = type;
        Name = Utilities.ReflectionUtilities.GetDerivedTypeShapeName(type);
    }

    /// <summary>
    /// Gets the derived type associated with the current union case.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets a unique string identifier for the current union case.
    /// </summary>
    /// <remarks>
    /// Defaults to the name of the derived type if left unspecified.
    /// </remarks>
    public string Name { get; init; }

    /// <summary>
    /// Gets a unique numeric identifier for the current union case.
    /// </summary>
    /// <remarks>
    /// <para>Used when serializing unions types in compact formats that require numeric identifiers.</para>
    /// <para>Defaults to the order in which the union case was declared if left unspecified or set to a negative value.</para>
    /// <para>
    /// Certain runtimes such as mono do not preserve ordering attribute declarations when using reflection.
    /// It is recommended that such use cases either set the property explicitly or use the source generator instead.
    /// </para>
    /// </remarks>
    public int Tag { get; init; } = -1;
}