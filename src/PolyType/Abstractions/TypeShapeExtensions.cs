using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Abstractions;

/// <summary>
/// Extension methods for the various type shape interfaces.
/// </summary>
public static class TypeShapeExtensions
{
    /// <summary>
    /// Retrieves the default constructor for the given type shape, if available.
    /// </summary>
    /// <typeparam name="T">The type to be constructed.</typeparam>
    /// <param name="shape">The type's object shape.</param>
    /// <returns>A delegate for the default constructor; or <see langword="null" /> if none is available.</returns>
    public static Func<T>? GetDefaultConstructor<T>(this IObjectTypeShape<T> shape) =>
        (Func<T>?)shape.Constructor?.Accept(DefaultConstructorVisitor.Instance);

    /// <inheritdoc cref="GetDefaultConstructor{T}(IObjectTypeShape{T})"/>
    public static Func<object>? GetDefaultConstructor(this IObjectTypeShape shape) =>
        (Func<object>?)shape.Constructor?.Accept(DefaultConstructorVisitor.Instance);

    extension(TypeShapeResolver)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        /// <inheritdoc cref="TypeShapeResolver.ResolveDynamicOrThrow{T}()"/>
#if NET8_0
        [RequiresDynamicCode(TypeShapeResolver.ResolveDynamicMessage)]
#endif
#if NET
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete($"Check that T actually has a [GenerateShape] attribute or otherwise implements or is constrained to IShapeable<T>. If T is declared in an assembly that does not target .NET, use {nameof(TypeShapeResolver)}.{nameof(TypeShapeResolver.ResolveDynamicOrThrow)}<T>() instead.", error: true)]
#endif
        public static ITypeShape<T> Resolve<T>()
            => TypeShapeResolver.ResolveDynamicOrThrow<T>();

        /// <inheritdoc cref="TypeShapeResolver.ResolveDynamicOrThrow{T, TProvider}()"/>
#if NET8_0
        [RequiresDynamicCode(TypeShapeResolver.ResolveDynamicMessage)]
#endif
#if NET
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete($"Check that TProvider actually has a [GenerateShape] attribute or otherwise implements or is constrained to IShapeable<T>. If TProvider is declared in an assembly that does not target .NET, use {nameof(TypeShapeResolver)}.{nameof(TypeShapeResolver.ResolveDynamicOrThrow)}<T, TProvider>() instead.", error: true)]
#endif
        public static ITypeShape<T> Resolve<T, TProvider>()
            => TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    private sealed class DefaultConstructorVisitor : TypeShapeVisitor
    {
        internal static readonly DefaultConstructorVisitor Instance = new();

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
            => constructorShape.Parameters is [] ? constructorShape.GetDefaultConstructor() : null;
    }
}
