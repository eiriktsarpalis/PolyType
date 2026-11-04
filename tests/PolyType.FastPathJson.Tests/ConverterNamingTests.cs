using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class ConverterNamingTests
{
    [Theory]
    [InlineData("bool", "BooleanConverter")]
    [InlineData("System.Boolean", "BooleanConverter")]
    [InlineData("sbyte", "SByteConverter")]
    [InlineData("short", "Int16Converter")]
    [InlineData("int", "Int32Converter")]
    [InlineData("System.Int32", "Int32Converter")]
    [InlineData("long", "Int64Converter")]
    [InlineData("byte", "ByteConverter")]
    [InlineData("ushort", "UInt16Converter")]
    [InlineData("uint", "UInt32Converter")]
    [InlineData("ulong", "UInt64Converter")]
    [InlineData("float", "SingleConverter")]
    [InlineData("double", "DoubleConverter")]
    [InlineData("decimal", "DecimalConverter")]
    [InlineData("char", "CharConverter")]
    [InlineData("string", "StringConverter")]
    [InlineData("byte[]", "Array_ByteConverter")]
    [InlineData("System.Byte[]", "Array_ByteConverter")]
    [InlineData("System.Guid", "GuidConverter")]
    [InlineData("System.DateTime", "DateTimeConverter")]
    [InlineData("System.DateTimeOffset", "DateTimeOffsetConverter")]
    [InlineData("System.TimeSpan", "TimeSpanConverter")]
    [InlineData("System.DateOnly", "DateOnlyConverter")]
    [InlineData("System.TimeOnly", "TimeOnlyConverter")]
    [InlineData("System.Int128", "Int128Converter")]
    [InlineData("System.UInt128", "UInt128Converter")]
    [InlineData("System.Half", "HalfConverter")]
    [InlineData("System.Uri", "UriConverter")]
    [InlineData("System.Version", "VersionConverter")]
    [InlineData("System.Numerics.BigInteger", "BigIntegerConverter")]
    [InlineData("System.Text.Rune", "RuneConverter")]
    public static void ScalarConverterNamesDescribeSerializedTypes(string type, string expectedName)
    {
        string source = $$"""
            namespace TestNamespace;

            [GenerateJsonFor(typeof({{type}}))]
            public partial class Witness;
            """;
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        GenericNameSyntax[] calls = result.Driver.GetRunResult().Results.Single().GeneratedSources
            .SelectMany(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<GenericNameSyntax>())
            .Where(n => n.Identifier.ValueText is "Read" or "Write" && n.TypeArgumentList.Arguments.Count == 2)
            .ToArray();
        Assert.Equal(2, calls.Length);
        Assert.All(calls, call => Assert.Equal(
            "global::PolyType.FastPathJson.Converters." + expectedName, call.TypeArgumentList.Arguments[1].ToString()));
    }

    [Theory]
    [InlineData("Numbers", "NumbersConverter")]
    [InlineData("Box<int>", "Box_Int32Converter")]
    [InlineData("int[,]", "Array2D_Int32Converter")]
    [InlineData("(int, string)", "ValueTuple_Int32_StringConverter")]
    [InlineData("@class", "__Type_classConverter")]
    [InlineData("Outer.Inner", "InnerConverter")]
    [InlineData("Boolean", "TestNamespace_BooleanConverter")]
    [InlineData("DateTime", "DateTimeConverter")]
    [InlineData("Guid", "GuidConverter")]
    public static void ConverterNamesDescribeSerializedTypes(string type, string expectedName)
    {
        string source = $$"""
            using System.Collections.Generic;
            namespace TestNamespace;

            public class Numbers : List<int> { }
            public record Box<T>(T Value);
            public class @class { public int Value { get; set; } }
            public class Outer { public class Inner { public int Value { get; set; } } }
            public record Boolean(bool Value);
            public record DateTime(int Value);
            public record Guid(int Value);

            public record Root<T>(T Value);

            [GenerateJsonFor(typeof(Root<{{type}}>))]
            public partial class Witness;
            """;
        Assert.Contains(expectedName, GetConverterNames(source));
    }

    [Theory]
    [InlineData("string", "StringConverter")]
    [InlineData("byte[]", "Array_ByteConverter")]
    [InlineData("System.Uri", "UriConverter")]
    public static void NullableScalarPropertiesUseTheSameRuntimeWitness(string type, string expectedName)
    {
        string source = $$"""
            namespace TestNamespace;

            [GenerateJson]
            public partial class Payload
            {
                public {{type}}? First { get; set; }
                public {{type}}? Second { get; set; }
            }
            """;
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        GenericNameSyntax[] calls = result.Driver.GetRunResult().Results.Single().GeneratedSources
            .SelectMany(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<GenericNameSyntax>())
            .Where(n => n.Identifier.ValueText is "Read" or "Write" && n.TypeArgumentList.Arguments.Count == 2)
            .ToArray();
        Assert.Equal(4, calls.Length);
        Assert.All(calls, call => Assert.Equal(
            "global::PolyType.FastPathJson.Converters." + expectedName, call.TypeArgumentList.Arguments[1].ToString()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ConflictingNamesUseNamespaceQualification(bool reverseAttributes)
    {
        string[] attributes = ["[GenerateJsonFor(typeof(Root<A.Item>))]", "[GenerateJsonFor(typeof(Root<B.Item>))]"];
        if (reverseAttributes)
        {
            Array.Reverse(attributes);
        }

        string source = $$"""
            namespace A { public record Item(int Value); }
            namespace B { public record Item(int Value); }
            namespace TestNamespace
            {
                public record Root<T>(T Value);
                {{string.Join("\n", attributes)}}
                public partial class Witness;
            }
            """;
        Assert.Equal(["A_ItemConverter", "B_ItemConverter"], GetConverterNames(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void FullyQualifiedIdentifierCollisionsHaveDeterministicSuffixes(bool reverseAttributes)
    {
        string[] attributes = ["[GenerateJsonFor(typeof(Root<A.B.Item>))]", "[GenerateJsonFor(typeof(Root<A_B.Item>))]"];
        if (reverseAttributes)
        {
            Array.Reverse(attributes);
        }

        string source = $$"""
            namespace A.B { public record Item(int Value); }
            namespace A_B { public record Item(int Value); }
            namespace TestNamespace
            {
                public record Root<T>(T Value);
                {{string.Join("\n", attributes)}}
                public partial class Witness;
            }
            """;
        Assert.Equal(["A_B_ItemConverter", "A_B_ItemConverter_1"], GetConverterNames(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CaseInsensitiveIdentifierCollisionsHaveDeterministicSuffixes(bool reverseAttributes)
    {
        string[] attributes = ["[GenerateJsonFor(typeof(Root<Model>))]", "[GenerateJsonFor(typeof(Root<MODEL>))]"];
        if (reverseAttributes)
        {
            Array.Reverse(attributes);
        }

        string source = $$"""
            namespace TestNamespace;
            public record Model(int Value);
            public record MODEL(int Value);
            public record Root<T>(T Value);

            {{string.Join("\n", attributes)}}
            public partial class Witness;
            """;
        Assert.Equal(["MODELConverter", "ModelConverter_1"], GetConverterNames(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnrelatedRootsDoNotRenameExistingConverters(bool includeUnrelated)
    {
        string source = $$"""
            namespace TestNamespace;
            public record Payload(int Value);
            public record Additional(bool Value);
            public record Root<T>(T Value);

            [GenerateJsonFor(typeof(Root<Payload>))]
            {{(includeUnrelated ? "[GenerateJsonFor(typeof(Root<Additional>))]" : "")}}
            public partial class Witness;
            """;
        Assert.Contains("PayloadConverter", GetConverterNames(source));
    }

    [Theory]
    [InlineData(false, "Node", "Node")]
    [InlineData(true, "JsonWitness", "JsonWitness")]
    [InlineData(false, "@class", "__Type_class")]
    [InlineData(false, "Helpers", "Helpers")]
    [InlineData(false, "helpers", "helpers")]
    [InlineData(false, "Converters", "Converters")]
    public static void RootSourceFilesDescribeAnnotatedTypes(bool externalType, string typeName, string identifier)
    {
        string source = $$"""
            namespace TestNamespace;
            public record Payload(int Value);

            {{(externalType ? "[GenerateJsonFor<Payload>]" : "[GenerateJson]")}}
            public partial class {{typeName}}
            {
                public int Value { get; set; }
            }
            """;
        string targetSuffix = externalType ? ".Payload" : "";
        string[] expected = [$"FastPathJson.{identifier}{targetSuffix}.g.cs"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), GetGeneratedFileNames(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void WitnessImplementationsUseSerializedTypeSuffixes(bool reverseAttributes)
    {
        string[] attributes =
        [
            "[GenerateJsonFor(typeof(int[,]))]",
            "[GenerateJsonFor(typeof(int[,,]))]",
            "[GenerateJsonFor<System.Collections.Generic.List<int>>]",
        ];
        if (reverseAttributes)
        {
            Array.Reverse(attributes);
        }

        string source = $$"""
            namespace TestNamespace;
            {{string.Join("\n", attributes)}}
            public partial class Witness;
            """;
        string[] expected =
        [
            "FastPathJson.Witness.Array2D_Int32.g.cs",
            "FastPathJson.Witness.Array3D_Int32.g.cs",
            "FastPathJson.Witness.List_Int32.g.cs",
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), GetGeneratedFileNames(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void WitnessTargetFilenameCollisionsAreDisambiguated(bool reverseAttributes)
    {
        string[] attributes =
        [
            "[GenerateJsonFor<A.B.Item>]",
            "[GenerateJsonFor<A_B.Item>]",
            "[GenerateJsonFor<MODEL>]",
            "[GenerateJsonFor<Model>]",
        ];
        if (reverseAttributes)
        {
            Array.Reverse(attributes);
        }

        string source = $$"""
            namespace A.B { public record Item(int Value); }
            namespace A_B { public record Item(int Value); }
            namespace TestNamespace
            {
                public record MODEL(int Value);
                public record Model(int Value);
                {{string.Join("\n", attributes)}}
                public partial class Witness;
            }
            """;
        string[] expected =
        [
            "FastPathJson.Witness.A_B_Item.g.cs",
            "FastPathJson.Witness.A_B_Item_1.g.cs",
            "FastPathJson.Witness.MODEL.g.cs",
            "FastPathJson.Witness.Model_1.g.cs",
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), GetGeneratedFileNames(source));
    }

    [Theory]
    [InlineData("Converters")]
    [InlineData("converters")]
    public static void WitnessFilesDoNotCollideWithInternalConverterFiles(string witnessName)
    {
        string source = $$"""
            namespace TestNamespace;
            public record Payload(int Value);
            public record PayloadConverter(Payload Value);

            [GenerateJsonFor<PayloadConverter>]
            public partial class {{witnessName}};
            """;
        string[] expected =
        [
            "FastPathJson.Converters.PayloadConverter.g.cs",
            $"FastPathJson.{witnessName}.PayloadConverter_1.g.cs",
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), GetGeneratedFileNames(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void AddingWitnessTargetsDoesNotRenameExistingImplementations(bool includeAdditional)
    {
        string source = $$"""
            namespace TestNamespace;
            [GenerateJsonFor(typeof(int[,]))]
            {{(includeAdditional ? "[GenerateJsonFor<string>]" : "")}}
            public partial class Witness;
            """;
        Assert.Contains("FastPathJson.Witness.Array2D_Int32.g.cs", GetGeneratedFileNames(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void SelfImplementationsKeepTheirFilenameWhenAddingTargets(bool includeAdditional)
    {
        string source = $$"""
            namespace TestNamespace;
            [GenerateJson]
            {{(includeAdditional ? "[GenerateJsonFor<string>]" : "")}}
            public partial record Node(int Value);
            """;
        string[] expected = includeAdditional
            ? ["FastPathJson.Node.g.cs", "FastPathJson.Node.String.g.cs"]
            : ["FastPathJson.Node.g.cs"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), GetGeneratedFileNames(source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void RootSourceFileCollisionsAreDisambiguatedDeterministically(bool reverseDeclarations)
    {
        string[] declarations =
        [
            "namespace A.B { [GenerateJson] public partial record Node(int Value); }",
            "namespace A_B { [GenerateJson] public partial record Node(int Value); }",
            "namespace TestNamespace { public partial class Outer1 { [GenerateJson] public partial record Node(int Value); } }",
            "namespace TestNamespace { public partial class Outer2 { [GenerateJson] public partial record Node(int Value); } }",
            "namespace TestNamespace { [GenerateJson] public partial record MODEL(int Value); }",
            "namespace TestNamespace { [GenerateJson] public partial record Model(int Value); }",
        ];
        if (reverseDeclarations)
        {
            Array.Reverse(declarations);
        }

        string[] expected =
        [
            "FastPathJson.A_B_Node.g.cs",
            "FastPathJson.A_B_Node_1.g.cs",
            "FastPathJson.TestNamespace_TestNamespace_Outer1_Node.g.cs",
            "FastPathJson.TestNamespace_TestNamespace_Outer2_Node.g.cs",
            "FastPathJson.MODEL.g.cs",
            "FastPathJson.Model_1.g.cs",
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), GetGeneratedFileNames(string.Join("\n", declarations)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnrelatedRootsDoNotRenameExistingSourceFiles(bool includeUnrelated)
    {
        string source = $$"""
            namespace TestNamespace;
            [GenerateJson]
            public partial record Node(int Value);

            {{(includeUnrelated ? "[GenerateJson] public partial record Additional(bool Value);" : "")}}
            """;
        Assert.Contains("FastPathJson.Node.g.cs", GetGeneratedFileNames(source));
    }

    private static string[] GetGeneratedFileNames(string source)
    {
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));

        return result.Driver.GetRunResult().Results.Single().GeneratedSources
            .Select(s => s.HintName)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetConverterNames(string source)
    {
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        return result.Driver.GetRunResult().Results.Single().GeneratedSources
            .SelectMany(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(c => c.Parent is ClassDeclarationSyntax { Identifier.ValueText: "GeneratedJsonConverters" })
            .Select(c => c.Identifier.ValueText)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }
}
