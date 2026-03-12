namespace PolyType.ReflectionProvider;

internal static class CSharpUnionValueAccessorHelper<TUnion>
{
    public static Func<TUnion, object?> Create(Func<object, object?> untypedAccessor)
    {
        return union => union is not null ? untypedAccessor(union) : null;
    }
}
