using PolyType.Abstractions;
using PolyType.SourceGenModel;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionUnionCaseShape<TUnionCase, TUnion>(IUnionTypeShape unionType, DerivedTypeInfo derivedTypeInfo, ReflectionTypeShapeProvider provider) : IUnionCaseShape<TUnionCase, TUnion>
    where TUnionCase : TUnion
{
    public ITypeShape<TUnionCase> UnionCaseType => _type ?? CommonHelpers.ExchangeIfNull(ref _type, typeof(TUnionCase) == typeof(TUnion) ? (ITypeShape<TUnionCase>)unionType.BaseType : provider.GetTypeShape<TUnionCase>());
    private ITypeShape<TUnionCase>? _type;

    public IMarshaler<TUnionCase, TUnion> Marshaler => SubtypeMarshaler<TUnionCase, TUnion>.Instance;

    public string Name => derivedTypeInfo.Name;
    public int Tag => derivedTypeInfo.Tag;
    public bool IsTagSpecified => derivedTypeInfo.IsTagSpecified;
    public int Index => derivedTypeInfo.Index;

    ITypeShape IUnionCaseShape.UnionCaseType => UnionCaseType;

    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnionCase(this, state);
}