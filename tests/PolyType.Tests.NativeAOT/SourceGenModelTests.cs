using PolyType.Abstractions;
using PolyType.SourceGenModel;

namespace PolyType.Tests.NativeAOT;

[GenerateShapeFor<ScalarUnion?>]
public partial class SourceGenModelTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NullableUnionDeconstructorPreservesPresence(bool hasValue)
    {
        ScalarUnion? optional = hasValue ? new ScalarUnion((string?)null) : default(ScalarUnion?);
        var shape = (IOptionalTypeShape<ScalarUnion?, ScalarUnion>)GeneratedTypeShapeProvider.GetTypeShapeOrThrow<ScalarUnion?>();
        bool result = shape.GetDeconstructor()(optional, out ScalarUnion element);

        await Assert.That(optional.HasValue).IsEqualTo(hasValue);
        await Assert.That(result).IsEqualTo(hasValue);
        await Assert.That(element.Value).IsNull();
    }

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
