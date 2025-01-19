using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionSurrogateTypeShape<T, TSurrogate>(
    IMarshaller<T, TSurrogate> marshaller,
    ReflectionTypeShapeProvider provider)
    : ReflectionTypeShape<T>(provider), ISurrogateTypeShape<T, TSurrogate>
{
    public override TypeShapeKind Kind => TypeShapeKind.Surrogate;
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitSurrogate(this, state);
    public IMarshaller<T, TSurrogate> Marshaller => marshaller;
    public ITypeShape<TSurrogate> SurrogateType => Provider.GetShape<TSurrogate>();
    ITypeShape ISurrogateTypeShape.SurrogateType => SurrogateType;
}