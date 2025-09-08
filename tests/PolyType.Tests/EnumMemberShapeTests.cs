namespace PolyType.Tests;

public abstract partial class EnumMemberShapeTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void EnumMemberShapeTest()
    {
        var enumShape = (IEnumTypeShape<TestEnum, byte>)providerUnderTest.Provider.GetTypeShape<TestEnum>(throwIfMissing: true)!;
        Assert.Equal(3, enumShape.Members.Count);
        Assert.Equal(0, enumShape.Members["FirstValue"]);
        Assert.Equal(5, enumShape.Members["Second"]);
        Assert.Equal(8, enumShape.Members["3rd"]);
    }

    public enum TestEnum : byte
    {
        [EnumMemberShape(Name = "FirstValue")]
        First,
        Second = 5,
        [EnumMemberShape(Name = "3rd")]
        Third = 8,
    }

    [GenerateShapeFor<TestEnum>]
    protected partial class Witness;

    public sealed class Reflection() : EnumMemberShapeTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : EnumMemberShapeTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : EnumMemberShapeTests(new SourceGenProviderUnderTest(Witness.GeneratedTypeShapeProvider));
}
