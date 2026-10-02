using Microsoft.CodeAnalysis;
using PolyType.SourceGenerator.Model;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class GenerateShapeClosedInferenceTests
{
    [Theory]
    [InlineData("self", "true")]
    [InlineData("generic", "true")]
    [InlineData("type", "true")]
    [InlineData("pattern", "true")]
    [InlineData("self", "false")]
    [InlineData("generic", "false")]
    [InlineData("type", "false")]
    [InlineData("pattern", "false")]
    [InlineData("self", null)]
    [InlineData("generic", null)]
    [InlineData("type", null)]
    [InlineData("pattern", null)]
    public static void GenerateShapeClosedInferenceUsesExplicitSetting(string form, string? value)
    {
        PolyTypeSourceGeneratorResult result = Compile(CreateSource(form, value));
        TypeShapeModel root = Assert.Single(result.AllGeneratedTypes, model => model.Type.FullyQualifiedName == "global::Root");
        if (value is "true")
        {
            UnionShapeModel union = Assert.IsType<UnionShapeModel>(root);
            Assert.Equal("Leaf", Assert.Single(union.UnionCases).Name);
        }
        else
        {
            Assert.IsType<ObjectShapeModel>(root);
        }

        Assert.DoesNotContain(result.AllGeneratedTypes.OfType<UnionShapeModel>(),
            model => model.Type.FullyQualifiedName == "global::Witness");
    }

    [Theory]
    [InlineData("self", null)]
    [InlineData("generic", null)]
    [InlineData("type", null)]
    [InlineData("pattern", null)]
    [InlineData("self", "true")]
    [InlineData("generic", "true")]
    [InlineData("type", "true")]
    [InlineData("pattern", "true")]
    public static void GenerateShapeClosedInferenceOmissionDoesNotConflict(string form, string? value)
    {
        PolyTypeSourceGeneratorResult result = Compile(CreateSource(form, value, "[TypeShape(InferClosedTypePolymorphism = true)]"));
        UnionShapeModel union = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        Assert.Equal("Leaf", Assert.Single(union.UnionCases).Name);
    }

    [Theory]
    [InlineData("self")]
    [InlineData("generic")]
    [InlineData("type")]
    [InlineData("pattern")]
    public static void GenerateShapeClosedInferenceRejectsConflictingSettings(string form)
    {
        PolyTypeSourceGeneratorResult result = Compile(
            CreateSource(form, "false", "[TypeShape(InferClosedTypePolymorphism = true)]"), validate: false);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "PT0028");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.DoesNotContain(result.AllGeneratedTypes, model => model.Type.FullyQualifiedName == "global::Root");
    }

    [Theory]
    [InlineData("self")]
    [InlineData("generic")]
    [InlineData("type")]
    [InlineData("pattern")]
    public static void GenerateShapeClosedInferenceRejectsNonClosedTargets(string form)
    {
        PolyTypeSourceGeneratorResult result = Compile(
            CreateSource(form, "true").Replace("closed ", ""), validate: false);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "PT0032");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Theory]
    [InlineData("self", "Kind = TypeShapeKind.Object")]
    [InlineData("generic", "Kind = TypeShapeKind.Object")]
    [InlineData("type", "Kind = TypeShapeKind.Object")]
    [InlineData("pattern", "Kind = TypeShapeKind.Object")]
    [InlineData("self", "Marshaler = typeof(object)")]
    [InlineData("generic", "Marshaler = typeof(object)")]
    [InlineData("type", "Marshaler = typeof(object)")]
    [InlineData("pattern", "Marshaler = typeof(object)")]
    public static void GenerateShapeClosedInferenceRejectsIncompatibleSettings(string form, string configuration)
    {
        PolyTypeSourceGeneratorResult result = Compile(CreateSource(form, "true, " + configuration), validate: false);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "PT0028");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Theory]
    [InlineData("self")]
    [InlineData("generic")]
    [InlineData("type")]
    [InlineData("pattern")]
    public static void GenerateShapeClosedInferenceRejectsConflictingExtensions(string form)
    {
        string source = CreateSource(form, "false").Replace("using PolyType;", """
            using PolyType;
            [assembly: TypeShapeExtension(typeof(Root), InferClosedTypePolymorphism = true)]
            """);
        PolyTypeSourceGeneratorResult result = Compile(source, validate: false);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "PT0028");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Theory]
    [InlineData("generic")]
    [InlineData("type")]
    [InlineData("pattern")]
    public static void GenerateShapeClosedInferenceSupportsReferencedTargets(string form)
    {
        Compilation library = CompilationTests.CreateClosedClassCompilation("""
            public closed class Root;
            public sealed class Zebra : Root;
            public sealed class Antelope : Root;
            """, assemblyName: "ClosedLibrary");
        using MemoryStream image = new();
        var emit = library.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        string source = "using PolyType;" + Environment.NewLine + GetAnnotation(form, "true") + Environment.NewLine +
            "public partial class Witness;";
        Compilation compilation = CompilationTests.CreateClosedClassCompilation(
            source, additionalReferences: [MetadataReference.CreateFromImage(image.ToArray())]);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        UnionShapeModel union = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        Assert.Equal(new[] { "Zebra", "Antelope" }, union.UnionCases.OrderBy(model => model.Index).Select(model => model.Name));
    }

    [Theory]
    [InlineData(null, "Root")]
    [InlineData("false", "Root")]
    [InlineData("true", "Root")]
    [InlineData(null, "R*")]
    [InlineData("false", "R*")]
    [InlineData("true", "R*")]
    public static void GenerateShapeClosedInferencePatternPreservesMethodInclusion(string? infer, string pattern)
    {
        string setting = infer is null ? "" : ", InferClosedTypePolymorphism = " + infer;
        PolyTypeSourceGeneratorResult result = Compile($$"""
            using PolyType;

            [GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
            public closed partial class Root
            {
                public int M() => 42;
            }

            public sealed class Leaf : Root;

            [GenerateShapeFor("{{pattern}}"{{setting}})]
            public partial class Witness;
            """);

        TypeShapeModel root = Assert.Single(result.AllGeneratedTypes, model => model.Type.FullyQualifiedName == "global::Root");
        Assert.Equal(new[] { "M" }, root.Methods.Select(method => method.Name));
        Assert.Equal(infer is "true", root is UnionShapeModel);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void GenerateShapeClosedInferencePatternHonorsExplicitMethodInclusion(bool infer, bool includeMethods)
    {
        PolyTypeSourceGeneratorResult result = Compile($$"""
            using PolyType;

            [GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
            public closed partial class Root
            {
                public int M() => 42;
                public static int StaticM() => 43;
            }

            public sealed class Leaf : Root;

            [GenerateShapeFor("R*", InferClosedTypePolymorphism = {{(infer ? "true" : "false")}}, IncludeMethods = MethodShapeFlags.{{(includeMethods ? "AllPublic" : "None")}})]
            public partial class Witness;
            """);

        TypeShapeModel root = Assert.Single(result.AllGeneratedTypes, model => model.Type.FullyQualifiedName == "global::Root");
        Assert.Equal(includeMethods ? new[] { "M", "StaticM" } : Array.Empty<string>(), root.Methods.Select(method => method.Name));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void GenerateShapeClosedInferenceOverlappingPatternsPreserveMethodInclusion(bool infer)
    {
        PolyTypeSourceGeneratorResult result = Compile($$"""
            using PolyType;

            [GenerateShape]
            public closed partial class Root
            {
                public int M() => 42;
            }

            public sealed class Leaf : Root;

            [GenerateShapeFor("Root", InferClosedTypePolymorphism = {{(infer ? "true" : "false")}}, IncludeMethods = MethodShapeFlags.PublicInstance)]
            [GenerateShapeFor("R*", InferClosedTypePolymorphism = {{(infer ? "true" : "false")}})]
            public partial class Witness;
            """);

        TypeShapeModel root = Assert.Single(result.AllGeneratedTypes, model => model.Type.FullyQualifiedName == "global::Root");
        Assert.Equal(new[] { "M" }, root.Methods.Select(method => method.Name));
    }

    private static PolyTypeSourceGeneratorResult Compile(string source, bool validate = true) =>
        CompilationHelpers.RunPolyTypeSourceGenerator(CompilationTests.CreateClosedClassCompilation(source), disableDiagnosticValidation: !validate);

    private static string CreateSource(string form, string? value, string configuration = "")
    {
        string annotation = GetAnnotation(form, value);
        string rootDeclaration = form is "self"
            ? annotation + Environment.NewLine + configuration + Environment.NewLine + "public closed partial class Root;"
            : configuration + Environment.NewLine + "public closed class Root;";
        string witness = form is "self" ? "" : annotation + Environment.NewLine + "public partial class Witness;";
        return $$"""
            using PolyType;
            {{rootDeclaration}}
            public sealed class Leaf : Root;
            {{witness}}
            """;
    }

    private static string GetAnnotation(string form, string? value)
    {
        string setting = value is null ? "" : "InferClosedTypePolymorphism = " + value;
        string namedArgument = value is null ? "" : ", " + setting;
        return form switch
        {
            "self" => $"[GenerateShape({setting})]",
            "generic" => $"[GenerateShapeFor<Root>({setting})]",
            "type" => $"[GenerateShapeFor(typeof(Root){namedArgument})]",
            "pattern" => $"[GenerateShapeFor(\"Root\"{namedArgument})]",
            _ => throw new ArgumentOutOfRangeException(nameof(form)),
        };
    }
}
