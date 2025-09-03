using System.Runtime.CompilerServices;

namespace PolyType.Tests;

public class TypeShapeVisitorTests(ITestOutputHelper logger)
{
    private readonly MyVisitor visitor = new();

    [Fact]
    public void AllVisitorMethodsTested()
    {
        var visitorMethods = typeof(TypeShapeVisitor).GetMethods()
            .Where(m => m.Name.StartsWith("Visit"))
            .Select(m => m.Name);
        var testedMethods = typeof(TypeShapeVisitorTests).GetMethods()
            .Where(m => m.Name.StartsWith("Visit"))
            .Select(m => m.Name);
        var untestedMethods = visitorMethods.Except(testedMethods);
        if (untestedMethods.Any())
        {
            logger.WriteLine($"The following visitor methods are not tested: \n{string.Join("\n", untestedMethods)}");
        }

        Assert.Empty(untestedMethods);
    }

    [Fact]
    public void VisitEnum() => AssertVisitor(v => v.VisitEnum<TypeCode, int>(default!));

    [Fact]
    public void VisitObject() => AssertVisitor(v => v.VisitObject<object>(default!));

    [Fact]
    public void VisitProperty() => AssertVisitor(v => v.VisitProperty<object, object>(default!));

    [Fact]
    public void VisitConstructor() => AssertVisitor(v => v.VisitConstructor<object, IArgumentState>(default!));

    [Fact]
    public void VisitParameter() => AssertVisitor(v => v.VisitParameter<IArgumentState, object>(default!));

    [Fact]
    public void VisitOptional() => AssertVisitor(v => v.VisitOptional<object, object>(default!));

    [Fact]
    public void VisitEnumerable() => AssertVisitor(v => v.VisitEnumerable<object, object>(default!));

    [Fact]
    public void VisitDictionary() => AssertVisitor(v => v.VisitDictionary<object, object, object>(default!));

    [Fact]
    public void VisitSurrogate() => AssertVisitor(v => v.VisitSurrogate<object, object>(default!));

    [Fact]
    public void VisitUnion() => AssertVisitor(v => v.VisitUnion<object>(default!));

    [Fact]
    public void VisitUnionCase() => AssertVisitor(v => v.VisitUnionCase<object, object>(default!));

    [Fact]
    public void VisitMethod() => AssertVisitor(v => v.VisitMethod<object, IArgumentState, object>(default!));

    [Fact]
    public void VisitFunction() => AssertVisitor(v => v.VisitFunction<object, IArgumentState, object>(default!));

    [Fact]
    public void VisitEvent() => AssertVisitor(v => v.VisitEvent<object, object>(default!));

    private void AssertVisitor(Action<TypeShapeVisitor> test, [CallerMemberName] string? methodName = null)
    {
        NotImplementedException ex = Assert.Throws<NotImplementedException>(() => test(this.visitor));
        Assert.Contains($"{this.visitor.GetType().Name}.{methodName}", ex.Message);
        logger.WriteLine(ex.Message);
    }

    class MyVisitor : TypeShapeVisitor { }
}
