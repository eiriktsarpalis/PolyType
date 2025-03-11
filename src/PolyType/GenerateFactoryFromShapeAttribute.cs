using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType;

/// <summary>
/// An attribute that describes an "edge" from a closed generic type shape
/// to an open generic type that should have a factory generated for it, using the type arguments from the originating type shape.
/// </summary>
/// <remarks>
/// This attribute ensures that source generated type shapes in an AOT application can produce a factory for
/// <see cref="ITypeShape.GetRelatedTypeFactory(Type)"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public class GenerateFactoryFromShapeAttribute : Attribute
{
    private const string GenericTypeDefinitionRequired = "The type must be a generic type definition.";

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateFactoryFromShapeAttribute"/> class.
    /// </summary>
    /// <param name="from">The generic type definition of a type which, if a type shape is generated for it, should lead to the construction of a factory for an associated generic type.</param>
    /// <param name="to">
    /// The generic type definition of the type to make available via <see cref="ITypeShape.GetRelatedTypeFactory(Type)" />.
    /// This type must be public and have a public, default constructor.
    /// </param>
    public GenerateFactoryFromShapeAttribute(Type from, Type to)
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

    /// <summary>
    /// An open generic type for which a closed generic type shape may be produced by this or a referencing assembly.
    /// </summary>
    public Type From { get; }

    /// <summary>
    /// An open generic type for which a factory should be generated with type arguments that match those used
    /// to close the type that the <see cref="From"/> shape describes.
    /// </summary>
    public Type To { get; }
}
