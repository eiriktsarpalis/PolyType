﻿using System.Diagnostics.CodeAnalysis;

namespace PolyType.Examples.StructuralEquality.Comparers;

internal sealed class NullableEqualityComparer<T> : EqualityComparer<T?>
    where T : struct
{
    public required IEqualityComparer<T> ElementComparer { get; init; }

    public override bool Equals(T? x, T? y)
    {
        if (x is null || y is null)
        {
            return x is null && y is null;
        }

        return ElementComparer.Equals(x.Value, y.Value);
    }

    public override int GetHashCode(T? obj)
    {
        return obj.HasValue ? ElementComparer.GetHashCode(obj.Value) : 0;
    }
}