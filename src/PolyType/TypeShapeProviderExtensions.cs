namespace PolyType;

/// <summary>
/// Extension methods for the <see cref="ITypeShapeProvider"/> interface.
/// </summary>
public static class TypeShapeProviderExtensions
{
    /// <summary>
    /// Gets a <see cref="ITypeShape"/> instance corresponding to the specified type.
    /// </summary>
    /// <param name="typeShapeProvider">The shape provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <param name="type">The type whose shape we need to resolve.</param>
    /// <param name="throwIfMissing">If <see langword="true"/>, throws an exception if no shape is found.</param>
    /// <returns>An <see cref="ITypeShape"/> instance describing <paramref name="type"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="typeShapeProvider"/> does not support <paramref name="type"/>.</exception>
    public static ITypeShape? GetTypeShape(this ITypeShapeProvider typeShapeProvider, Type type, bool throwIfMissing)
    {
        ITypeShape? typeShape = typeShapeProvider.GetTypeShape(type);
        if (typeShape is null && throwIfMissing)
        {
            ThrowTypeShapeNotFound();
            void ThrowTypeShapeNotFound() => throw new NotSupportedException($"The type shape provider '{typeShapeProvider.GetType()}' does not support type '{type}'.");
        }

        return typeShape;
    }

    /// <summary>
    /// Gets a <see cref="ITypeShape{T}"/> instance corresponding to the specified type.
    /// </summary>
    /// <typeparam name="T">The type whose shape we need to resolve.</typeparam>
    /// <param name="typeShapeProvider">The shape provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <param name="throwIfMissing">If <see langword="true"/>, throws an exception if no shape is found.</param>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="typeShapeProvider"/> does not support <typeparamref name="T"/>.</exception>
    public static ITypeShape<T>? GetTypeShape<T>(this ITypeShapeProvider typeShapeProvider, bool throwIfMissing = false)
    {
        ITypeShape<T>? typeShape = (ITypeShape<T>?)typeShapeProvider.GetTypeShape(typeof(T));
        if (typeShape is null && throwIfMissing)
        {
            ThrowTypeShapeNotFound();
            void ThrowTypeShapeNotFound() => throw new NotSupportedException($"The type shape provider '{typeShapeProvider.GetType()}' does not support type '{typeof(T)}'.");
        }

        return typeShape;
    }
}
