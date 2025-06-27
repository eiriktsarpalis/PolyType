using System.Diagnostics;

namespace PolyType;

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
/// <seealso cref="GenerateShapeForAttribute"/>
/// <seealso cref="GenerateShapeAttribute"/>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
[Conditional("NEVER")] // only the source generator uses this, and avoiding generic attributes avoid .NET Framework and Unity issues.
public sealed class GenerateShapeForAttribute<T> : Attribute;

/// <summary>
/// Instructs the PolyType source generator to include <paramref name="type"/>
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
/// <seealso cref="GenerateShapeForAttribute{T}"/>
/// <seealso cref="GenerateShapeAttribute"/>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
[Conditional("NEVER")] // only the source generator uses this.
#pragma warning disable CS9113 // Parameter is unread.
public sealed class GenerateShapeForAttribute(Type type) : Attribute;
#pragma warning restore CS9113 // Parameter is unread.
