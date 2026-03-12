using PolyType.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.UnionTypeShapeDebugView))]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class CSharpUnionTypeShape<TUnion>(CSharpUnionCaseInfo[] caseInfos, Func<TUnion, object?> valueAccessor, ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionTypeShape<TUnion>(provider, options), IUnionTypeShape<TUnion>
{
    public override TypeShapeKind Kind => TypeShapeKind.Union;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnion(this, state);
    public UnionKind UnionKind => UnionKind.CSharpUnion;

    public ITypeShape<TUnion> BaseType => _baseType ?? CommonHelpers.ExchangeIfNull(ref _baseType, (ITypeShape<TUnion>)Provider.CreateTypeShapeCore(typeof(TUnion), allowUnionShapes: false));
    private ITypeShape<TUnion>? _baseType;

    public IReadOnlyList<IUnionCaseShape> UnionCases => _unionCases ?? CommonHelpers.ExchangeIfNull(ref _unionCases, CreateUnionCaseShapes().AsReadOnlyList());
    private IReadOnlyList<IUnionCaseShape>? _unionCases;

    public Getter<TUnion, int> GetGetUnionCaseIndex()
    {
        return _unionCaseIndexReader ??= CommonHelpers.ExchangeIfNull(ref _unionCaseIndexReader, CreateUnionCaseIndexGetter());
    }

    private Getter<TUnion, int>? _unionCaseIndexReader;

    ITypeShape IUnionTypeShape.BaseType => BaseType;

    private IEnumerable<IUnionCaseShape> CreateUnionCaseShapes()
    {
        foreach (CSharpUnionCaseInfo caseInfo in caseInfos)
        {
            yield return Provider.CreateCSharpUnionCaseShape(this, caseInfo);
        }
    }

    private Getter<TUnion, int> CreateUnionCaseIndexGetter()
    {
        var cache = new ConcurrentDictionary<Type, int>();
        foreach (CSharpUnionCaseInfo caseInfo in caseInfos)
        {
            cache.TryAdd(caseInfo.CaseType, caseInfo.Index);
        }

        // Use the IUnion.Value property accessor to determine the active case
        return (ref TUnion union) =>
        {
            if (union is null)
            {
                return -1;
            }

            object? val = valueAccessor(union);
            if (val is null)
            {
                return -1;
            }

            Type valType = val.GetType();
            if (cache.TryGetValue(valType, out int index))
            {
                return index;
            }

            return ComputeIndexForType(valType);
        };

        int ComputeIndexForType(Type type)
        {
            int foundIndex = -1;
            foreach (Type parentType in CommonHelpers.TraverseGraphWithTopologicalSort(type, GetParentTypes))
            {
                if (cache.TryGetValue(parentType, out int i))
                {
                    foundIndex = i;
                    break;
                }
            }

            cache[type] = foundIndex;

            return foundIndex;
        }

        static Type[] GetParentTypes(Type type)
        {
            var parents = new List<Type>();
            if (type.BaseType is { } baseType)
            {
                parents.Add(baseType);
            }

            foreach (Type iface in type.GetInterfaces())
            {
                parents.Add(iface);
            }

            return parents.ToArray();
        }
    }
}

internal sealed record CSharpUnionCaseInfo(Type CaseType, string Name, int Tag, int Index, bool IsTagSpecified, ConstructorInfo? MarshalConstructor, Func<object, object?> ValueAccessor);
