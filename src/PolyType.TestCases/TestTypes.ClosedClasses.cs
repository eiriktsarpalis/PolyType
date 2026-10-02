using System.Runtime.Serialization;
using System.Runtime.CompilerServices;

[assembly: PolyType.TypeShapeExtension(typeof(PolyType.Tests.ClosedExtensionRoot), InferClosedTypePolymorphism = true)]

namespace PolyType.Tests;

[GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
public closed partial class ClosedAnimal
{
    public string? Name { get; set; }
}

public sealed class ClosedZebra : ClosedAnimal
{
    public int Stripes { get; set; }
}

[GenerateShape, TypeShape(InferClosedTypePolymorphism = false)]
[DerivedTypeShape(typeof(ClosedLabrador), Name = "independent", Tag = 42)]
public closed partial class ClosedDog : ClosedAnimal
{
    public bool GoodBoy { get; set; }
}

public sealed class ClosedCollie : ClosedDog
{
    public bool Herding { get; set; }
}

public sealed class ClosedLabrador : ClosedDog
{
    public string? Color { get; set; }
}

public static class ClosedAnimalContainer
{
    public sealed class Antelope : ClosedAnimal
    {
        public int HornLength { get; set; }
    }
}

[GenerateShape, TypeShape(InferClosedTypePolymorphism = true, Kind = TypeShapeKind.Union)]
public closed partial class ClosedEmptyRoot;

[GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
public closed partial class ClosedEmptyBranchesRoot;

public closed class ClosedEmptyMiddle : ClosedEmptyBranchesRoot;

[GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
public closed partial class ClosedBoundaryRoot;

public abstract class ClosedOpenBranch : ClosedBoundaryRoot
{
    public int Number { get; set; }
}

public sealed class ClosedOpenLeaf : ClosedOpenBranch;

[TypeShape(InferClosedTypePolymorphism = true)]
public closed class ClosedGenericRoot<TLeft, TRight>
{
    public TLeft Left { get; set; } = default!;
    public TRight Right { get; set; } = default!;
}

public sealed class ClosedReorderedLeaf<TRight, TLeft> : ClosedGenericRoot<TLeft, TRight>
{
    public int Marker { get; set; }
}

public closed class ClosedGenericBranch<TLeft, TRight> : ClosedGenericRoot<TLeft, TRight>;

public sealed class ClosedWrappedLeaf<TRight, TLeft> : ClosedGenericBranch<List<TLeft>, TRight[]>
{
    public string? Description { get; set; }
}

[GenerateShape, TypeShape]
public closed partial class ClosedExtensionRoot
{
    public int Number { get; set; }
}

public sealed class ClosedExtensionZebra : ClosedExtensionRoot;
public sealed class ClosedExtensionAntelope : ClosedExtensionRoot;

#pragma warning disable PT0030 // These fixtures intentionally combine explicit registrations with inference.
[GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
[DerivedTypeShape(typeof(ClosedExplicitLeaf), Name = "selected", Tag = 42)]
[KnownType(typeof(ClosedExplicitIgnoredLeaf))]
public closed partial class ClosedExplicitRoot
{
    public int Number { get; set; }
}

public sealed class ClosedExplicitLeaf : ClosedExplicitRoot;
public sealed class ClosedExplicitIgnoredLeaf : ClosedExplicitRoot;

[GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
[KnownType(typeof(ClosedKnownLeaf))]
public closed partial class ClosedKnownRoot
{
    public int Number { get; set; }
}

public sealed class ClosedKnownLeaf : ClosedKnownRoot;
public sealed class ClosedKnownIgnoredLeaf : ClosedKnownRoot;
#pragma warning restore PT0030

[GenerateShape, TypeShape(InferClosedTypePolymorphism = false)]
public closed partial class ClosedDisabledRoot
{
    public int Number { get; set; }
}

public sealed class ClosedDisabledLeaf : ClosedDisabledRoot;

[Union, GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
public closed partial class ClosedUnionHierarchy : ClosedUnionHierarchy.IUnionMembers
{
    protected ClosedUnionHierarchy(int number) => Number = number;

    public int Number { get; }

    object IUnionMembers.Value => Number;

    public interface IUnionMembers
    {
        public static ClosedUnionHierarchy Create(int number) => new ClosedUnionLeaf(number);
        object Value { get; }
    }
}

public sealed class ClosedUnionLeaf(int number) : ClosedUnionHierarchy(number);

[Union, GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
public closed partial class ClosedEmptyUnionHierarchy : ClosedEmptyUnionHierarchy.IUnionMembers
{
    object IUnionMembers.Value => 0;

    public interface IUnionMembers
    {
        public static ClosedEmptyUnionHierarchy Create(int number) => throw new NotSupportedException();
        object Value { get; }
    }
}
