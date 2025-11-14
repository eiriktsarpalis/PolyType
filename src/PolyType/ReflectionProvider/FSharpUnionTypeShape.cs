using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.UnionTypeShapeDebugView))]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class FSharpUnionTypeShape<TUnion>(FSharpUnionInfo unionInfo, ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionTypeShape<TUnion>(provider, options), IUnionTypeShape<TUnion>
{
    public override TypeShapeKind Kind => TypeShapeKind.Union;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnion(this, state);
    public ITypeShape<TUnion> BaseType { get; } = new FSharpUnionCaseTypeShape<TUnion>(null, provider, options);

    public IReadOnlyList<IUnionCaseShape> UnionCases => _unionCases ?? CommonHelpers.ExchangeIfNull(ref _unionCases, CreateUnionCaseShapes().AsReadOnlyList());
    private IReadOnlyList<IUnionCaseShape>? _unionCases;

    public Getter<TUnion, int> GetGetUnionCaseIndex() => _unionCaseIndexReader ?? CommonHelpers.ExchangeIfNull(ref _unionCaseIndexReader, CreateUnionCaseIndex());
    private Getter<TUnion, int>? _unionCaseIndexReader;

    ITypeShape IUnionTypeShape.BaseType => BaseType;

    private Getter<TUnion, int> CreateUnionCaseIndex()
    {
        switch (unionInfo.TagReader)
        {
            case MethodInfo tagReaderMethod:
                var func = tagReaderMethod.CreateDelegate<Func<TUnion, int>>();
                return (ref TUnion value) => func(value);

            case PropertyInfo tagReaderProperty:
                return Provider.MemberAccessor.CreateGetter<TUnion, int>(tagReaderProperty, null);

            default:
                Debug.Fail("Invalid tag reader member");
                throw new InvalidOperationException();
        }
    }

    private IEnumerable<IUnionCaseShape> CreateUnionCaseShapes()
    {
        foreach (FSharpUnionCaseInfo unionCaseInfo in unionInfo.UnionCases)
        {
            Type fsharpUnionCaseTy = typeof(FSharpUnionCaseShape<,>).MakeGenericType(unionCaseInfo.DeclaringType, typeof(TUnion));
            yield return (IUnionCaseShape)Activator.CreateInstance(fsharpUnionCaseTy, unionCaseInfo, Provider, Options)!;
        }
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class FSharpUnionCaseShape<TUnionCase, TUnion>(FSharpUnionCaseInfo unionCaseInfo, ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : IUnionCaseShape<TUnionCase, TUnion>
    where TUnionCase : TUnion
{
    public ITypeShape<TUnionCase> UnionCaseType { get; } = new FSharpUnionCaseTypeShape<TUnionCase>(unionCaseInfo, provider, options);
    public IMarshaler<TUnionCase, TUnion> Marshaler => SubtypeMarshaler<TUnionCase, TUnion>.Instance;
    public string Name => unionCaseInfo.Name;
    public int Tag => unionCaseInfo.Tag;
    public bool IsTagSpecified => false; // F# tags are inferred from the union case ordering
    public int Index => unionCaseInfo.Tag;
    ITypeShape IUnionCaseShape.UnionCaseType => UnionCaseType;
    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitUnionCase(this, state);
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class FSharpUnionCaseTypeShape<TUnionCase>(FSharpUnionCaseInfo? unionCaseInfo, ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionObjectTypeShape<TUnionCase>(provider, options)
{
    protected override IConstructorShape? GetConstructor()
    {
        if (unionCaseInfo is null)
        {
            return null;
        }

        NullabilityInfoContext? nullabilityCtx = ReflectionTypeShapeProvider.CreateNullabilityInfoContext();
        MethodParameterShapeInfo[] methodParameterShapes = unionCaseInfo.Constructor.GetParameters()
            .Select((p, i) => new MethodParameterShapeInfo(p, isNonNullable: p.IsNonNullableAnnotation(nullabilityCtx), logicalName: unionCaseInfo.Properties[i].Name))
            .ToArray();

        MethodShapeInfo constructorShapeInfo = new(typeof(TUnionCase), unionCaseInfo.Constructor, methodParameterShapes);
        return Provider.CreateConstructor(this, constructorShapeInfo);
    }

    protected override IEnumerable<IPropertyShape> GetProperties()
    {
        if (unionCaseInfo is null)
        {
            yield break;
        }

        int i = 0;
        NullabilityInfoContext? nullabilityCtx = ReflectionTypeShapeProvider.CreateNullabilityInfoContext();
        foreach (PropertyInfo property in unionCaseInfo.Properties)
        {
            property.ResolveNullableAnnotation(nullabilityCtx, out bool isGetterNonNullable, out _);
            PropertyShapeInfo propertyShapeInfo = new(typeof(TUnionCase), property, DerivedMemberInfo: property, IsGetterNonNullable: isGetterNonNullable);
            yield return Provider.CreateProperty(this, propertyShapeInfo, position: i++);
        }
    }
}
