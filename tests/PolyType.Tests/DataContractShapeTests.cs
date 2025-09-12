using System.Runtime.Serialization;

namespace PolyType.Tests;

public abstract partial class DataContractShapeTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void DataContract_DataMember_ObjectShape()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(ContractType));
        Assert.NotNull(shape);

        // Only members explicitly marked with [DataMember] should be included.
        Assert.Equal(3, shape.Properties.Count);

        // Orders should follow DataMember.Order
        Assert.Equal("id", shape.Properties[0].Name);
        Assert.Equal("Renamed", shape.Properties[1].Name);
        Assert.Equal("AlsoIncluded", shape.Properties[2].Name);

        // Required flag propagated from DataMember(IsRequired=true)
        Assert.True(shape.Constructor!.Parameters.Single(p => p.Name == "id").IsRequired);
        Assert.False(shape.Constructor.Parameters.Single(p => p.Name == "Renamed").IsRequired);
    }

    [Fact]
    public void NonDataContract_IgnoresDataMember()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(NonContractType));
        Assert.NotNull(shape);
        // Non-contract type: public properties included even without DataMember.
        Assert.Contains(shape.Properties, p => p.Name == nameof(NonContractType.Value));
        // DataMember attribute on this type should not filter others out.
        Assert.Contains(shape.Properties, p => p.Name == nameof(NonContractType.Other));
    }

    [Fact]
    public void DerivedNonContractType_IncludesBaseContractProperties()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(DerivedNonContractType));
        Assert.NotNull(shape);

        // Only members explicitly marked with [DataMember] should be included.
        Assert.Equal(4, shape.Properties.Count);

        // Orders should follow DataMember.Order
        Assert.Equal("NewValue", shape.Properties[0].Name);
        Assert.Equal("id", shape.Properties[1].Name);
        Assert.Equal("Renamed", shape.Properties[2].Name);
        Assert.Equal("AlsoIncluded", shape.Properties[3].Name);

        // Required flag propagated from DataMember(IsRequired=true)
        Assert.True(shape.Constructor!.Parameters.Single(p => p.Name == "id").IsRequired);
        Assert.False(shape.Constructor.Parameters.Single(p => p.Name == "Renamed").IsRequired);
    }

    [Fact]
    public void NonContractTypeWithIgnoreDataMemberAttribute_IgnoresIgnoredProperty()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(NonContractTypeWithIgnoreDataMemberAttribute));
        Assert.NotNull(shape);

        Assert.Equal(2, shape.Properties.Count);
        Assert.Contains(shape.Properties, p => p.Name == nameof(NonContractTypeWithIgnoreDataMemberAttribute.Included));
        // Property with [IgnoreDataMember] should be excluded.
        Assert.DoesNotContain(shape.Properties, p => p.Name == nameof(NonContractTypeWithIgnoreDataMemberAttribute.Ignored));
        // Property with both [IgnoreDataMember] and [PropertyShape] should be included.
        Assert.Contains(shape.Properties, p => p.Name == nameof(NonContractTypeWithIgnoreDataMemberAttribute.IgnoreWithShapeAttribute));
    }

    [Fact]
    public void Enum_EnumMember_And_EnumMemberShape_Priority()
    {
        var enumShape = (IEnumTypeShape<ContractEnum, int>)providerUnderTest.Provider.GetTypeShapeOrThrow<ContractEnum>();
        // Expect names: First (EnumMember value), SecondRenamed (EnumMemberShape), Third (field name)
        Assert.Equal(4, enumShape.Members.Count);
        Assert.True(enumShape.Members.ContainsKey("FirstRenamed"));
        Assert.True(enumShape.Members.ContainsKey("SecondRenamed"));
        Assert.True(enumShape.Members.ContainsKey("ThirdRenamed"));
        Assert.True(enumShape.Members.ContainsKey("Fourth"));
    }

    [GenerateShape]
    [DataContract]
    public partial class ContractType
    {
        [DataMember(Name = "id", Order = 0, IsRequired = true)]
        public int Id { get; set; }

        [DataMember(Name = "Renamed", Order = 1)]
        public string? Name { get; set; }

        [DataMember(Order = 2)]
        public bool AlsoIncluded { get; set; }

        // Not annotated => excluded because type is [DataContract]
        public int Ignored { get; set; }

        [PropertyShape(Name = "ExplicitShape", Order = 3)]
        public int ViaPropertyShape { get; set; }
    }

    [GenerateShape]
    public partial class NonContractType
    {
        [DataMember] // Has no effect on inclusion (type not marked DataContract)
        public int Value { get; set; }
        public int Other { get; set; }
    }

    [GenerateShape]
    public partial class DerivedNonContractType : ContractType
    {
        public int NewValue { get; set; }
    }

    [GenerateShape]
    public partial class NonContractTypeWithIgnoreDataMemberAttribute
    {
        [IgnoreDataMember]
        public int Ignored { get; set; }
        public int Included { get; set; }
        [IgnoreDataMember, PropertyShape]
        public int IgnoreWithShapeAttribute { get; set; }
    }

    public enum ContractEnum
    {
        [EnumMember(Value = "FirstRenamed")] First = 0,
        [EnumMemberShape(Name = "SecondRenamed"), EnumMember(Value = "IgnoredName")] Second = 1,
        [EnumMemberShape, EnumMember(Value = "ThirdRenamed")] Third = 2,
        Fourth = 3,
    }

    [GenerateShapeFor<ContractEnum>]
    public partial class Witness;

    public sealed class Reflection() : DataContractShapeTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : DataContractShapeTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : DataContractShapeTests(new SourceGenProviderUnderTest(PolyType.SourceGenerator.TypeShapeProvider_PolyType_Tests.Default));
}
