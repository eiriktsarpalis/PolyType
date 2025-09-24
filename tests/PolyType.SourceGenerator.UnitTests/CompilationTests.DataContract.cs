using Microsoft.CodeAnalysis;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

// Tests covering usage of System.Runtime.Serialization data contract annotations.
public static partial class CompilationTests
{
    [Fact]
    public static void DataContract_Class_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Runtime.Serialization;
            using PolyType;

            [DataContract]
            [GenerateShape]
            public partial class Customer
            {
                [DataMember(Name = "identifier", Order = 1)]
                public int Id { get; set; }

                [DataMember(Order = 2, IsRequired = true)]
                public string? Name { get; set; }

                // Not annotated -> should be ignored by data contract projection (still fine for generator)
                public string? IgnoredProp { get; set; }

                [DataMember(Order = 3)]
                public Address? PrimaryAddress { get; set; }
            }

            [DataContract]
            [GenerateShape]
            public partial class Address
            {
                [DataMember(Order = 1)] public string? Street { get; set; }
                [DataMember(Order = 2)] public string? City { get; set; }
                [DataMember(Order = 3)] public string? Country { get; set; }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DataContract_Record_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Runtime.Serialization;
            using PolyType;

            [DataContract]
            [GenerateShape]
            public partial record Order(
                [property: DataMember(Order = 1)] int Id,
                [property: DataMember(Name = "when", Order = 2)] System.DateTime CreatedAt,
                [property: DataMember(Order = 3, IsRequired = true)] decimal Total);
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DataContract_KnownTypeHierarchy_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Runtime.Serialization;
            using PolyType;

            [DataContract]
            [KnownType(typeof(Dog))]
            [KnownType(typeof(Cat))]
            [GenerateShape]
            public abstract partial class Animal
            {
                [DataMember(Order = 1)] public string? Name { get; set; }
            }

            public sealed class Dog : Animal
            {
                [DataMember(Order = 2)] public bool Barks { get; set; }
            }

            public sealed class Cat : Animal
            {
                [DataMember(Order = 2)] public int Lives { get; set; }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DataContract_Enum_WithEnumMember_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Runtime.Serialization;
            using PolyType;

            [DataContract]
            internal enum Color
            {
                [EnumMember(Value = "r")] Red = 1,
                [EnumMember(Value = "g")] Green = 2,
                [EnumMember(Value = "b")] Blue = 3,
            }

            [GenerateShapeFor(typeof(Color))]
            internal partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DataContract_IgnoreDataMember_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Runtime.Serialization;
            using PolyType;

            [DataContract]
            [GenerateShape]
            public partial class Product
            {
                [DataMember(Order = 1)] public int Id { get; set; }
                [DataMember(Order = 2)] public string? Name { get; set; }
                [IgnoreDataMember] public string? InternalCode { get; set; } // should be ignored by data contract projection
                // Also ensure normal non-attributed property remains fine.
                public string? NonAnnotated { get; set; }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DataContract_ConflictingMembers()
    {
        // Regression test for https://github.com/eiriktsarpalis/PolyType/issues/286

        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.IO;
            using System.Runtime.Serialization;

            [DataContract]
            [GenerateShape]
            public partial class StreamContainingClass
            {
                [DataMember]
                private Stream innerStream;

                public StreamContainingClass(Stream innerStream)
                {
                    this.innerStream = innerStream;
                }

                public Stream InnerStream => innerStream;
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
}
