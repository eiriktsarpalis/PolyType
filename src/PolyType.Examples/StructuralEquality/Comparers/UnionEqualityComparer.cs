using PolyType.Abstractions;

namespace PolyType.Examples.StructuralEquality.Comparers;

internal sealed class UnionEqualityComparer<TUnion>(
    Getter<TUnion, int> getUnionCaseIndex,
    EqualityComparer<TUnion> baseComparer,
    EqualityComparer<TUnion>[] unionCaseComparers) : EqualityComparer<TUnion>
{
    public override bool Equals(TUnion? x, TUnion? y)
    {
        if (x is null || y is null)
        {
            return x is null && y is null;
        }

        int xIndex = getUnionCaseIndex(ref x);
        int yIndex = getUnionCaseIndex(ref y);
        if (xIndex != yIndex)
        {
            return false;
        }

        EqualityComparer<TUnion> unionCaseComparer = xIndex < 0 ? baseComparer : unionCaseComparers[xIndex];
        return unionCaseComparer.Equals(x, y);
    }

    public override int GetHashCode(TUnion obj)
    {
        int index = getUnionCaseIndex(ref obj);
        EqualityComparer<TUnion> unionCaseComparer = index < 0 ? baseComparer : unionCaseComparers[index];
        return unionCaseComparer.GetHashCode(obj!);
    }
}

internal sealed class UnionCaseEqualityComparer<TUnionCase, TUnion>(EqualityComparer<TUnionCase> underlying) : EqualityComparer<TUnion>
    where TUnionCase : TUnion
{
    public override bool Equals(TUnion? x, TUnion? y) => underlying.Equals((TUnionCase)x!, (TUnionCase)y!);
    public override int GetHashCode(TUnion obj) => underlying.GetHashCode((TUnionCase)obj!);
}