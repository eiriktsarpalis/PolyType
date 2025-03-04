using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionUnionCaseShape<TUnionCase, TUnion>(IUnionTypeShape unionType, string name, int tag, bool isTagSpecified, int index, ReflectionTypeShapeProvider provider) : IUnionCaseShape<TUnionCase, TUnion>
    where TUnionCase : TUnion
{
    public ITypeShape<TUnionCase> Type => _type ??= typeof(TUnionCase) == typeof(TUnion) ? (ITypeShape<TUnionCase>)unionType.BaseType : provider.GetShape<TUnionCase>();
    private ITypeShape<TUnionCase>? _type;

    public string Name { get; } = name;
    public int Tag { get; } = tag;
    public bool IsTagSpecified { get; } = isTagSpecified;
    public int Index { get; } = index;

    ITypeShape IUnionCaseShape.Type => Type;

    public object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitUnionCase(this, state);
}