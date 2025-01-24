namespace PolyType.Examples.StructuralEquality.Comparers;

internal sealed class SurrogateEqualityComparer<T, TSurrogate>(
    IEqualityComparer<TSurrogate> surrogateComparer,
    IMarshaller<T, TSurrogate> mapper) : EqualityComparer<T>
{
    public override bool Equals(T? x, T? y) => surrogateComparer.Equals(mapper.ToSurrogate(x)!, mapper.ToSurrogate(y)!);
    public override int GetHashCode(T obj) => surrogateComparer.GetHashCode(mapper.ToSurrogate(obj)!);
}