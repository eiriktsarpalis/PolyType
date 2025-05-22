using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionSurrogateTypeShape<T, TSurrogate>(
    IMarshaller<T, TSurrogate> marshaller,
    ReflectionTypeShapeProvider provider)
    : ISurrogateTypeShape<T, TSurrogate>(provider)
{
    public override IMarshaller<T, TSurrogate> Marshaller => marshaller;

    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, associatedType);
}