using PolyType.Abstractions;
using PolyType.ReflectionProvider;
using PolyType.Tests;

namespace PolyType.Tests;

/// <summary>
/// Tests for C# union types, closed enums, and closed class hierarchies.
/// These features are prototypes based on upcoming C# language proposals.
/// </summary>
public class CSharpUnionTests
{
    private static readonly ReflectionTypeShapeProvider s_reflectionProvider =
        ReflectionTypeShapeProvider.Create(new ReflectionTypeShapeProviderOptions
        {
            UseReflectionEmit = true,
        });

    [Fact]
    public void Pet_IsDetectedAsUnion()
    {
        ITypeShape shape = s_reflectionProvider.GetTypeShapeOrThrow(typeof(Pet));

        Assert.Equal(TypeShapeKind.Union, shape.Kind);
    }

    [Fact]
    public void Pet_UnionKind_IsCSharpUnion()
    {
        var unionShape = (IUnionTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Pet));

        Assert.Equal(UnionKind.CSharpUnion, unionShape.UnionKind);
    }

    [Fact]
    public void Pet_UnionCases_MatchConstructorParameterTypes()
    {
        var unionShape = (IUnionTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Pet));

        Assert.Equal(3, unionShape.UnionCases.Count);
        Assert.Equal(typeof(Dog), unionShape.UnionCases[0].UnionCaseType.Type);
        Assert.Equal(typeof(Cat), unionShape.UnionCases[1].UnionCaseType.Type);
        Assert.Equal(typeof(int), unionShape.UnionCases[2].UnionCaseType.Type);
    }

    [Fact]
    public void Pet_UnionCaseNames_AreTypeNames()
    {
        var unionShape = (IUnionTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Pet));

        Assert.Equal("Dog", unionShape.UnionCases[0].Name);
        Assert.Equal("Cat", unionShape.UnionCases[1].Name);
        Assert.Equal("Int32", unionShape.UnionCases[2].Name);
    }

    [Fact]
    public void Pet_GetGetUnionCaseIndex_IdentifiesDogCase()
    {
        var unionShape = (IUnionTypeShape<Pet>)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Pet));
        Getter<Pet, int> getCaseIndex = unionShape.GetGetUnionCaseIndex();
        Pet pet = new Pet(new Dog { Name = "Rex", Age = 3 });

        int index = getCaseIndex(ref pet);

        Assert.Equal(0, index);
    }

    [Fact]
    public void Pet_GetGetUnionCaseIndex_IdentifiesCatCase()
    {
        var unionShape = (IUnionTypeShape<Pet>)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Pet));
        Getter<Pet, int> getCaseIndex = unionShape.GetGetUnionCaseIndex();
        Pet pet = new Pet(new Cat { Name = "Whiskers", IsIndoor = true });

        int index = getCaseIndex(ref pet);

        Assert.Equal(1, index);
    }

    [Fact]
    public void Pet_GetGetUnionCaseIndex_IdentifiesIntCase()
    {
        var unionShape = (IUnionTypeShape<Pet>)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Pet));
        Getter<Pet, int> getCaseIndex = unionShape.GetGetUnionCaseIndex();
        Pet pet = new Pet(42);

        int index = getCaseIndex(ref pet);

        Assert.Equal(2, index);
    }

    [Fact]
    public void Pet_BaseType_IsObjectShapeOfPet()
    {
        var unionShape = (IUnionTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Pet));

        Assert.Equal(typeof(Pet), unionShape.BaseType.Type);
        Assert.Equal(TypeShapeKind.Object, unionShape.BaseType.Kind);
    }

    [Fact]
    public void ExistingSubtypeUnion_UnionKind_IsClassHierarchy()
    {
        var unionShape = (IUnionTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(PolymorphicClass));

        Assert.Equal(UnionKind.ClassHierarchy, unionShape.UnionKind);
    }

    [Fact]
    public void ClosedColor_IsClosed_ReturnsTrue()
    {
        var enumShape = (IEnumTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(ClosedColor));

        Assert.True(enumShape.IsClosed);
    }

    [Fact]
    public void RegularEnum_IsClosed_ReturnsFalse()
    {
        var enumShape = (IEnumTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(StringComparison));

        Assert.False(enumShape.IsClosed);
    }

    [Fact]
    public void Shape_WithClosedSubtype_IsDetectedAsUnion()
    {
        ITypeShape shape = s_reflectionProvider.GetTypeShapeOrThrow(typeof(Shape));

        Assert.Equal(TypeShapeKind.Union, shape.Kind);
    }

    [Fact]
    public void Shape_UnionKind_IsClassHierarchy()
    {
        var unionShape = (IUnionTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Shape));

        Assert.Equal(UnionKind.ClassHierarchy, unionShape.UnionKind);
    }

    [Fact]
    public void Shape_InferredDerivedTypes_IncludeCircleAndRectangle()
    {
        var unionShape = (IUnionTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Shape));

        Type[] caseTypes = unionShape.UnionCases.Select(c => c.UnionCaseType.Type).ToArray();
        Assert.Contains(typeof(Circle), caseTypes);
        Assert.Contains(typeof(Rectangle), caseTypes);
    }

    [Fact]
    public void Vehicle_WithInferDerivedTypes_DiscoversDerivedTypesFromAssemblyScan()
    {
        var unionShape = (IUnionTypeShape)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Vehicle));

        Type[] caseTypes = unionShape.UnionCases.Select(c => c.UnionCaseType.Type).ToArray();
        Assert.Contains(typeof(Car), caseTypes);
        Assert.Contains(typeof(Truck), caseTypes);
    }

    [Fact]
    public void Shape_GetGetUnionCaseIndex_IdentifiesCircle()
    {
        var unionShape = (IUnionTypeShape<Shape>)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Shape));
        Getter<Shape, int> getCaseIndex = unionShape.GetGetUnionCaseIndex();
        Shape circle = new Circle { Color = "Red", Radius = 5.0 };

        int index = getCaseIndex(ref circle);

        Assert.True(index >= 0, "Circle should match a union case");
        Assert.Equal(typeof(Circle), unionShape.UnionCases[index].UnionCaseType.Type);
    }

    [Fact]
    public void Vehicle_GetGetUnionCaseIndex_IdentifiesCar()
    {
        var unionShape = (IUnionTypeShape<Vehicle>)s_reflectionProvider.GetTypeShapeOrThrow(typeof(Vehicle));
        Getter<Vehicle, int> getCaseIndex = unionShape.GetGetUnionCaseIndex();
        Vehicle car = new Car { Make = "Toyota", Doors = 4 };

        int index = getCaseIndex(ref car);

        Assert.True(index >= 0, "Car should match a union case");
        Assert.Equal(typeof(Car), unionShape.UnionCases[index].UnionCaseType.Type);
    }

    [Fact]
    public void SourceGen_ClosedColor_IsClosed_ReturnsTrue()
    {
        var enumShape = (IEnumTypeShape)Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow(typeof(ClosedColor));

        Assert.True(enumShape.IsClosed);
    }

    [Fact]
    public void SourceGen_Shape_WithClosedSubtype_IsDetectedAsUnion()
    {
        ITypeShape shape = Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow(typeof(Shape));

        Assert.Equal(TypeShapeKind.Union, shape.Kind);
    }

    [Fact]
    public void SourceGen_Shape_UnionCases_IncludeCircleAndRectangle()
    {
        var unionShape = (IUnionTypeShape)Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow(typeof(Shape));

        Type[] caseTypes = unionShape.UnionCases.Select(c => c.UnionCaseType.Type).ToArray();
        Assert.Contains(typeof(Circle), caseTypes);
        Assert.Contains(typeof(Rectangle), caseTypes);
    }

    [Fact]
    public void SourceGen_Shape_UnionKind_IsClassHierarchy()
    {
        var unionShape = (IUnionTypeShape)Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow(typeof(Shape));

        Assert.Equal(UnionKind.ClassHierarchy, unionShape.UnionKind);
    }
}
