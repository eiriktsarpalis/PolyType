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
public sealed class GenerateShapeForAttribute<T> : Attribute
{
    /// <inheritdoc cref="TypeShapeAttribute.Marshaler"/>
    public Type? Marshaler { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.Kind" />
    public TypeShapeKind Kind { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.IncludeMethods" />
    public MethodShapeFlags IncludeMethods { get; init; }
}

/// <summary>
/// Instructs the PolyType source generator to include types
/// in the <see cref="ITypeShapeProvider"/> that it generates.
/// </summary>
/// <remarks>
/// <para>
/// The source generator will include a static property in the annotated class pointing
/// to the <see cref="ITypeShapeProvider"/> that was generated for the entire project.
/// </para>
/// <para>
/// For projects targeting .NET 8 or later, this additionally augments the class
/// with an implementation of IShapeable for the generated types.
/// </para>
/// </remarks>
/// <seealso cref="GenerateShapeForAttribute{T}"/>
/// <seealso cref="GenerateShapeAttribute"/>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
[Conditional("NEVER")] // only the source generator uses this.
public sealed class GenerateShapeForAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateShapeForAttribute"/> class with a specific type.
    /// </summary>
    /// <param name="target">The type for which shape metadata will be generated. This must not be an open-generic type.</param>
    public GenerateShapeForAttribute(Type target)
    {
        Target = target;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateShapeForAttribute"/> class with glob patterns.
    /// </summary>
    /// <param name="typeNamePatterns">Glob patterns matching fully qualified type names.</param>
    /// <remarks>
    /// Patterns support wildcards: '*' matches any sequence of characters, '?' matches a single character.
    /// Patterns without wildcard characters are matched exactly against fully qualified type names.
    /// Examples: "MyNamespace.*", "*.Dtos.*", "MyNamespace.Person*", "MyNamespace.ExactTypeName".
    /// </remarks>
    public GenerateShapeForAttribute(params string[] typeNamePatterns)
    {
        TypeNamePatterns = typeNamePatterns;
    }

    /// <summary>
    /// Gets the target type being generated, or null if patterns are used.
    /// </summary>
    public Type? Target { get; }

    /// <summary>
    /// Gets the type name patterns to match, or null if a specific type is used.
    /// </summary>
    public string[]? TypeNamePatterns { get; }

    /// <inheritdoc cref="TypeShapeAttribute.Marshaler"/>
    public Type? Marshaler { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.Kind" />
    public TypeShapeKind Kind { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.IncludeMethods" />
    public MethodShapeFlags IncludeMethods { get; init; }
}
