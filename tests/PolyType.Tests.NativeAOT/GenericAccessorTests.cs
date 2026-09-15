using PolyType.Examples.JsonSerializer;

namespace PolyType.Tests.NativeAOT;

public partial class GenericAccessorTests
{
    [Test]
    public async Task PrivateGenericConstructors_PreserveInitializers()
    {
        var converter = JsonSerializerTS.CreateConverter<GenericPrivateConstructorWithInitializers<int>>(Witness.GeneratedTypeShapeProvider);
        var value = converter.Deserialize("""{"Value":42,"Count":3,"Name":"name"}""");
        await Assert.That(value).IsNotNull();
        await Assert.That(value!.Value).IsEqualTo(42);
        await Assert.That(value.Name).IsEqualTo("name");
        await Assert.That(value.Tag).IsEqualTo("default");

        var structConverter = JsonSerializerTS.CreateConverter<GenericPrivateConstructorStruct<string>>(Witness.GeneratedTypeShapeProvider);
        var structValue = structConverter.Deserialize("""{"Value":"value","ReadOnly":"ignored","InitOnly":"extra"}""");
        await Assert.That(structValue.GetMembers()).IsEqualTo(("value", "extra", "initial", -1));
    }

    [Test]
    public async Task CompositeAndNestedGenericMembers_Roundtrip()
    {
        var converter = JsonSerializerTS.CreateConverter<GenericPrivateCompositeMembers<int>>(Witness.GeneratedTypeShapeProvider);
        var value = GenericPrivateCompositeMembers<int>.Create([[1, 2]], [[3, 4]]);
        string json = converter.Serialize(value);
        await Assert.That(converter.Serialize(converter.Deserialize(json))).IsEqualTo(json);

        var nestedConverter = JsonSerializerTS.CreateConverter<GenericAccessorOuter<int>.Nested<string>>(Witness.GeneratedTypeShapeProvider);
        var nested = GenericAccessorOuter<int>.Nested<string>.Create(42, "item", 17);
        string nestedJson = nestedConverter.Serialize(nested);
        await Assert.That(nestedConverter.Serialize(nestedConverter.Deserialize(nestedJson))).IsEqualTo(nestedJson);
    }

    [Test]
    public async Task ReadonlyGenericStruct_InitAccessors()
    {
        var converter = JsonSerializerTS.CreateConverter<GenericAccessorColor<float>>(Witness.GeneratedTypeShapeProvider);
        var color = converter.Deserialize("""{"A":1,"R":0.25,"G":0.5,"B":0.75}""");
        await Assert.That((color.A, color.R, color.G, color.B)).IsEqualTo((1f, 0.25f, 0.5f, 0.75f));
    }

    [GenerateShapeFor<GenericPrivateConstructorWithInitializers<int>>]
    [GenerateShapeFor<GenericPrivateConstructorStruct<string>>]
    [GenerateShapeFor<GenericPrivateCompositeMembers<int>>]
    [GenerateShapeFor<GenericAccessorOuter<int>.Nested<string>>]
    [GenerateShapeFor<GenericAccessorColor<float>>]
    private partial class Witness;
}
