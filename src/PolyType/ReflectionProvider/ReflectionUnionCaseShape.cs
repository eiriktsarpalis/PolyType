using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionUnionCaseShape<TUnionCase, TUnion>(IUnionTypeShape unionType, DerivedTypeInfo derivedTypeInfo, ReflectionTypeShapeProvider provider) : IUnionCaseShape<TUnionCase, TUnion>
    where TUnionCase : TUnion
{
    public ITypeShape<TUnionCase> Type => _type ?? CommonHelpers.ExchangeIfNull(ref _type, typeof(TUnionCase) == typeof(TUnion) ? (ITypeShape<TUnionCase>)unionType.BaseType : provider.GetShape<TUnionCase>());
    private ITypeShape<TUnionCase>? _type;

    public string Name => derivedTypeInfo.Name;
    public int Tag => derivedTypeInfo.Tag;
    public bool IsTagSpecified => derivedTypeInfo.IsTagSpecified;
    public int Index => derivedTypeInfo.Index;

    ITypeShape IUnionCaseShape.Type => Type;

    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnionCase(this, state);
}