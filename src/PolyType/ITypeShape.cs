using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType;

/// <summary>
/// Provides a strongly typed shape model for a given .NET type.
/// </summary>
[InternalImplementationsOnly]
public interface ITypeShape
{
    /// <summary>
    /// Gets the underlying <see cref="Type"/> that this instance represents.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets determines the <see cref="TypeShapeKind"/> that the current shape supports.
    /// </summary>
    TypeShapeKind Kind { get; }

    /// <summary>
    /// Gets the provider used to generate this instance.
    /// </summary>
    public ITypeShapeProvider Provider { get; }

    /// <summary>
    /// Gets the provider used for type-level attribute resolution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides fast attribute resolution when using the source generator,
    /// otherwise this is wrapping standard reflection-based attribute resolution.
    /// </para>
    /// <para>
    /// When using the source generator, the following categories of attributes are excluded to reduce trimmed application size:
    /// <list type="bullet">
    /// <item><description><c>System.Runtime.CompilerServices.*</c> - Compiler-generated attributes</description></item>
    /// <item><description><c>System.Runtime.InteropServices.*</c> - COM interop attributes</description></item>
    /// <item><description><c>System.Diagnostics.*</c> - Diagnostic attributes</description></item>
    /// <item><description><c>System.Reflection.DefaultMemberAttribute</c> - Default member metadata</description></item>
    /// <item><description><c>System.CLSCompliantAttribute</c> - CLS compliance marker</description></item>
    /// <item><description><c>Microsoft.FSharp.Core.*</c> - F# compiler generated attributes</description></item>
    /// <item><description>Attributes marked with unmet <see cref="ConditionalAttribute" /> annotations.</description></item>
    /// </list>
    /// Users requiring complete attribute resolution can use the <see cref="Type"/> property
    /// to access standard reflection-based attribute APIs, though this will be slower.
    /// </para>
    /// </remarks>
    IGenericCustomAttributeProvider AttributeProvider { get; }

    /// <summary>
    /// Gets all available method shapes for the given type.
    /// </summary>
    /// <returns>An enumeration of all available method shapes.</returns>
    IReadOnlyList<IMethodShape> Methods { get; }

    /// <summary>
    /// Gets all available event shapes for the given type.
    /// </summary>
    /// <returns>An enumeration of all available method shapes.</returns>
    IReadOnlyList<IEventShape> Events { get; }

    /// <summary>
    /// Accepts a <see cref="TypeShapeVisitor"/> for type graph traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <summary>
    /// Invokes the specified generic function with the given state.
    /// </summary>
    /// <param name="func">The generic function to be invoked.</param>
    /// <param name="state">The state to be passed to the function.</param>
    /// <returns>The result produced by the function.</returns>
    object? Invoke(ITypeShapeFunc func, object? state = null);

    /// <summary>
    /// Gets the shape for a type associated to this property's declared <see cref="Type"/>,
    /// as captured in <see cref="AssociatedTypeShapeAttribute.AssociatedTypes"/> or
    /// <see cref="TypeShapeExtensionAttribute.AssociatedTypes"/>.
    /// </summary>
    /// <param name="associatedType">
    /// The associated type (which must be one found in the <see cref="AssociatedTypeShapeAttribute.AssociatedTypes"/> property).
    /// If the associated type is a generic type definition, the type arguments used on this shape's <see cref="Type"/>
    /// will be used to close the associated generic type.
    /// </param>
    /// <returns>The shape for the associated type, or <see langword="null" /> if no shape for the associated type is available.</returns>
    /// <remarks>
    /// <see cref="ReflectionProvider.ReflectionTypeShapeProvider"/> can produce the shape on demand without any <see cref="AssociatedTypeShapeAttribute.AssociatedTypes"/>,
    /// while <see cref="SourceGenModel.SourceGenTypeShapeProvider"/> is expected to only produce the shape that was explicitly requested via attribute.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when this method is called on an instance that does not represent a generic type.</exception>
    ITypeShape? GetAssociatedTypeShape(Type associatedType);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET type.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
[InternalImplementationsOnly]
public interface ITypeShape<T> : ITypeShape;
