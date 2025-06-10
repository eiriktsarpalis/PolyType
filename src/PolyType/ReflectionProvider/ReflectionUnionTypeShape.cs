using PolyType.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionUnionTypeShape<TUnion>(DerivedTypeInfo[] derivedTypeInfos, ReflectionTypeShapeProvider provider)
    : ReflectionTypeShape<TUnion>(provider), IUnionTypeShape<TUnion>
{
    private readonly object _syncObject = new();

    public override TypeShapeKind Kind => TypeShapeKind.Union;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnion(this, state);

    public ITypeShape<TUnion> BaseType
    {
        get
        {
            if (_baseType is null)
            {
                lock (_syncObject)
                {
                    return _baseType ??= (ITypeShape<TUnion>)Provider.CreateTypeShapeCore(typeof(TUnion), allowUnionShapes: false);
                }
            }

            return _baseType;
        }
    }

    private ITypeShape<TUnion>? _baseType;

    public IReadOnlyList<IUnionCaseShape> UnionCases
    {
        get
        {
            if (_unionCases is null)
            {
                lock (_syncObject)
                {
                    return _unionCases ??= CreateUnionCaseShapes().AsReadOnlyList();
                }
            }

            return _unionCases;
        }
    }

    private IReadOnlyList<IUnionCaseShape>? _unionCases;

    public Getter<TUnion, int> GetGetUnionCaseIndex()
    {
        if (_unionCaseIndexReader is null)
        {
            lock (_syncObject)
            {
                return _unionCaseIndexReader ??= Provider.MemberAccessor.CreateGetUnionCaseIndex<TUnion>(derivedTypeInfos);
            }
        }

        return _unionCaseIndexReader;
    }

    private Getter<TUnion, int>? _unionCaseIndexReader;

    ITypeShape IUnionTypeShape.BaseType => BaseType;

    private IEnumerable<IUnionCaseShape> CreateUnionCaseShapes()
    {
        foreach (DerivedTypeInfo derivedTypeInfo in derivedTypeInfos)
        {
            yield return Provider.CreateUnionCaseShape(this, derivedTypeInfo);
        }
    }
}

internal sealed record DerivedTypeInfo(Type Type, string Name, int Tag, int Index, bool IsTagSpecified);