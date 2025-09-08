using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionSurrogateTypeShape<T, TSurrogate>(
    IMarshaler<T, TSurrogate> marshaler,
    ReflectionTypeShapeProvider provider,
    ReflectionTypeShapeOptions options)
    : ReflectionTypeShape<T>(provider, options), ISurrogateTypeShape<T, TSurrogate>
{
    public override TypeShapeKind Kind => TypeShapeKind.Surrogate;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitSurrogate(this, state);
    public IMarshaler<T, TSurrogate> Marshaler => marshaler;
    public ITypeShape<TSurrogate> SurrogateType => Provider.GetTypeShape<TSurrogate>();
    ITypeShape ISurrogateTypeShape.SurrogateType => SurrogateType;
}