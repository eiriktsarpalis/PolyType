using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.UnionCaseShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class CSharpUnionCaseShape<TUnionCase, TUnion>(CSharpUnionTypeShape<TUnion> unionShape, CSharpUnionCaseInfo caseInfo)
    : IUnionCaseShape<TUnionCase, TUnion>
{
    public ITypeShape<TUnionCase> UnionCaseType => field ?? CommonHelpers.ExchangeIfNull(ref field, unionShape.Provider.GetTypeShape<TUnionCase>());

    public IMarshaler<TUnionCase, TUnion> Marshaler => field ?? CommonHelpers.ExchangeIfNull(
        ref field,
        CreateMarshaler());

    public string Name => caseInfo.Name;
    public int Tag => caseInfo.Index;
    public bool IsTagSpecified => false;
    public bool IsNullable => caseInfo.IsNullable;
    public int Index => caseInfo.Index;

    ITypeShape IUnionCaseShape.UnionCaseType => UnionCaseType;
    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnionCase(this, state);

    private IMarshaler<TUnionCase, TUnion> CreateMarshaler()
    {
        var constructor = unionShape.Provider.MemberAccessor.CreateCSharpUnionCaseConstructor<TUnionCase, TUnion>(caseInfo.CreationMember);
        var valueGetter = unionShape.GetValueGetter();
        IMarshaler<TUnionCase, TUnion> marshaler = caseInfo.IsNullable
            ? new NullableCSharpUnionCaseMarshaler<TUnionCase, TUnion>(constructor, valueGetter)
            : new CSharpUnionCaseMarshaler<TUnionCase, TUnion>(constructor, valueGetter);
        return caseInfo.TryGetValue is null
            ? marshaler
            : new TryGetValueCSharpUnionCaseMarshaler<TUnionCase, TUnion>(
                marshaler, unionShape.Provider.MemberAccessor.CreateCSharpUnionCaseGetter<TUnionCase, TUnion>(caseInfo.TryGetValue));
    }

    private string DebuggerDisplay => $"{{Name = \"{Name}\", CaseType = \"{typeof(TUnionCase)}\"}}";
}

internal sealed class TryGetValueCSharpUnionCaseMarshaler<TUnionCase, TUnion>(
    IMarshaler<TUnionCase, TUnion> fallback, OptionDeconstructor<TUnion, TUnionCase> tryGetValue)
    : IMarshaler<TUnionCase, TUnion>
{
    public TUnion? Marshal(TUnionCase? value) => fallback.Marshal(value);

    public TUnionCase? Unmarshal(TUnion? value)
    {
        if (value is not null && tryGetValue(value, out TUnionCase? caseValue))
        {
            return caseValue;
        }

        return fallback.Unmarshal(value);
    }
}

internal class CSharpUnionCaseMarshaler<TUnionCase, TUnion>(Func<TUnionCase?, TUnion?> constructor, Getter<TUnion, object?> valueGetter)
    : IMarshaler<TUnionCase, TUnion>
{
    public TUnion? Marshal(TUnionCase? value) => constructor(value);

    public virtual TUnionCase? Unmarshal(TUnion? value)
    {
        object? payload = GetPayload(value);
        if (payload is TUnionCase caseValue)
        {
            return caseValue;
        }

        return Throw();
        static TUnionCase? Throw() => throw new ArgumentException("The union value does not match this case.", nameof(value));
    }

    protected object? GetPayload(TUnion? value) => value is null ? null : valueGetter(ref value!);
}

internal sealed class NullableCSharpUnionCaseMarshaler<TUnionCase, TUnion>(Func<TUnionCase?, TUnion?> constructor, Getter<TUnion, object?> valueGetter)
    : CSharpUnionCaseMarshaler<TUnionCase, TUnion>(constructor, valueGetter)
{
    public override TUnionCase? Unmarshal(TUnion? value)
    {
        object? payload = GetPayload(value);
        if (payload is null)
        {
            return default;
        }

        if (payload is TUnionCase caseValue)
        {
            return caseValue;
        }

        return Throw();
        static TUnionCase? Throw() => throw new ArgumentException("The union value does not match this case.", nameof(value));
    }
}
