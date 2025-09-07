using System.Diagnostics.CodeAnalysis;

namespace PolyType.Abstractions;

/// <summary>
/// Links the declared type to a specific <see cref="ITypeShapeProvider"/> implementation.
/// </summary>
/// <remarks>
/// Used by the source generator as a substitute for the <see cref="IShapeable{T}"/>
/// interface in targets where static abstract interface members are
/// not available.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class TypeShapeProviderAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TypeShapeProviderAttribute"/> class.
    /// </summary>
    /// <param name="shapeProvider">The linked type shape provider implementation.</param>
    /// <remarks>
    /// The parameter <paramref name="shapeProvider"/> should expose a public
    /// parameterless constructor and implement <see cref="ITypeShapeProvider"/>.
    /// </remarks>
    public TypeShapeProviderAttribute([DynamicallyAccessedMembers(TypeShapeProviderRequirements)] Type shapeProvider)
    {
        ShapeProvider = shapeProvider;
    }

    /// <summary>
    /// The linked type implementing <see cref="ITypeShapeProvider"/>.
    /// </summary>
    [DynamicallyAccessedMembers(TypeShapeProviderRequirements)]
    public Type ShapeProvider { get; }

    private const DynamicallyAccessedMemberTypes TypeShapeProviderRequirements =
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
        DynamicallyAccessedMemberTypes.Interfaces;
}