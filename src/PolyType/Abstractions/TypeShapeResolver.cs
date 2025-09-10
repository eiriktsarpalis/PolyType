using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Defines helpers methods used for resolving <see cref="ITypeShape"/> instances from source generated types.
/// </summary>
public static class TypeShapeResolver
{
    // C.f. https://github.com/dotnet/runtime/issues/119440#issuecomment-3269894751
    private const string ResolveDynamicMessage =
        "Dynamic resolution of IShapeable<T> interface may require dynamic code generation in .NET 8 Native AOT. " +
        "It is recommended to switch to statically resolved IShapeable<T> APIs or upgrade your app to .NET 9 or later.";

#if NET
    /// <summary>
    /// Resolves the <see cref="ITypeShape{T}"/> from the <see cref="IShapeable{T}"/> implementation of the type.
    /// </summary>
    /// <typeparam name="T">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> Resolve<T>() where T : IShapeable<T> => T.GetTypeShape();

    /// <summary>
    /// Resolves the <see cref="ITypeShape{T}"/> from the <see cref="IShapeable{T}"/> implementation of another type.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the shape.</typeparam>
    /// <typeparam name="TProvider">The type providing the <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    public static ITypeShape<T> Resolve<T, TProvider>() where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#endif

    /// <summary>
    /// Uses reflection to resolve the source generated <see cref="ITypeShape{T}"/> implementation of the type.
    /// </summary>
    /// <typeparam name="T">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>, or <see langword="null"/> if none is found.</returns>
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
#if NET8_0
    [RequiresDynamicCode(ResolveDynamicMessage)]
#endif
    public static ITypeShape<T>? ResolveDynamic<T>() =>
        ResolveDynamicFactoryCache<T, T>.GetFactory()?.Invoke();

    /// <summary>
    /// Uses reflection to resolve the source generated <see cref="ITypeShape{T}"/> implementation from another type.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the shape.</typeparam>
    /// <typeparam name="TProvider">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>, or <see langword="null"/> if none is found.</returns>
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
#if NET8_0
    [RequiresDynamicCode(ResolveDynamicMessage)]
#endif
    public static ITypeShape<T>? ResolveDynamic<T, TProvider>() =>
        ResolveDynamicFactoryCache<T, TProvider>.GetFactory()?.Invoke();

    /// <summary>
    /// Uses reflection to resolve the source generated <see cref="ITypeShape{T}"/> implementation of the type.
    /// </summary>
    /// <typeparam name="T">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException">No source generated shape could be resolved.</exception>
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
#if NET8_0
    [RequiresDynamicCode(ResolveDynamicMessage)]
#endif
    public static ITypeShape<T> ResolveDynamicOrThrow<T>()
    {
        ITypeShape<T>? result = ResolveDynamicFactoryCache<T, T>.GetFactory()?.Invoke();
        if (result is null)
        {
            ThrowNotSupported();

            [DoesNotReturn]
            static void ThrowNotSupported() => throw new NotSupportedException($"The type '{typeof(T)}' does not have a generated shape. Ensure that the type is annotated with the 'GenerateShape' attribute.");
        }

        return result;
    }

    /// <summary>
    /// Uses reflection to resolve the source generated <see cref="ITypeShape{T}"/> implementation from another type.
    /// </summary>
    /// <typeparam name="T">The type for which to extract the shape.</typeparam>
    /// <typeparam name="TProvider">The type from which to extract the shape.</typeparam>
    /// <returns>An <see cref="ITypeShape{T}"/> instance describing <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException">No source generated shape could be resolved.</exception>
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
#if NET8_0
    [RequiresDynamicCode(ResolveDynamicMessage)]
#endif
    public static ITypeShape<T> ResolveDynamicOrThrow<T, TProvider>()
    {
        ITypeShape<T>? result = ResolveDynamicFactoryCache<T, TProvider>.GetFactory()?.Invoke();
        if (result is null)
        {
            ThrowNotSupported();

            [DoesNotReturn]
            static void ThrowNotSupported() => throw new NotSupportedException($"The type '{typeof(TProvider)}' does not have a generated shape for '{typeof(T)}'. Ensure that the type is annotated with the 'GenerateShapeFor' attribute.");
        }

        return result;
    }

#if NET8_0
    [RequiresDynamicCode(ResolveDynamicMessage)]
#endif
    private static class ResolveDynamicFactoryCache<T, TProvider>
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
                Type typeShapeProviderType = attr.TypeShapeProvider;
                return () =>
                {
                    var typeShapeProvider = (ITypeShapeProvider)Activator.CreateInstance(typeShapeProviderType)!;
                    return typeShapeProvider.GetTypeShape(typeof(T)) as ITypeShape<T>;
                };
            }
#if NET
            // For forward compatibility with newer target frameworks
            // also resolve potential IShapeable<T> implementations.
            if (typeof(IShapeable<T>).IsAssignableFrom(typeof(TProvider)))
            {
                MethodInfo genericResolveMethod = typeof(TypeShapeResolver).GetMethod(nameof(Resolve), genericParameterCount: 2, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)!;
                MethodInfo resolveMethod = genericResolveMethod.MakeGenericMethod(typeof(T), typeof(TProvider));
                return resolveMethod.CreateDelegate<Func<ITypeShape<T>?>>();
            }
#endif
            return null;
        }
    }
}
