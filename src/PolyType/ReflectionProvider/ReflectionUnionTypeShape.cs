using PolyType.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionUnionTypeShape<TUnion>(DerivedTypeShapeAttribute[] derivedTypeAttributes, ReflectionTypeShapeProvider provider)
    : ReflectionTypeShape<TUnion>(provider), IUnionTypeShape<TUnion>
{
    public override TypeShapeKind Kind => TypeShapeKind.Union;
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitUnion(this, state);

    public ITypeShape<TUnion> BaseType => _baseType ??= (ITypeShape<TUnion>)Provider.CreateTypeShapeCore(typeof(TUnion), allowUnionShapes: false);
    private ITypeShape<TUnion>? _baseType;

    public IReadOnlyList<IUnionCaseShape> UnionCases => _unionCases ??= CreateUnionCaseShapes().AsReadOnlyList();
    private IReadOnlyList<IUnionCaseShape>? _unionCases;

    public Getter<TUnion, int> GetGetUnionCaseIndex() => _unionCaseIndexReader ??= Provider.MemberAccessor.CreateGetUnionCaseIndex<TUnion>(derivedTypeAttributes);
    private Getter<TUnion, int>? _unionCaseIndexReader;

    ITypeShape IUnionTypeShape.BaseType => BaseType;

    private IEnumerable<IUnionCaseShape> CreateUnionCaseShapes()
    {
        int i = 0;
        foreach (DerivedTypeShapeAttribute attribute in derivedTypeAttributes)
        {
            yield return Provider.CreateUnionCaseShape(this, attribute, i++);
        }
    }
}
