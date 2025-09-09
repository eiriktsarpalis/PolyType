using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for type shapes.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
public abstract class SourceGenTypeShape<T> : ITypeShape<T>
{
    /// <inheritdoc/>
    public abstract TypeShapeKind Kind { get; }

    /// <inheritdoc/>
    public required ITypeShapeProvider Provider { get; init; }

    Type ITypeShape.Type => typeof(T);
    ICustomAttributeProvider? ITypeShape.AttributeProvider => typeof(T);

    /// <summary>
    /// Gets the factory method for creating method shapes.
    /// </summary>
    public Func<IEnumerable<IMethodShape>>? CreateMethodsFunc { get; init; }

    /// <summary>
    /// Gets the factory method for creating event shapes.
    /// </summary>
    public Func<IEnumerable<IEventShape>>? CreateEventsFunc { get; init; }

    /// <summary>
    /// Gets the function that looks up associated type shapes by type.
    /// </summary>
    public Func<Type, ITypeShape?>? GetAssociatedTypeShapeFunc { get; init; }

    /// <inheritdoc/>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    object? ITypeShape.Invoke(ITypeShapeFunc func, object? state) => func.Invoke(this, state);

    /// <inheritdoc/>
    public IReadOnlyList<IMethodShape> Methods => _methods ?? CommonHelpers.ExchangeIfNull(ref _methods, (CreateMethodsFunc?.Invoke()).AsReadOnlyList());

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IReadOnlyList<IMethodShape>? _methods;

    /// <inheritdoc/>
    public IReadOnlyList<IEventShape> Events => _events ?? CommonHelpers.ExchangeIfNull(ref _events, (CreateEventsFunc?.Invoke()).AsReadOnlyList());

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IReadOnlyList<IEventShape>? _events;

    ITypeShape? ITypeShape.GetAssociatedTypeShape(Type associatedType)
    {
        if (associatedType is null)
        {
            ThrowArgumentNull();
            [DoesNotReturn]
            static void ThrowArgumentNull() => throw new ArgumentNullException(nameof(associatedType));
        }

        if (associatedType.IsGenericTypeDefinition && typeof(T).GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            ThrowArgumentException();
            [DoesNotReturn]
            static void ThrowArgumentException() => throw new ArgumentException("Type is not a generic type definition or does not have an equal count of generic type parameters with this type shape.", nameof(associatedType));
        }

        return GetAssociatedTypeShapeFunc?.Invoke(associatedType);
    }
}
