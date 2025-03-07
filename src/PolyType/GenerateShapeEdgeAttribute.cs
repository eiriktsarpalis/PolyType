using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType;

/// <summary>
/// An attribute that describes an "edge" from a closed generic type that gets a shape generated for it
/// to an open generic type that should have a factory generated for it, using the type arguments from the originating type shape.
/// </summary>
/// <remarks>
/// This attribute ensures that source generated type shapes in an AOT application can produce a factory for
/// <see cref="IGenericTypeShape.GetRelatedTypeFactory(Type)"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public class GenerateShapeEdgeAttribute : Attribute
{
    private const string GenericTypeDefinitionRequired = "The type must be a generic type definition.";

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateShapeEdgeAttribute"/> class.
    /// </summary>
    /// <param name="from">The generic type definition of a type which, if a type shape is generated for it, should lead to the construction of a factory for an associated generic type.</param>
    /// <param name="to">
    /// The generic type definition of the type to make available via <see cref="IGenericTypeShape.GetRelatedTypeFactory(Type)" />.
    /// This type must be public and have a public, default constructor.
    /// </param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public GenerateShapeEdgeAttribute(Type from, Type to)
    {
        if (from is null)
        {
            throw new ArgumentNullException(nameof(from));
        }

        if (to is null)
        {
            throw new ArgumentNullException(nameof(to));
        }

        if (!from.IsGenericTypeDefinition)
        {
            throw new ArgumentException(GenericTypeDefinitionRequired, nameof(from));
        }

        if (!to.IsGenericTypeDefinition)
        {
            throw new ArgumentException(GenericTypeDefinitionRequired, nameof(to));
        }

        if (from.GetGenericArguments().Length != to.GetGenericArguments().Length)
        {
            throw new ArgumentException("The number of generic arguments must match between the two types.");
        }

        From = from;
        To = to;
    }

    public Type From { get; }

    public Type To { get; }
}
