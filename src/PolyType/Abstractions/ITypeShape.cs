using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET type.
/// </summary>
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
    public ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state = null);

    /// <summary>
    /// Invokes the specified generic function with the given state.
    /// </summary>
    /// <param name="func">The generic function to be invoked.</param>
    /// <param name="state">The state to be passed to the function.</param>
    /// <returns>The result produced by the function.</returns>
    object? Invoke(ITypeShapeFunc func, object? state = null);

    /// <summary>
    /// Closes a generic type associated to this property's declared <see cref="Type"/>,
    /// as captured in <see cref="TypeShapeAttribute.AssociatedTypes"/> or
    /// <see cref="TypeShapeExtensionAttribute.AssociatedTypes"/>.
    /// </summary>
    /// <param name="associatedType">
    /// A generic type definition (which must be one found in the <see cref="TypeShapeAttribute.AssociatedTypes"/> property if using the <see cref="SourceGenModel.SourceGenTypeShapeProvider"/>).
    /// </param>
    /// <returns>A closed generic type, baesd on <paramref name="associatedType"/> and the generic type arguments in this <see cref="Type"/>, or <see langword="null" /> if no matching associated type is available.</returns>
    /// <remarks>
    /// <see cref="ReflectionProvider.ReflectionTypeShapeProvider"/> can produce the closed generic on demand without any <see cref="TypeShapeAttribute.AssociatedTypes"/>,
    /// while <see cref="SourceGenModel.SourceGenTypeShapeProvider"/> is expected to only produce a closed generic if its generic type definition was explicitly requested via attribute.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="associatedType"/> is not a generic type definition.</exception>
    /// <exception cref="InvalidOperationException">Thrown when this method is called on an instance that does not represent a generic type.</exception>
#if NET
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
    Type? GetAssociatedType(Type associatedType);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET type.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
public interface ITypeShape<T> : ITypeShape;
