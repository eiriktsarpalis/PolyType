using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionNullableTypeShape<T>(ReflectionTypeShapeProvider provider) : IOptionalTypeShape<T?, T>(provider)
    where T : struct
{
    public override Func<T?> GetNoneConstructor() => static () => null;

    public override Func<T, T?> GetSomeConstructor() => static t => t;

    public override OptionDeconstructor<T?, T> GetDeconstructor() =>
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

    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, associatedType);
}
