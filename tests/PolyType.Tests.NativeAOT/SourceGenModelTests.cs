using PolyType.Abstractions;
using PolyType.SourceGenModel;

namespace PolyType.Tests.NativeAOT;

public class SourceGenModelTests
{
    [Test]
    public async Task GeneratorVersionSurvivesTrimming()
    {
        var provider = (SourceGenTypeShapeProvider)TypeShapeResolver.Resolve<SimpleTestData>().Provider;
        await Assert.That(provider.SourceGeneratorVersion).IsNotNull();
    }

    [Test]
    public async Task EmptyClosedHierarchyRemainsAUnion()
    {
        var shape = (IUnionTypeShape<EmptyClosedShape>)TypeShapeResolver.Resolve<EmptyClosedShape>();
        await Assert.That(shape.UnionKind).IsEqualTo(UnionTypeShapeKind.TypeHierarchy);
        await Assert.That(shape.UnionCases.Count).IsEqualTo(0);
        await Assert.That(shape.BaseType.Kind).IsEqualTo(TypeShapeKind.Object);
        await Assert.That(shape.BaseType.IsContextual).IsTrue();
        EmptyClosedShape value = null!;
        await Assert.That(shape.GetGetUnionCaseIndex()(ref value)).IsEqualTo(-1);
    }
}
