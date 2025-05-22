using PolyType.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionUnionTypeShape<TUnion>(DerivedTypeInfo[] derivedTypeInfos, ReflectionTypeShapeProvider provider)
    : IUnionTypeShape<TUnion>(provider)
{
    public override ITypeShape<TUnion> BaseType => _baseType ??= (ITypeShape<TUnion>)provider.CreateTypeShapeCore(typeof(TUnion), allowUnionShapes: false);
    private ITypeShape<TUnion>? _baseType;

    public override IReadOnlyList<IUnionCaseShape> UnionCases => _unionCases ??= CreateUnionCaseShapes().AsReadOnlyList();
    private IReadOnlyList<IUnionCaseShape>? _unionCases;

    public override Getter<TUnion, int> GetGetUnionCaseIndex() => _unionCaseIndexReader ??= provider.MemberAccessor.CreateGetUnionCaseIndex<TUnion>(derivedTypeInfos);
    private Getter<TUnion, int>? _unionCaseIndexReader;

    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, associatedType);

    private IEnumerable<IUnionCaseShape> CreateUnionCaseShapes()
    {
        foreach (DerivedTypeInfo derivedTypeInfo in derivedTypeInfos)
        {
            yield return provider.CreateUnionCaseShape(this, derivedTypeInfo);
        }
    }
}

internal sealed record DerivedTypeInfo(Type Type, string Name, int Tag, int Index, bool IsTagSpecified);