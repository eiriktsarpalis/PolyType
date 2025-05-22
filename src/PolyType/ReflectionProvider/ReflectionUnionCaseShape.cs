using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionUnionCaseShape<TUnionCase, TUnion>(IUnionTypeShape unionType, DerivedTypeInfo derivedTypeInfo, ReflectionTypeShapeProvider provider) : IUnionCaseShape<TUnionCase, TUnion>
    where TUnionCase : TUnion
{
    public override ITypeShape<TUnionCase> Type => _type ??= typeof(TUnionCase) == typeof(TUnion) ? (ITypeShape<TUnionCase>)unionType.BaseType : provider.GetShape<TUnionCase>();
    private ITypeShape<TUnionCase>? _type;

    public override string Name => derivedTypeInfo.Name;
    public override int Tag => derivedTypeInfo.Tag;
    public override bool IsTagSpecified => derivedTypeInfo.IsTagSpecified;
    public override int Index => derivedTypeInfo.Index;
}