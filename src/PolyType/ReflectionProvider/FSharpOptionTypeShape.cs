using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class FSharpOptionTypeShape<TOptional, TElement>(FSharpUnionInfo unionInfo, ReflectionTypeShapeProvider provider)
    : ReflectionTypeShape<TOptional>(provider), IOptionalTypeShape<TOptional, TElement>
    where TOptional : IEquatable<TOptional>
{
    public override TypeShapeKind Kind => TypeShapeKind.Optional;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitOptional(this, state);

    public ITypeShape<TElement> ElementType => Provider.GetShape<TElement>();
    ITypeShape IOptionalTypeShape.ElementType => ElementType;

    public Func<TOptional> GetNoneConstructor() => _noneConstructor ?? CommonHelpers.ExchangeIfNull(ref _noneConstructor, unionInfo.UnionCases[0].Constructor.CreateDelegate<Func<TOptional>>());
    private Func<TOptional>? _noneConstructor;

    public Func<TElement, TOptional> GetSomeConstructor() => _someConstructor ?? CommonHelpers.ExchangeIfNull(ref _someConstructor, unionInfo.UnionCases[1].Constructor.CreateDelegate<Func<TElement, TOptional>>());
    private Func<TElement, TOptional>? _someConstructor;

    public OptionDeconstructor<TOptional, TElement> GetDeconstructor()
    {
        PropertyInfo valueGetterProp = unionInfo.UnionCases[1].Properties[0];
        var valueGetter = Provider.MemberAccessor.CreateGetter<TOptional, TElement>(valueGetterProp, null);
        return (TOptional? optional, [MaybeNullWhen(false)] out TElement value) =>
        {
            // 'None' in both FSharpOption<T> and FSharpValueOption<T> equals default
            if (typeof(TOptional).IsValueType ? optional!.Equals(default!) : optional is null)
            {
                value = default;
                return false;
            }

            value = valueGetter(ref optional!);
            return true;
        };
    }

}