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
}
