using PolyType.Examples.JsonSerializer;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PolyType.Tests;

public abstract partial class JsonSpecializedTests(ProviderUnderTest providerUnderTest)
{
    [Theory, PairwiseData]
    public void RequiredMembersWithDefaultCtor_Exhaustive([CombinatorialRange(0, (1 << RequiredMembersWithDefaultCtor.FieldCount) - 1)] int mask)
    {
        StringBuilder builder = new("{");
        if ((mask & 0x1) == 0x1) builder.Append("\"A\":\"X\",");
        if ((mask & 0x2) == 0x2) builder.Append("\"B\":3,");
        if ((mask & 0x4) == 0x4) builder.Append("\"C\":false,");
        if ((mask & 0x8) == 0x8) builder.Append("\"D\":false,");
        if (builder.Length > 1) builder.Length -= 1; // Remove trailing comma
        builder.Append('}');
        TestContext.Current.TestOutputHelper?.WriteLine(builder.ToString());

        if ((mask & 0x5) == 0x5)
        {
            RequiredMembersWithDefaultCtor? actual = AssertRequirementMet<RequiredMembersWithDefaultCtor>(builder.ToString());
            Assert.True(actual is { A: "X", C: false });
            Assert.Equal((mask & 0x2) == 0x2 ? 3 : 5, actual.B);
            Assert.Equal((mask & 0x8) == 0x8 ? false : true, actual.D);
        }
        else
        {
            AssertRequirementsNotMet<RequiredMembersWithDefaultCtor>(builder.ToString());
        }
    }

    [Theory, PairwiseData]
    public void RequiredMembersWithComprehensiveCtor_Exhaustive([CombinatorialRange(0, (1 << RequiredMembersWithComprehensiveCtor.FieldCount) - 1)] int mask)
    {
        StringBuilder builder = new("{");
        if ((mask & 0x1) == 0x1) builder.Append("\"A\":\"X\",");
        if ((mask & 0x2) == 0x2) builder.Append("\"B\":3,");
        if ((mask & 0x4) == 0x4) builder.Append("\"C\":false,");
        if ((mask & 0x8) == 0x8) builder.Append("\"D\":false,");
        if (builder.Length > 1) builder.Length -= 1; // Remove trailing comma
        builder.Append('}');
        TestContext.Current.TestOutputHelper?.WriteLine(builder.ToString());

        if ((mask & 0x5) == 0x5)
        {
            RequiredMembersWithComprehensiveCtor? actual = AssertRequirementMet<RequiredMembersWithComprehensiveCtor>(builder.ToString());
            Assert.True(actual is { A: "X", C: false });
            Assert.Equal((mask & 0x2) == 0x2 ? 3 : 5, actual.B);
            Assert.Equal((mask & 0x8) == 0x8 ? false : true, actual.D);
        }
        else
        {
            AssertRequirementsNotMet<RequiredMembersWithComprehensiveCtor>(builder.ToString());
        }
    }

    [Theory, PairwiseData]
    public void RequiredMembersWithPartialCtor_Exhaustive([CombinatorialRange(0, (1 << RequiredMembersWithPartialCtor.FieldCount) - 1)] int mask)
    {
        StringBuilder builder = new("{");
        if ((mask & 0x1) == 0x1) builder.Append("\"A\":\"X\",");
        if ((mask & 0x2) == 0x2) builder.Append("\"B\":3,");
        if ((mask & 0x4) == 0x4) builder.Append("\"C\":false,");
        if ((mask & 0x8) == 0x8) builder.Append("\"D\":false,");
        if (builder.Length > 1) builder.Length -= 1; // Remove trailing comma
        builder.Append('}');
        TestContext.Current.TestOutputHelper?.WriteLine(builder.ToString());

        if ((mask & 0x5) == 0x5)
        {
            RequiredMembersWithPartialCtor? actual = AssertRequirementMet<RequiredMembersWithPartialCtor>(builder.ToString());
            Assert.True(actual is { A: "X", C: false });
            Assert.Equal((mask & 0x2) == 0x2 ? 3 : 5, actual.B);
            Assert.Equal((mask & 0x8) == 0x8 ? false : true, actual.D);
        }
        else
        {
            AssertRequirementsNotMet<RequiredMembersWithPartialCtor>(builder.ToString());
        }
    }

    private void AssertRequirementsNotMet<T>([StringSyntax("json")] string json)
    {
        var converter = JsonSerializerTS.CreateConverter<T>(providerUnderTest.Provider);
        JsonException ex = Assert.Throws<JsonException>(() => converter.Deserialize(json));
        TestContext.Current.TestOutputHelper!.WriteLine(ex.Message);
    }

    private T? AssertRequirementMet<T>([StringSyntax("json")] string json)
    {
        var converter = JsonSerializerTS.CreateConverter<T>(providerUnderTest.Provider);
        return converter.Deserialize(json);
    }

    [GenerateShape]
    public partial record RequiredMembersWithDefaultCtor
    {
        internal const int FieldCount = 4;

        public required string A { get; set; }
        public int B { get; set; } = 5;
        public required bool C { get; init; } = true;
        public bool D { get; set; } = true;
    }

    [GenerateShape]
    public partial record RequiredMembersWithComprehensiveCtor
    {
        internal const int FieldCount = 4;

        [SetsRequiredMembers]
        public RequiredMembersWithComprehensiveCtor(string a, bool c, int b = 5, bool d = true)
        {
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public required string A { get; set; }
        public int B { get; set; }
        public required bool C { get; init; }
        public bool D { get; set; }
    }

    [GenerateShape]
    public partial record RequiredMembersWithPartialCtor
    {
        internal const int FieldCount = 4;

        public RequiredMembersWithPartialCtor(string a)
        {
            A = a;
        }

        public string A { get; set; } // TODO: required keyword here causes the JsonSerializer to throw.
        public int B { get; set; } = 5;
        public required bool C { get; init; } = true;
        public bool D { get; set; } = true;
    }

    [GenerateShape<RequiredMembersWithDefaultCtor>]
    internal partial class Witness;

    public sealed class Reflection() : JsonSpecializedTests(RefectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : JsonSpecializedTests(RefectionProviderUnderTest.Emit);
    public sealed class SourceGen() : JsonSpecializedTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
