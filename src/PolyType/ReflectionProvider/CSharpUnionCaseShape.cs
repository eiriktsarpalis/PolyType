using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.UnionCaseShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class CSharpUnionCaseShape<TUnionCase, TUnion>(
    CSharpUnionCaseInfo caseInfo,
    ReflectionTypeShapeProvider provider) : IUnionCaseShape<TUnionCase, TUnion>
{
    public ITypeShape<TUnionCase> UnionCaseType => _type ?? CommonHelpers.ExchangeIfNull(ref _type, provider.GetTypeShape<TUnionCase>());
    private ITypeShape<TUnionCase>? _type;

    public IMarshaler<TUnionCase, TUnion> Marshaler => _marshaler ?? CommonHelpers.ExchangeIfNull(ref _marshaler, CreateMarshaler());
    private IMarshaler<TUnionCase, TUnion>? _marshaler;

    public string Name => caseInfo.Name;
    public int Tag => caseInfo.Tag;
    public bool IsTagSpecified => caseInfo.IsTagSpecified;
    public int Index => caseInfo.Index;

    ITypeShape IUnionCaseShape.UnionCaseType => UnionCaseType;

    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnionCase(this, state);

    private string DebuggerDisplay => $"{{Name = \"{Name}\", CaseType = \"{typeof(TUnionCase)}\"}}";

    private DelegateMarshaler<TUnionCase, TUnion> CreateMarshaler()
    {
        // Marshal: create union value from case value via constructor
        Func<TUnionCase?, TUnion?> marshal;
        if (caseInfo.MarshalConstructor is { } ctor)
        {
            marshal = caseValue =>
            {
                object? result = ctor.Invoke([caseValue]);

                return (TUnion?)result;
            };
        }
        else
        {
            marshal = _ => default;
        }

        // Unmarshal: extract case value from union via reflection-based IUnion.Value accessor
        Func<object, object?> valueAccessor = caseInfo.ValueAccessor;
        Func<TUnion?, TUnionCase?> unmarshal = unionValue =>
        {
            if (unionValue is null)
            {
                return default;
            }

            object? val = valueAccessor(unionValue);
            if (val is TUnionCase caseVal)
            {
                return caseVal;
            }

            return default;
        };

        return new DelegateMarshaler<TUnionCase, TUnion>(marshal, unmarshal);
    }
}
