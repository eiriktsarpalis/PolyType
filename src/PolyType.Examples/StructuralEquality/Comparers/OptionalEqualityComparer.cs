using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Examples.StructuralEquality.Comparers;

internal sealed class OptionalEqualityComparer<TOptional, TElement> : EqualityComparer<TOptional>
{
    public required IEqualityComparer<TElement> ElementComparer { get; init; }
    public required OptionDeconstructor<TOptional, TElement> Deconstructor { get; init; }

    public override bool Equals(TOptional? x, TOptional? y)
    {
        bool leftIsSome = Deconstructor(x, out TElement? xElement);
        bool rightIsSome = Deconstructor(y, out TElement? yElement);
        if (!leftIsSome || !rightIsSome)
        {
            return leftIsSome == rightIsSome;
        }

        return ElementComparer.Equals(xElement!, yElement!);
    }

    public override int GetHashCode(TOptional obj)
    {
        return Deconstructor(obj, out TElement? element) ? ElementComparer.GetHashCode(element!) : 0;
    }
}