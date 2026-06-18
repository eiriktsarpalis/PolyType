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
        /// <inheritdoc cref="TypeShapeResolver.ResolveDynamicOrThrow{T}()"/>
#if NET8_0
        [RequiresDynamicCode(TypeShapeResolver.ResolveDynamicMessage)]
#endif
#if NET
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use the TypeShapeResolver.Resolve<T>() method instead. If using the extension method syntax, check that your type argument actually has a [GenerateShape] attribute or otherwise implements IShapeable<T> to avoid a runtime failure.", error: true)]
#endif
        public static ITypeShape<T> Resolve<T>()
            => TypeShapeResolver.ResolveDynamicOrThrow<T>();

        /// <inheritdoc cref="TypeShapeResolver.ResolveDynamicOrThrow{T, TProvider}()"/>
#if NET8_0
        [RequiresDynamicCode(TypeShapeResolver.ResolveDynamicMessage)]
#endif
#if NET
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use the TypeShapeResolver.Resolve<T, TProvider>() method instead. If using the extension method syntax, check that your type argument actually has a [GenerateShape] attribute or otherwise implements IShapeable<T> to avoid a runtime failure.", error: true)]
#endif
        public static ITypeShape<T> Resolve<T, TProvider>()
            => TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
    }

    private sealed class DefaultConstructorVisitor : TypeShapeVisitor
    {
        internal static readonly DefaultConstructorVisitor Instance = new();

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
            => constructorShape.Parameters is [] ? constructorShape.GetDefaultConstructor() : null;
    }
}
