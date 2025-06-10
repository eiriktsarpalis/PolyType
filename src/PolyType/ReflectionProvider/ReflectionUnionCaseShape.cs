using PolyType.Abstractions;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionUnionCaseShape<TUnionCase, TUnion>(IUnionTypeShape unionType, DerivedTypeInfo derivedTypeInfo, ReflectionTypeShapeProvider provider) : IUnionCaseShape<TUnionCase, TUnion>
    where TUnionCase : TUnion
{
    private readonly object _syncObject = new();
    private ITypeShape<TUnionCase>? _type;

    public ITypeShape<TUnionCase> Type
    {
        get
        {
            if (_type is null)
            {
                lock (_syncObject)
                {
                    if (_type is null)
                    {
                        _type = typeof(TUnionCase) == typeof(TUnion) ? (ITypeShape<TUnionCase>)unionType.BaseType : provider.GetShape<TUnionCase>();
                    }
                }
            }

            return _type;
        }
    }

    public string Name => derivedTypeInfo.Name;
    public int Tag => derivedTypeInfo.Tag;
    public bool IsTagSpecified => derivedTypeInfo.IsTagSpecified;
    public int Index => derivedTypeInfo.Index;

    ITypeShape IUnionCaseShape.Type => Type;

    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnionCase(this, state);
}