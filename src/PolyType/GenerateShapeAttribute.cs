using System.Diagnostics;

namespace PolyType;

/// <summary>
/// Instructs the PolyType source generator to include the annotated type
/// in the <see cref="ITypeShapeProvider"/> that it generates.
/// </summary>
/// <remarks>
/// For projects targeting .NET 8 or later, this additionally augments the type
/// with an implementation of IShapeable for the type.
///
/// Projects targeting older versions of .NET need to access the generated
/// <see cref="ITypeShapeProvider"/> instance through the static property
/// added to classes annotated with the <see cref="GenerateShapeForAttribute{T}"/>
/// or <see cref="GenerateShapeForAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
[Conditional("NEVER")] // only the source generator uses this.
public sealed class GenerateShapeAttribute : Attribute
{
    /// <inheritdoc cref="TypeShapeAttribute.Marshaler"/>
    public Type? Marshaler { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.Kind" />
    public TypeShapeKind Kind { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.IncludeMethods" />
    public MethodShapeFlags IncludeMethods { get; init; }
}