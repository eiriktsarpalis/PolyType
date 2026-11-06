using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PolyType.SourceGenerator.Model;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class GenerateShapeAttributeAttributeTests
{
    [Theory]
    [InlineData("class", "")]
    [InlineData("record", "")]
    [InlineData("struct", "")]
    [InlineData("record struct", "")]
    [InlineData("interface", "")]
    [InlineData("class", "[GenerateShape]")]
    [InlineData("class", "[GenerateShape(IncludeMethods = MethodShapeFlags.None)]")]
    public static void CustomAttributeTriggersGeneration(string kind, string explicitAnnotation)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CreateCompilation($$"""
                using System;
                using PolyType;
                using PolyType.Abstractions;
                using ContractAlias = ContractAttribute;

                [GenerateShapeAttributeAttribute(IncludeMethods = MethodShapeFlags.PublicInstance)]
                public sealed class ContractAttribute : Attribute;

                [ContractAlias]
                {{explicitAnnotation}}
                public partial {{kind}} ContractType
                {
                    {{(kind is "interface" ? "int Method();" : "public int Method() => 42;")}}
                }

                [ContractAlias]
                public partial {{kind}} OtherType;
                """));

        TypeShapeModel model = Assert.Single(result.AllGeneratedTypes, m => m.Type.FullyQualifiedName == "global::ContractType");
        Assert.Equal(explicitAnnotation.Length == 0 ? ["Method"] : Array.Empty<string>(), model.Methods.Select(m => m.Name));
        Assert.Contains(result.AllGeneratedTypes, m => m.Type.FullyQualifiedName == "global::OtherType");
        Assert.DoesNotContain(result.AllGeneratedTypes, m => m.Type.FullyQualifiedName == "global::ContractAttribute");
    }

    [Theory]
    [InlineData("Kind = TypeShapeKind.None", typeof(ObjectShapeModel))]
    [InlineData("Kind = TypeShapeKind.Object", typeof(ObjectShapeModel))]
    [InlineData("Marshaler = typeof(Marshaler)", typeof(SurrogateShapeModel))]
    public static void CustomAttributeConfiguresShape(string settings, Type expectedModel)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CreateCompilation($$"""
                using System;
                using PolyType;
                using PolyType.Abstractions;

                [GenerateShapeAttributeAttribute({{settings}})]
                public sealed class ContractAttribute : Attribute;

                [Contract]
                public partial class ContractType
                {
                    public int Value { get; set; }
                }

                public sealed class Marshaler : IMarshaler<ContractType, string>
                {
                    public string? Marshal(ContractType? value) => "";
                    public ContractType? Unmarshal(string? value) => new();
                }
                """));

        TypeShapeModel model = Assert.Single(result.AllGeneratedTypes, m => m.Type.FullyQualifiedName == "global::ContractType");
        Assert.IsType(expectedModel, model);
        if (model is ObjectShapeModel objectModel)
        {
            Assert.Equal(settings is "Kind = TypeShapeKind.None" ? 0 : 1, objectModel.Properties.Length);
        }
    }

    [Fact]
    public static void CustomAttributeSupportsClosedInference()
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CompilationTests.CreateClosedClassCompilation("""
                using System;
                using PolyType.Abstractions;
                [GenerateShapeAttributeAttribute(InferClosedTypePolymorphism = true)]
                public sealed class ContractAttribute : Attribute;
                [Contract]
                public closed partial class Root;
                public sealed class Leaf : Root;
                """));
        UnionShapeModel root = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        Assert.Equal("Leaf", Assert.Single(root.UnionCases).Name);
    }

    [Theory]
    [InlineData("class", "PT0002")]
    [InlineData("static partial class", "PT0007")]
    public static void CustomAttributeValidatesTarget(string declaration, string diagnosticId)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CreateCompilation($$"""
                using System;
                using PolyType.Abstractions;
                [GenerateShapeAttributeAttribute]
                public sealed class ContractAttribute : Attribute;
                [Contract]
                public {{declaration}} ContractType;
                """), disableDiagnosticValidation: true);
        Assert.Contains(result.Diagnostics, d => d.Id == diagnosticId);
    }

    [Fact]
    public static void CustomAttributeWorksFromMetadata()
    {
        Compilation library = CreateCompilation("""
            using System;
            using PolyType;
            using PolyType.Abstractions;
            [GenerateShapeAttributeAttribute(IncludeMethods = MethodShapeFlags.PublicInstance)]
            public sealed class ContractAttribute : Attribute;
            """, assemblyName: "Contracts");
        using MemoryStream image = new();
        var emit = library.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CreateCompilation("""
                [Contract]
                public partial interface IContract
                {
                    int Method();
                }
                """, additionalReferences: [MetadataReference.CreateFromImage(image.ToArray())]));
        TypeShapeModel model = Assert.Single(result.AllGeneratedTypes, m => m.Type.FullyQualifiedName == "global::IContract");
        Assert.Equal("Method", Assert.Single(model.Methods).Name);
    }

    [Fact]
    public static void CustomAttributeHandlesPartialNestedAndGenericAnnotations()
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CreateCompilation("""
                using System;
                using PolyType.Abstractions;
                [GenerateShapeAttributeAttribute]
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                public sealed class ContractAttribute<T> : Attribute;
                public partial class Outer
                {
                    [Contract<int>]
                    public partial record Inner;
                    [Contract<string>]
                    public partial record Inner;
                }
                """));
        Assert.Single(result.AllGeneratedTypes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CustomAttributeCacheIsInvalidatedWhenDeclarationChanges(bool initiallyMarked)
    {
        const string marker = "[PolyType.Abstractions.GenerateShapeAttribute]";
        string Source(bool marked) => $$"""
            using System;
            {{(marked ? marker : "")}}
            public sealed class ContractAttribute : Attribute;
            """;
        Compilation compilation = CreateCompilation(Source(initiallyMarked));
        SyntaxTree tree = compilation.SyntaxTrees.First();
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            [Contract]
            public partial class ContractType;
            """, (CSharpParseOptions)tree.Options, cancellationToken: TestContext.Current.CancellationToken));
        GeneratorDriver driver = CompilationHelpers.CreatePolyTypeSourceGeneratorDriver(compilation);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.Equal(initiallyMarked, driver.GetRunResult().GeneratedTrees.Any());

        GeneratorDriver cached = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.All(cached.GetRunResult().Results.SelectMany(r => r.TrackedOutputSteps.Values).SelectMany(s => s).SelectMany(s => s.Outputs),
            output => Assert.Equal(IncrementalStepRunReason.Cached, output.Reason));

        compilation = compilation.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(Source(!initiallyMarked), (CSharpParseOptions)tree.Options, cancellationToken: TestContext.Current.CancellationToken));
        driver = cached.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.Equal(!initiallyMarked, driver.GetRunResult().GeneratedTrees.Any());
    }

    private static Compilation CreateCompilation(
        [StringSyntax("c#-test")] string source,
        MetadataReference[]? additionalReferences = null,
        string assemblyName = "TestAssembly") =>
        CompilationHelpers.CreateCompilation(source, additionalReferences, assemblyName,
            parseOptions: CompilationHelpers.CreateParseOptions(LanguageVersion.CSharp12));
}
