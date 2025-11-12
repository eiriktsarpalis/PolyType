namespace PolyType.Tests;

public abstract partial class EnumMemberShapeTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void EnumMemberShapeTest()
    {
        var enumShape = (IEnumTypeShape<TestEnum, byte>)providerUnderTest.Provider.GetTypeShapeOrThrow<TestEnum>();
        Assert.Equal(3, enumShape.Members.Count);
        Assert.Equal(0, enumShape.Members["FirstValue"]);
        Assert.Equal(5, enumShape.Members["Second"]);
        Assert.Equal(8, enumShape.Members["3rd"]);
    }

    [Fact]
    public void Enum_IsFlags_WhenNotFlagsAttribute_ReturnsFalse()
    {
        var enumShape = (IEnumTypeShape<TestEnum, byte>)providerUnderTest.Provider.GetTypeShapeOrThrow<TestEnum>();
        Assert.False(enumShape.IsFlags);
    }

    [Fact]
    public void Enum_IsFlags_WhenFlagsAttribute_ReturnsTrue()
    {
        var enumShape = (IEnumTypeShape<TestFlagsEnum, int>)providerUnderTest.Provider.GetTypeShapeOrThrow<TestFlagsEnum>();
        Assert.True(enumShape.IsFlags);
    }

    public enum TestEnum : byte
    {
        [EnumMemberShape(Name = "FirstValue")]
        First,
        Second = 5,
        [EnumMemberShape(Name = "3rd")]
        Third = 8,
    }

    [Flags]
    public enum TestFlagsEnum
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4,
        All = Read | Write | Execute
    }

    [GenerateShapeFor<TestEnum>]
    [GenerateShapeFor<TestFlagsEnum>]
    protected partial class Witness;

    public sealed class Reflection() : EnumMemberShapeTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : EnumMemberShapeTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : EnumMemberShapeTests(new SourceGenProviderUnderTest(Witness.GeneratedTypeShapeProvider));
}
