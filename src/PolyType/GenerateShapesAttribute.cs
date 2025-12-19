using System.Diagnostics;

namespace PolyType;

/// <summary>
/// Instructs the PolyType source generator to include types matching the specified glob patterns
/// in the <see cref="ITypeShapeProvider"/> that it generates.
/// </summary>
/// <param name="typeNamePattern">
/// A glob pattern matching fully qualified type names.
/// Patterns support wildcards: '*' matches any sequence of characters, '?' matches a single character.
/// Examples: "MyNamespace.*", "*.Dtos.*", "MyNamespace.Person*".
/// </param>
/// <param name="additionalPatterns">Additional patterns to match.</param>
/// <remarks>
/// <para>
/// The source generator will include a static property in the annotated class pointing
/// to the <see cref="ITypeShapeProvider"/> that was generated for the entire project.
/// </para>
/// <para>
/// For projects targeting .NET 8 or later, this additionally augments the class
/// with an implementation of IShapeable for matched types.
/// </para>
/// </remarks>
/// <seealso cref="GenerateShapeForAttribute"/>
/// <seealso cref="GenerateShapeForAttribute{T}"/>
/// <seealso cref="GenerateShapeAttribute"/>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
[Conditional("NEVER")] // only the source generator uses this.
public sealed class GenerateShapesAttribute(string typeNamePattern, params string[] additionalPatterns) : Attribute
{
    /// <summary>
    /// Gets the type name patterns to match.
    /// </summary>
    public string[] TypeNamePatterns { get; } = additionalPatterns.Length == 0
        ? [typeNamePattern]
        : [typeNamePattern, .. additionalPatterns];

    /// <inheritdoc cref="TypeShapeAttribute.Marshaler"/>
    public Type? Marshaler { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.Kind" />
    public TypeShapeKind Kind { get; init; }

    /// <inheritdoc cref="TypeShapeAttribute.IncludeMethods" />
    public MethodShapeFlags IncludeMethods { get; init; }
}
