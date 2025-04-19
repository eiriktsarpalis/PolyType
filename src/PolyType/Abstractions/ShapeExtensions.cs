using System.ComponentModel;

namespace PolyType.Abstractions;

/// <summary>
/// Extension methods for the various type shape interfaces.
/// </summary>
public static class ShapeExtensions
{
    /// <summary>
    /// Retrieves the default constructor for the given type shape, if available.
    /// </summary>
    /// <typeparam name="T">The type to be constructed.</typeparam>
    /// <param name="shape">The type's object shape.</param>
    /// <returns>A delegate for the default constructor; or <see langword="null" /> if none is available.</returns>
    public static Func<T>? GetDefaultConstructor<T>(this IObjectTypeShape<T> shape) => (Func<T>?)shape.Constructor?.Accept(DefaultConstructorVisitor.Instance);

    /// <inheritdoc cref="GetDefaultConstructor{T}(IObjectTypeShape{T})"/>
    public static Func<object>? GetDefaultConstructor(this IObjectTypeShape shape) => (Func<object>?)shape.Constructor?.Accept(DefaultConstructorVisitor.Instance);

    private sealed class DefaultConstructorVisitor : TypeShapeVisitor
    {
        internal static readonly DefaultConstructorVisitor Instance = new();

        private DefaultConstructorVisitor()
        {
        }

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
            => constructorShape.Parameters is [] ? constructorShape.GetDefaultConstructor() : null;
    }
}
