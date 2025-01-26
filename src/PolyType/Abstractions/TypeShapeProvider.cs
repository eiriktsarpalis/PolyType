namespace PolyType.Abstractions;

/// <summary>
/// Helper methods for extracting <see cref="ITypeShape"/> instances from shape providers.
/// </summary>
public static class TypeShapeProvider
{
#if NET
    /// <summary>
    /// Resolves the <see cref="ITypeShape"/> provided by the given type.
    /// </summary>
    /// <typeparam name="T">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> Resolve<T>() where T : IShapeable<T>
        => T.GetShape();

    /// <summary>
    /// Resolves the <see cref="ITypeShape"/> provided by the given type.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the shape.</typeparam>
    /// <typeparam name="TProvider">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> Resolve<T, TProvider>() where TProvider : IShapeable<T>
        => TProvider.GetShape();
#endif

    /// <summary>
    /// Resolves the <see cref="ITypeShape{T}"/> corresponding to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type whose shape we need to resolve.</typeparam>
    /// <param name="shapeProvider">The provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="shapeProvider"/> does not support <typeparamref name="T"/>.</exception>
    public static ITypeShape<T> Resolve<T>(this ITypeShapeProvider shapeProvider)
        => (ITypeShape<T>)Resolve(shapeProvider, typeof(T));

    /// <summary>
    /// Resolves the <see cref="ITypeShape"/> corresponding to <paramref name="type"/>.
    /// </summary>
    /// <param name="shapeProvider">The provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <param name="type">The type whose shape we need to resolve.</param>
    /// <returns>An <see cref="ITypeShape"/> instance describing <paramref name="type"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="shapeProvider"/> does not support <paramref name="type"/>.</exception>
    public static ITypeShape Resolve(this ITypeShapeProvider shapeProvider, Type type)
        => shapeProvider.GetShape(type) ?? throw new NotSupportedException($"The shape provider '{shapeProvider.GetType()}' does not support type '{type}'.");

    /// <summary>
    /// Creates a new <see cref="ITypeShapeProvider"/> that returns the first shape found
    /// from iterating over a given list of providers.
    /// </summary>
    /// <param name="providers">The sequence of providers to solicit for type shapes.</param>
    /// <returns>The aggregating <see cref="ITypeShapeProvider"/>.</returns>
    public static ITypeShapeProvider Combine(params ITypeShapeProvider[] providers)
    {
        return new AggregatingTypeShapeProvider(providers);
    }

    private sealed class AggregatingTypeShapeProvider(ITypeShapeProvider[] providers) : ITypeShapeProvider
    {
        public ITypeShape? GetShape(Type type)
        {
            foreach (ITypeShapeProvider provider in providers)
            {
                if (provider.GetShape(type) is ITypeShape shape)
                {
                    return shape;
                }
            }

            return null;
        }
    }
}