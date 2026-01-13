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
                    public string? Name { get; set; }
                }

                public class AddressDto
                {
                    public string? Street { get; set; }
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
                    public string? Name { get; set; }
                }
            }

            namespace MyNamespace.Models
            {
                public class Person
                {
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("MyNamespace.Dtos.*")]
            [GenerateShapeFor("MyNamespace.Models.*")]
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
                    public string? Name { get; set; }
                }

                public class AddressDto
                {
                    public string? Street { get; set; }
                }

                public class PersonModel
                {
                    public string? Name { get; set; }
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
                    public string? Name { get; set; }
                }
            }

            namespace OtherCompany.Product.Dtos
            {
                public class AddressDto
                {
                    public string? Street { get; set; }
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
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        // Empty pattern should generate a warning for matching no types
        Assert.Contains(result.Diagnostics, d => d.Id == "PT0014" && d.Severity == DiagnosticSeverity.Warning);
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
                    public string? Name { get; set; }
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
                    public string? Name { get; set; }
                }

                public class Dto2
                {
                    public string? Name { get; set; }
                }

                public class Dto10
                {
                    public string? Name { get; set; }
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

    [Fact]
    public static void GenerateShapeFor_PatternMatchesNoTypes_ReportsWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace
            {
                public class Person
                {
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("NonExistent.Namespace.*")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        
        // Should have a warning for pattern matching no types
        Assert.Contains(result.Diagnostics, d => 
            d.Id == "PT0014" && 
            d.Severity == DiagnosticSeverity.Warning &&
            d.GetMessage().Contains("NonExistent.Namespace.*"));
    }

    [Fact]
    public static void GenerateShapeFor_MultiplePatterns_OnlyUnmatchedReportsWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace.Dtos
            {
                public class PersonDto
                {
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("MyNamespace.Dtos.*")]
            [GenerateShapeFor("NonExistent.*")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        
        // Should have exactly one warning for the unmatched pattern
        var warnings = result.Diagnostics.Where(d => d.Id == "PT0014").ToList();
        Assert.Single(warnings);
        Assert.Contains("NonExistent.*", warnings[0].GetMessage());
        
        // Verify the matched pattern still generates code
        string generatedCode = string.Join("\n", result.NewCompilation.SyntaxTrees.Select(t => t.ToString()));
        Assert.Contains("PersonDto", generatedCode);
    }

    [Fact]
    public static void GenerateShapeFor_EmptyPattern_ReportsWarningForNoMatch()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace
            {
                public class Person
                {
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        
        // Empty pattern should report warning for matching no types
        Assert.Contains(result.Diagnostics, d => d.Id == "PT0014");
    }

    [Fact]
    public static void GenerateShapeFor_OverlyBroadPattern_SingleAsterisk_ReportsWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace
            {
                public class Person
                {
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("*")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        
        // Pattern "*" is overly broad and should report PT0015 warning
        Assert.Contains(result.Diagnostics, d => 
            d.Id == "PT0015" && 
            d.Severity == DiagnosticSeverity.Warning &&
            d.GetMessage().Contains("*"));
    }

    [Fact]
    public static void GenerateShapeFor_OverlyBroadPattern_DotStar_ReportsWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace
            {
                public class Person
                {
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("*.*")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        
        // Pattern "*.*" is overly broad and should report PT0015 warning
        Assert.Contains(result.Diagnostics, d => 
            d.Id == "PT0015" && 
            d.Severity == DiagnosticSeverity.Warning &&
            d.GetMessage().Contains("*.*"));
    }

    [Fact]
    public static void GenerateShapeFor_OverlyBroadPattern_MultipleAsterisks_ReportsWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace
            {
                public class Person
                {
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("***")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        
        // Pattern "***" is overly broad and should report PT0015 warning
        Assert.Contains(result.Diagnostics, d => 
            d.Id == "PT0015" && 
            d.Severity == DiagnosticSeverity.Warning &&
            d.GetMessage().Contains("***"));
    }

    [Fact]
    public static void GenerateShapeFor_OverlyBroadPattern_OnlyWildcardsAndDots_ReportsWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace
            {
                public class Person
                {
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("*.*.*")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        
        // Pattern "*.*.*" is overly broad and should report PT0015 warning
        Assert.Contains(result.Diagnostics, d => 
            d.Id == "PT0015" && 
            d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public static void GenerateShapeFor_ValidPatternWithConstantCharacters_NoWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            namespace MyNamespace.Dtos
            {
                public class PersonDto
                {
                    public string? Name { get; set; }
                }
            }

            [GenerateShapeFor("MyNamespace.Dtos.*")]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        
        // Valid pattern should not report PT0015 warning
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "PT0015");
        Assert.Empty(result.Diagnostics);
    }
}
