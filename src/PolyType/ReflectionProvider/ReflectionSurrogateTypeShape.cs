using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionSurrogateTypeShape<T, TSurrogate>(
    IMarshaller<T, TSurrogate> marshaller,
    ReflectionTypeShapeProvider provider,
    ReflectionTypeShapeOptions options)
    : ReflectionTypeShape<T>(provider, options), ISurrogateTypeShape<T, TSurrogate>
{
    public override TypeShapeKind Kind => TypeShapeKind.Surrogate;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitSurrogate(this, state);
    public IMarshaller<T, TSurrogate> Marshaller => marshaller;
    public ITypeShape<TSurrogate> SurrogateType => Provider.GetShape<TSurrogate>();
    ITypeShape ISurrogateTypeShape.SurrogateType => SurrogateType;
}