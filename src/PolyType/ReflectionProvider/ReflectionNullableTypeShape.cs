using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionNullableTypeShape<T>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionTypeShape<T?>(provider, options), IOptionalTypeShape<T?, T>
    where T : struct
{
    public override TypeShapeKind Kind => TypeShapeKind.Optional;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitOptional(this, state);

    public Func<T?> GetNoneConstructor() => static () => null;
    public Func<T, T?> GetSomeConstructor() => static t => t;
    public OptionDeconstructor<T?, T> GetDeconstructor() =>
        static (T? optional, out T value) =>
        {
            if (optional is null)
            {
                value = default;
                return false;
            }

            value = optional.Value;
            return true;
        };

    public ITypeShape<T> ElementType => Provider.GetTypeShape<T>();
    ITypeShape IOptionalTypeShape.ElementType => ElementType;
}
