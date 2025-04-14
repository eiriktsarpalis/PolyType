namespace PolyType.Abstractions;

/// <summary>
/// An attribute to apply to other attribute classes that accept one or more <see cref="Type"/> arguments
/// that should be considered associated types to whatever type the other attribute is applied to.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AssociatedTypeAttributeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssociatedTypeAttributeAttribute"/> class.
    /// </summary>
    /// <param name="parameterOrNamedArgumentName">
    /// The name of the attribute constructor parameter or named argument (i.e. attribute property)
    /// whose <see cref="Type"/> or <see cref="Type"/> array argument should be included as associated types.
    /// </param>
    /// <param name="requirements">The requirements to apply to each associated type.</param>
    public AssociatedTypeAttributeAttribute(string parameterOrNamedArgumentName, TypeShapeDepth requirements)
    {
        _ = parameterOrNamedArgumentName;
    }
}
