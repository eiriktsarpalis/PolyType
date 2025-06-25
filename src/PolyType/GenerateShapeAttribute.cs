using System.Diagnostics;

namespace PolyType;

/// <summary>
/// Instructs the PolyType source generator to a given type
/// in the <see cref="ITypeShapeProvider"/> that it generates.
/// </summary>
/// <remarks>
/// <para>
/// For projects targeting .NET 8 or later, this additionally augments the type
/// with an implementation of IShapeable for the type.
/// </para>
/// <para>
/// Projects targeting older versions of .NET need to access the generated
/// <see cref="ITypeShapeProvider"/> instance through the static property
/// added to classes annotated with the <see cref="GenerateShapeAttribute{T}"/>
/// or <see cref="GenerateShapeAttribute(Type)"/>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
[Conditional("NEVER")] // only the source generator uses this.
public sealed class GenerateShapeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateShapeAttribute"/> class.
    /// This instructs the PolyType source generator to include the annotated type
    /// in the <see cref="ITypeShapeProvider"/> that it generates.
    /// </summary>
    public GenerateShapeAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateShapeAttribute"/> class.
    /// This instructs the PolyType source generator to include <paramref name="type"/>
    /// in the <see cref="ITypeShapeProvider"/> that it generates.
    /// </summary>
    /// <param name="type">The type for which shape metadata will be generated. This must not be an open-generic type.</param>
    /// <remarks>
    /// <para>
    /// The source generator will include a static property in the annotated class pointing
    /// to the <see cref="ITypeShapeProvider"/> that was generated for the entire project.
    /// </para>
    /// <para>
    /// For projects targeting .NET 8 or later, this additionally augments the class
    /// with an implementation of IShapeable for <paramref name="type"/>.
    /// </para>
    /// </remarks>
    public GenerateShapeAttribute(Type type)
    {
    }
}

/// <summary>
/// Instructs the PolyType source generator to include <typeparamref name="T"/>
/// in the <see cref="ITypeShapeProvider"/> that it generates.
/// </summary>
/// <typeparam name="T">The type for which shape metadata will be generated.</typeparam>
/// <remarks>
/// <para>
/// The source generator will include a static property in the annotated class pointing
/// to the <see cref="ITypeShapeProvider"/> that was generated for the entire project.
/// </para>
/// <para>
/// For projects targeting .NET 8 or later, this additionally augments the class
/// with an implementation of IShapeable for <typeparamref name="T"/>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
[Conditional("NEVER")] // only the source generator uses this, and avoiding generic attributes avoid .NET Framework and Unity issues.
public sealed class GenerateShapeAttribute<T> : Attribute;
