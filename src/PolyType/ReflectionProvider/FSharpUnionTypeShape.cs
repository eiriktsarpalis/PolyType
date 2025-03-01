using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class FSharpUnionTypeShape<TUnion>(FSharpUnionInfo unionInfo, ReflectionTypeShapeProvider provider)
    : ReflectionTypeShape<TUnion>(provider), IUnionTypeShape<TUnion>
{
    public override TypeShapeKind Kind => TypeShapeKind.Union;
    public override object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitUnion(this, state);
    public ITypeShape<TUnion> BaseType { get; } = new FSharpUnionCaseTypeShape<TUnion>(null, provider);

    public IReadOnlyList<IUnionCaseShape> UnionCases => _unionCases ??= CreateUnionCaseShapes().AsReadOnlyList();
    private IReadOnlyList<IUnionCaseShape>? _unionCases;

    public Getter<TUnion, int> GetGetUnionCaseIndex() => _unionCaseIndexReader ??= CreateUnionCaseIndex();
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
            yield return (IUnionCaseShape)Activator.CreateInstance(fsharpUnionCaseTy, unionCaseInfo, Provider)!;
        }
    }
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class FSharpUnionCaseShape<TUnionCase, TUnion>(FSharpUnionCaseInfo unionCaseInfo, ReflectionTypeShapeProvider provider)
    : IUnionCaseShape<TUnionCase, TUnion>
    where TUnionCase : TUnion
{
    public ITypeShape<TUnionCase> Type { get; } = new FSharpUnionCaseTypeShape<TUnionCase>(unionCaseInfo, provider);
    public string Name => unionCaseInfo.Name;
    public int Tag => unionCaseInfo.Tag;
    public int Index => unionCaseInfo.Tag;
    ITypeShape IUnionCaseShape.Type => Type;
    public object? Accept(ITypeShapeVisitor visitor, object? state = null) => visitor.VisitUnionCase(this, state);
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class FSharpUnionCaseTypeShape<TUnionCase>(FSharpUnionCaseInfo? unionCaseInfo, ReflectionTypeShapeProvider provider)
    : ReflectionObjectTypeShape<TUnionCase>(provider)
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

        MethodConstructorShapeInfo constructorShapeInfo = new(typeof(TUnionCase), unionCaseInfo.Constructor, methodParameterShapes);
        return Provider.CreateConstructor(this, constructorShapeInfo);
    }

    protected override IEnumerable<IPropertyShape> GetProperties()
    {
        if (unionCaseInfo is null)
        {
            yield break;
        }

        NullabilityInfoContext? nullabilityCtx = ReflectionTypeShapeProvider.CreateNullabilityInfoContext();
        foreach (PropertyInfo property in unionCaseInfo.Properties)
        {
            property.ResolveNullableAnnotation(nullabilityCtx, out bool isGetterNonNullable, out _);
            PropertyShapeInfo propertyShapeInfo = new(typeof(TUnionCase), property, AttributeProvider: property, IsGetterNonNullable: isGetterNonNullable);
            yield return Provider.CreateProperty(this, propertyShapeInfo);
        }
    }
}
