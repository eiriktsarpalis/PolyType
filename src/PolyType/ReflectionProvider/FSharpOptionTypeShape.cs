using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal sealed class FSharpOptionTypeShape<TOptional, TElement>(FSharpUnionInfo unionInfo, ReflectionTypeShapeProvider provider)
    : IOptionalTypeShape<TOptional, TElement>(provider)
    where TOptional : IEquatable<TOptional>
{
    internal new ReflectionTypeShapeProvider Provider => (ReflectionTypeShapeProvider)base.Provider;

    public override Func<TOptional> GetNoneConstructor() => _noneConstructor ??= unionInfo.UnionCases[0].Constructor.CreateDelegate<Func<TOptional>>();
    private Func<TOptional>? _noneConstructor;

    public override Func<TElement, TOptional> GetSomeConstructor() => _someConstructor ??= unionInfo.UnionCases[1].Constructor.CreateDelegate<Func<TElement, TOptional>>();
    private Func<TElement, TOptional>? _someConstructor;

    public override OptionDeconstructor<TOptional, TElement> GetDeconstructor()
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

    public override ITypeShape? GetAssociatedTypeShape(Type associatedType) => InternalTypeShapeExtensions.GetAssociatedTypeShape(this, associatedType);
}