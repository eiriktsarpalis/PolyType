using PolyType.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.UnionTypeShapeDebugView))]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class CSharpUnionTypeShape<TUnion>(CSharpUnionInfo unionInfo, ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionTypeShape<TUnion>(provider, options), IUnionTypeShape<TUnion>
{
    public override TypeShapeKind Kind => TypeShapeKind.Union;
    public UnionTypeShapeKind UnionKind => UnionTypeShapeKind.CSharpUnion;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnion(this, state);
    public ITypeShape<TUnion> BaseType { get; } = new CSharpUnionBaseTypeShape<TUnion>(provider, options);

    public IReadOnlyList<IUnionCaseShape> UnionCases => field ?? CommonHelpers.ExchangeIfNull(ref field, CreateUnionCaseShapes().AsReadOnlyList());

    ITypeShape IUnionTypeShape.BaseType => BaseType;

    public Getter<TUnion, int> GetGetUnionCaseIndex() => _unionCaseIndexReader ?? CommonHelpers.ExchangeIfNull(ref _unionCaseIndexReader, CreateUnionCaseIndexReader());
    private Getter<TUnion, int>? _unionCaseIndexReader;

    internal Getter<TUnion, object?> GetValueGetter() => _valueGetter ?? CommonHelpers.ExchangeIfNull(ref _valueGetter, Provider.MemberAccessor.CreateCSharpUnionValueGetter<TUnion>(unionInfo.ValueGetter));
    private Getter<TUnion, object?>? _valueGetter;

    private IEnumerable<IUnionCaseShape> CreateUnionCaseShapes()
    {
        foreach (CSharpUnionCaseInfo caseInfo in unionInfo.Cases)
        {
            Type caseShapeType = typeof(CSharpUnionCaseShape<,>).MakeGenericType(caseInfo.Type, typeof(TUnion));
            yield return (IUnionCaseShape)ReflectionHelpers.CreateInstanceNoWrapExceptions(caseShapeType, this, caseInfo)!;
        }
    }

    private Getter<TUnion, int> CreateUnionCaseIndexReader()
    {
        Getter<TUnion, int> fallback = CreateValueCaseIndexReader();
        (Func<TUnion, bool> Matches, int Index)[] cases = SortCases(unionInfo.Cases.Where(caseInfo => caseInfo.TryGetValue is not null).ToArray(), normalizeNullable: false)
            .Select(caseInfo => (Provider.MemberAccessor.CreateCSharpUnionCaseTester<TUnion>(caseInfo.TryGetValue!), caseInfo.Index))
            .ToArray();
        if (cases.Length == 0)
        {
            return fallback;
        }

        return (ref union) =>
        {
            if (union is not null)
            {
                foreach ((Func<TUnion, bool> matches, int index) in cases)
                {
                    if (matches(union))
                    {
                        return index;
                    }
                }
            }

            // Partial TryGetValue coverage, or all overloads returning false, falls back to Value.
            return fallback(ref union);
        };
    }

    private Getter<TUnion, int> CreateValueCaseIndexReader()
    {
        Getter<TUnion, object?> valueGetter = GetValueGetter();
        int nullCaseIndex = Array.FindIndex(unionInfo.Cases, static unionCase => unionCase.IsNullable);
        ConcurrentDictionary<Type, int> indices = new();
        foreach (CSharpUnionCaseInfo caseInfo in unionInfo.Cases)
        {
            indices.TryAdd(caseInfo.Type, caseInfo.Index);
        }

        foreach (CSharpUnionCaseInfo caseInfo in unionInfo.Cases)
        {
            if (Nullable.GetUnderlyingType(caseInfo.Type) is Type underlyingType)
            {
                // Nullable<T> boxes as T, but an explicit T case takes precedence.
                indices.TryAdd(underlyingType, caseInfo.Index);
            }
        }

        (Type MatchingType, int Index)[] sortedCases = SortCases(unionInfo.Cases)
            .Select(caseInfo => (Nullable.GetUnderlyingType(caseInfo.Type) ?? caseInfo.Type, caseInfo.Index))
            .ToArray();
        Func<Type, int> resolveIndex = type =>
        {
            foreach ((Type matchingType, int index) in sortedCases)
            {
                if (matchingType.IsAssignableFrom(type))
                {
                    return index;
                }
            }

            return -1;
        };

        return (ref value) =>
        {
            object? payload = value is null ? null : valueGetter(ref value);
            int index = payload is null ? nullCaseIndex : indices.GetOrAdd(payload.GetType(), resolveIndex);
            if (index < 0)
            {
                Throw(value);
                static void Throw(TUnion value) =>
#if NET
                    throw new SwitchExpressionException(value);
#else
                    throw new InvalidOperationException("Non-exhaustive switch expression failed to match its input.");
#endif
            }

            return index;
        };
    }

    private static CSharpUnionCaseInfo[] SortCases(CSharpUnionCaseInfo[] cases, bool normalizeNullable = true)
    {
        if (cases.Length < 2)
        {
            return cases;
        }

        // A payload can match multiple declared cases. Try more-specific types first
        // so an earlier base class or interface case cannot shadow a derived case.
        Type[] matchingTypes = cases.Select(unionCase => normalizeNullable ? Nullable.GetUnderlyingType(unionCase.Type) ?? unionCase.Type : unionCase.Type).ToArray();
        int[] indices = Enumerable.Range(0, cases.Length).ToArray();
        return CommonHelpers.TraverseGraphWithTopologicalSort(-1, GetSubtypes)
            .Where(index => index >= 0)
            .Reverse()
            .Select(index => cases[index])
            .ToArray();

        IReadOnlyCollection<int> GetSubtypes(int index) => index < 0
            ? indices
            : indices.Where(other =>
                matchingTypes[index].IsAssignableFrom(matchingTypes[other]) &&
                !matchingTypes[other].IsAssignableFrom(matchingTypes[index])).ToArray();
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class CSharpUnionBaseTypeShape<TUnion>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionObjectTypeShape<TUnion>(provider, options)
{
    public override bool IsContextual => true;
    protected override IConstructorShape? GetConstructor() => null;
    protected override IEnumerable<IPropertyShape> GetProperties() => [];
}
