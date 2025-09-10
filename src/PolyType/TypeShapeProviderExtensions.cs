using System.Diagnostics.CodeAnalysis;

namespace PolyType;

/// <summary>
/// Extension methods for the <see cref="ITypeShapeProvider"/> interface.
/// </summary>
public static class TypeShapeProviderExtensions
{
    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the specified type, or throw if one doesn't exist.
    /// </summary>
    /// <param name="typeShapeProvider">The shape provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <param name="type">The type whose shape we need to resolve.</param>
    /// <returns>An <see cref="ITypeShape"/> instance corresponding to <paramref name="type"/>.</returns>
    /// <exception cref="NotSupportedException">The <paramref name="typeShapeProvider"/> does not support <paramref name="type"/>.</exception>
    public static ITypeShape GetTypeShapeOrThrow(this ITypeShapeProvider typeShapeProvider, Type type)
    {
        ITypeShape? typeShape = typeShapeProvider.GetTypeShape(type);
        if (typeShape is null)
        {
            ThrowTypeShapeNotFound();

            [DoesNotReturn]
            void ThrowTypeShapeNotFound() => throw new NotSupportedException($"The type shape provider '{typeShapeProvider.GetType()}' does not support type '{type}'.");
        }

        return typeShape;
    }

    /// <summary>
    /// Gets a <see cref="ITypeShape{T}"/> instance corresponding to the specified type, or throw if one doesn't exist.
    /// </summary>
    /// <typeparam name="T">The type whose shape should be resolved.</typeparam>
    /// <param name="typeShapeProvider">The shape provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <returns>
    /// An <see cref="ITypeShape{T}"/> instance corresponding to <typeparamref name="T"/>,
    /// or <see langword="null" /> if a shape is not available.
    /// </returns>
    public static ITypeShape<T>? GetTypeShape<T>(this ITypeShapeProvider typeShapeProvider) =>
        (ITypeShape<T>?)typeShapeProvider.GetTypeShape(typeof(T));

    /// <summary>
    /// Gets a <see cref="ITypeShape{T}"/> instance corresponding to the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose shape should be resolved.</typeparam>
    /// <param name="typeShapeProvider">The shape provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <returns>An <see cref="ITypeShape{T}"/> instance corresponding to <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException">The <paramref name="typeShapeProvider"/> does not support <typeparamref name="T"/>.</exception>
    public static ITypeShape<T> GetTypeShapeOrThrow<T>(this ITypeShapeProvider typeShapeProvider)
    {
        var typeShape = (ITypeShape<T>?)typeShapeProvider.GetTypeShape(typeof(T));
        if (typeShape is null)
        {
            ThrowTypeShapeNotFound();

            [DoesNotReturn]
            void ThrowTypeShapeNotFound() => throw new NotSupportedException($"The type shape provider '{typeShapeProvider.GetType()}' does not support type '{typeof(T)}'.");
        }

        return typeShape;
    }
}
