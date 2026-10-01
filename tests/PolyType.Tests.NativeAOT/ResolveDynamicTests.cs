using PolyType.Abstractions;
using PolyType.Examples.JsonSerializer;

namespace PolyType.Tests.NativeAOT;

public partial class ResolveDynamicTests
{
    [Test]
    public async Task UnionFactoryAndExtractorAreCallableWithoutReflection()
    {
        var shape = (IUnionTypeShape<FactoryUnion>)TypeShapeResolver.Resolve<FactoryUnion>();
        var unionCase = (IUnionCaseShape<int, FactoryUnion>)shape.UnionCases[0];
        FactoryUnion value = unionCase.Marshaler.Marshal(42)!;
        await Assert.That(shape.GetGetUnionCaseIndex()(ref value)).IsEqualTo(0);
        await Assert.That(unionCase.Marshaler.Unmarshal(value)).IsEqualTo(42);
        await Assert.That(JsonSerializerTS.Serialize(value)).IsEqualTo("42");
        FactoryUnion? result = JsonSerializerTS.Deserialize<FactoryUnion>("42");
        await Assert.That(unionCase.Marshaler.Unmarshal(result)).IsEqualTo(42);
        value = null!;
        await Assert.That(() => shape.GetGetUnionCaseIndex()(ref value)).Throws<System.Runtime.CompilerServices.SwitchExpressionException>();
    }

    [Test]
    public async Task ResolveDynamic_NativeAOT_Supported()
    {
        await Assert.That(TypeShapeResolver.ResolveDynamic<Poco1>()).IsNotNull();
        await Assert.That(TypeShapeResolver.ResolveDynamic<Poco2, Witness>()).IsNotNull();

        await Assert.That(TypeShapeResolver.ResolveDynamic<Struct1>()).IsNotNull();
        await Assert.That(TypeShapeResolver.ResolveDynamic<Struct2, Witness>()).IsNotNull();

        await Assert.That(TypeShapeResolver.ResolveDynamic<NetStandardPoco1>()).IsNotNull();
        await Assert.That(TypeShapeResolver.ResolveDynamic<NetStandardPoco2, Witness>()).IsNotNull();
    }

    [GenerateShape]
    public partial record Poco1;

    public record Poco2;

    [GenerateShape]
    public partial record Struct1;

    public partial record Struct2;

    [GenerateShapeFor<Poco2>]
    [GenerateShapeFor<Struct2>]
    [GenerateShapeFor<NetStandardPoco1>]
    [GenerateShapeFor<NetStandardPoco2>]
    public partial class Witness;

    [TypeShapeProvider(typeof(Provider))]
    public record NetStandardPoco1
    {
        // Simulates the source generation strategy
        // used by netstandard2.0 compilations.
        private sealed class Provider : ITypeShapeProvider
        {
            public ITypeShape? GetTypeShape(Type type) =>
                type == typeof(NetStandardPoco1) ?
                PolyType.SourceGenerator.TypeShapeProvider_PolyType_Tests_NativeAOT.Default.NetStandardPoco1 :
                null;
        }
    }

    public record NetStandardPoco2;

    [TypeShapeProvider(typeof(Provider))]
    public partial class NetStandardWitness
    {
        // Simulates the source generation strategy
        // used by netstandard2.0 compilations.
        private sealed class Provider : ITypeShapeProvider
        {
            public ITypeShape? GetTypeShape(Type type) =>
                type == typeof(NetStandardPoco2) ?
                PolyType.SourceGenerator.TypeShapeProvider_PolyType_Tests_NativeAOT.Default.NetStandardPoco2 :
                null;
        }
    }
}
