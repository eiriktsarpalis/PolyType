using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionEnumTypeShape<TEnum, TUnderlying>(ReflectionTypeShapeProvider provider) : IEnumTypeShape<TEnum, TUnderlying>(provider)
    where TEnum : struct, Enum
{
    /// <inheritdoc/>
    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => null;
}
