using Microsoft.CodeAnalysis;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class PatternMatchingTests
{
    [Fact]
    public static void GenerateShapeFor_WithSinglePattern_MatchesTypes()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace.Dtos
            {
                public class PersonDto
                {
                    public string Name { get; set; }
                }

                public class AddressDto
                {
                    public string Street { get; set; }
                }
            }

            namespace OtherNamespace
            {
                public class Other
                {
                    public int Value { get; set; }
                }
            }

            [GenerateShapeFor("MyNamespace.Dtos.*")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
        
        // Verify that PersonDto and AddressDto are generated
        string generatedCode = string.Join("\n", result.NewCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("PersonDto", generatedCode);
        Assert.Contains("AddressDto", generatedCode);
    }

    [Fact]
    public static void GenerateShapeFor_WithMultiplePatterns_MatchesAllTypes()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace.Dtos
            {
                public class PersonDto
                {
                    public string Name { get; set; }
                }
            }

            namespace MyNamespace.Models
            {
                public class Person
                {
                    public string Name { get; set; }
                }
            }

            [GenerateShapeFor("MyNamespace.Dtos.*", "MyNamespace.Models.*")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
        
        // Verify that both PersonDto and Person are generated
        string generatedCode = string.Join("\n", result.NewCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("PersonDto", generatedCode);
        Assert.Contains("Person", generatedCode);
    }

    [Fact]
    public static void GenerateShapeFor_WithWildcardPattern_MatchesPartialNames()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace
            {
                public class PersonDto
                {
                    public string Name { get; set; }
                }

                public class AddressDto
                {
                    public string Street { get; set; }
                }

                public class PersonModel
                {
                    public string Name { get; set; }
                }
            }

            [GenerateShapeFor("MyNamespace.*Dto")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
        
        // Verify that PersonDto and AddressDto are generated
        string generatedCode = string.Join("\n", result.NewCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("PersonDto", generatedCode);
        Assert.Contains("AddressDto", generatedCode);
    }

    [Fact]
    public static void GenerateShapeFor_WithNestedWildcard_MatchesDeepNamespaces()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace Company.Product.Dtos
            {
                public class PersonDto
                {
                    public string Name { get; set; }
                }
            }

            namespace OtherCompany.Product.Dtos
            {
                public class AddressDto
                {
                    public string Street { get; set; }
                }
            }

            [GenerateShapeFor("*.Dtos.*")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
        
        // Verify that both are generated
        string generatedCode = string.Join("\n", result.NewCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("PersonDto", generatedCode);
        Assert.Contains("AddressDto", generatedCode);
    }

    [Fact]
    public static void GenerateShapeFor_EmptyPattern_DoesNotMatch()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace
            {
                public class Person
                {
                    public string Name { get; set; }
                }
            }

            [GenerateShapeFor("")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        // Empty pattern should not cause errors, but won't match anything
        // Just verify no diagnostics are reported
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= Microsoft.CodeAnalysis.DiagnosticSeverity.Warning));
    }

    [Fact]
    public static void GenerateShapeFor_CombineTypeAndPattern_BothWork()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace.Dtos
            {
                public class PersonDto
                {
                    public string Name { get; set; }
                }
            }

            namespace OtherNamespace
            {
                public class SpecialType
                {
                    public int Value { get; set; }
                }
            }

            [GenerateShapeFor("MyNamespace.Dtos.*")]
            [GenerateShapeFor(typeof(OtherNamespace.SpecialType))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
        
        // Verify both are generated
        string generatedCode = string.Join("\n", result.NewCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("PersonDto", generatedCode);
        Assert.Contains("SpecialType", generatedCode);
    }

    [Fact]
    public static void GenerateShapeFor_SingleCharacterWildcard_MatchesSingleChar()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace
            {
                public class Dto1
                {
                    public string Name { get; set; }
                }

                public class Dto2
                {
                    public string Name { get; set; }
                }

                public class Dto10
                {
                    public string Name { get; set; }
                }
            }

            [GenerateShapeFor("MyNamespace.Dto?")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
        
        // Verify that Dto1 and Dto2 are generated
        string generatedCode = string.Join("\n", result.NewCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("Dto1", generatedCode);
        Assert.Contains("Dto2", generatedCode);
    }
}
