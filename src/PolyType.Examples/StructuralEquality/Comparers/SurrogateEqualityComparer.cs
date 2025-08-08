namespace PolyType.Examples.StructuralEquality.Comparers;

internal sealed class SurrogateEqualityComparer<T, TSurrogate>(
    IEqualityComparer<TSurrogate> surrogateComparer,
    IMarshaler<T, TSurrogate> mapper) : EqualityComparer<T>
{
    public override bool Equals(T? x, T? y) => surrogateComparer.Equals(mapper.Marshal(x)!, mapper.Marshal(y)!);
    public override int GetHashCode(T obj) => surrogateComparer.GetHashCode(mapper.Marshal(obj)!);
}