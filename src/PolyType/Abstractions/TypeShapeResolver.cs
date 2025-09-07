using PolyType.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Helper methods for extracting <see cref="ITypeShape"/> instances from <see cref="IShapeable{T}"/> implementations,
/// <see cref="TypeShapeProviderAttribute"/> annotations, or <see cref="ITypeShapeProvider"/> implementations.
/// </summary>
public static class TypeShapeResolver
{
#if NET
    /// <summary>
    /// Resolves the <see cref="ITypeShape{T}"/> from the <see cref="IShapeable{T}"/> implementation of the type.
    /// </summary>
    /// <typeparam name="T">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> Resolve<T>() where T : IShapeable<T> => T.GetShape();

    /// <summary>
    /// Resolves the <see cref="ITypeShape{T}"/> from the <see cref="IShapeable{T}"/> implementation of another type.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the shape.</typeparam>
    /// <typeparam name="TProvider">The type providing the <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> Resolve<T, TProvider>() where TProvider : IShapeable<T> => TProvider.GetShape();
#endif

    /// <summary>
    /// Resolves the <see cref="ITypeShape{T}"/> corresponding to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type whose shape we need to resolve.</typeparam>
    /// <param name="shapeProvider">The provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="shapeProvider"/> does not support <typeparamref name="T"/>.</exception>
    public static ITypeShape<T> Resolve<T>(this ITypeShapeProvider shapeProvider) =>
        (ITypeShape<T>)Resolve(shapeProvider, typeof(T));

    /// <summary>
    /// Resolves the <see cref="ITypeShape"/> corresponding to <paramref name="type"/>.
    /// </summary>
    /// <param name="shapeProvider">The provider from which to extract the <see cref="ITypeShape"/>.</param>
    /// <param name="type">The type whose shape we need to resolve.</param>
    /// <returns>An <see cref="ITypeShape"/> instance describing <paramref name="type"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="shapeProvider"/> does not support <paramref name="type"/>.</exception>
    public static ITypeShape Resolve(this ITypeShapeProvider shapeProvider, Type type) =>
        shapeProvider.GetShape(type) ?? throw new NotSupportedException($"The shape provider '{shapeProvider.GetType()}' does not support type '{type}'.");

    /// <summary>
    /// Uses reflection to resolve the source generated <see cref="ITypeShape{T}"/> implementation of the type.
    /// </summary>
    /// <typeparam name="T">The type from which to extract the shape.</typeparam>
    /// <param name="throwIfMissing">If <see langword="true"/>, throws an exception if no shape is found.</param>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>, or <see langword="null"/> if none is found.</returns>
    /// <exception cref="NotSupportedException"><paramref name="throwIfMissing"/> is <see langword="true"/> and no shape is found.</exception>"
    /// <remarks>
    /// <para>
    /// Uses reflection to look for potential <see cref="IShapeable{T}"/> implementations or
    /// <see cref="TypeShapeProviderAttribute"/> annotations inserted onto <typeparamref name="T"/>
    /// by the source generator.
    /// </para>
    /// <para>
    /// Intended for compatibility with older frameworks where the <see cref="IShapeable{T}"/>
    /// interface (which uses static abstract interface methods) is not available.
    /// </para>
    /// </remarks>
    public static ITypeShape<T>? ResolveDynamic<
#if NET
        [DynamicallyAccessedMembers(ResolveDynamicLinkerRequirements)]
#endif
        T>(bool throwIfMissing = false)
    {
        ITypeShape<T>? result = ResolveDynamicFactoryCache<T, T>.GetFactory()?.Invoke();
        if (throwIfMissing && result is null)
        {
            Throw();
            static void Throw() => throw new NotSupportedException($"The type '{typeof(T)}' does not have a generated shape. Ensure that the type is annotated with the 'GenerateShape' attribute.");
        }

        return result;
    }

    /// <summary>
    /// Uses reflection to resolve the source generated <see cref="ITypeShape{T}"/> implementation from another type.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the shape.</typeparam>
    /// <typeparam name="TProvider">The type from which to extract the shape.</typeparam>
    /// <param name="throwIfMissing">If <see langword="true"/>, throws an exception if no shape is found.</param>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>, or <see langword="null"/> if none is found.</returns>
    /// <exception cref="NotSupportedException"><paramref name="throwIfMissing"/> is <see langword="true"/> and no shape is found.</exception>"
    /// <remarks>
    /// <para>
    /// Uses reflection to look for potential <see cref="IShapeable{T}"/> implementations or
    /// <see cref="TypeShapeProviderAttribute"/> annotations inserted onto <typeparamref name="T"/>
    /// by the source generator.
    /// </para>
    /// <para>
    /// Intended for compatibility with older frameworks where the <see cref="IShapeable{T}"/>
    /// interface (which uses static abstract interface methods) is not available.
    /// </para>
    /// </remarks>
    public static ITypeShape<T>? ResolveDynamic<T,
#if NET
        [DynamicallyAccessedMembers(ResolveDynamicLinkerRequirements)]
#endif
        TProvider>(bool throwIfMissing = false)
    {
        ITypeShape<T>? result = ResolveDynamicFactoryCache<T, TProvider>.GetFactory()?.Invoke();
        if (throwIfMissing && result is null)
        {
            Throw();
            static void Throw() => throw new NotSupportedException($"The type '{typeof(TProvider)}' does not have a generated shape for '{typeof(T)}'. Ensure that the type is annotated with the 'GenerateShapeFor' attribute.");
        }

        return result;
    }

#if NET
    /// <summary>
    /// Gets the dynamic access requirements for the <see cref="ResolveDynamic{T}"/> method group.
    /// </summary>
    public const DynamicallyAccessedMemberTypes ResolveDynamicLinkerRequirements =
        // Only needed for IShapeable<T> implementation resolution.
        // cf. https://github.com/dotnet/runtime/issues/119440
        DynamicallyAccessedMemberTypes.Interfaces |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.NonPublicMethods;
#endif

    private static class ResolveDynamicFactoryCache<T,
#if NET
        [DynamicallyAccessedMembers(ResolveDynamicLinkerRequirements)]
#endif
        TProvider>
    {
        private static Func<ITypeShape<T>?>? s_cachedFactory;
        private static bool s_isCacheInitialized;

        public static Func<ITypeShape<T>?>? GetFactory()
        {
            if (!s_isCacheInitialized)
            {
                s_cachedFactory = CreateResolveDynamicFactory();
                Volatile.Write(ref s_isCacheInitialized, true);
            }

            return s_cachedFactory;
        }

        private static Func<ITypeShape<T>?>? CreateResolveDynamicFactory()
        {
            if (typeof(TProvider).GetCustomAttribute<TypeShapeProviderAttribute>() is { } attr)
            {
                Type shapeProviderType = attr.ShapeProvider;
                return () =>
                {
                    var shapeProvider = (ITypeShapeProvider)Activator.CreateInstance(shapeProviderType)!;
                    return shapeProvider.GetShape(typeof(T)) as ITypeShape<T>;
                };
            }
#if NET
            // For forward compatibility with newer target frameworks
            // also resolve potential IShapeable<T> implementations.
            if (typeof(IShapeable<T>).IsAssignableFrom(typeof(TProvider)))
            {
                foreach (MethodInfo method in typeof(TProvider).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.IsStatic &&
                        method.ReturnType == typeof(ITypeShape<T>) &&
                        method.GetParameters().Length == 0 &&
                        (method.Name is "GetShape" ||
                            (method.Name.StartsWith("global::PolyType.IShapeable<", StringComparison.Ordinal) &&
                             method.Name.EndsWith(".GetShape", StringComparison.Ordinal))))
                    {
                        return (Func<ITypeShape<T>>)Delegate.CreateDelegate(typeof(Func<ITypeShape<T>>), method)!;
                    }
                }
            }
#endif
            return null;
        }
    }
}
