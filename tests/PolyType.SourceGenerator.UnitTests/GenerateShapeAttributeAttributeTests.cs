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

                [assembly: GenerateShapeAttributeAttribute(typeof(ContractAttribute), IncludeMethods = MethodShapeFlags.PublicInstance)]
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

                [assembly: GenerateShapeAttributeAttribute(typeof(ContractAttribute), {{settings}})]
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
                [assembly: GenerateShapeAttributeAttribute(typeof(ContractAttribute), InferClosedTypePolymorphism = true)]
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
                [assembly: GenerateShapeAttributeAttribute(typeof(ContractAttribute))]
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
            [assembly: GenerateShapeAttributeAttribute(typeof(ContractAttribute), IncludeMethods = MethodShapeFlags.PublicInstance)]
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
                [assembly: GenerateShapeAttributeAttribute(typeof(ContractAttribute<>))]
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

    [Fact]
    public static void DirectGenerationWorksWhenMetaAttributeTypeIsUnavailable()
    {
        Compilation compilation = CreateCompilation("""
            using System;

            [PolyType.GenerateShape]
            public partial class ContractType { }

            [Obsolete]
            public partial class UnrelatedType { }

            namespace PolyType
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class GenerateShapeAttribute : Attribute { }
            }
            """);
        MetadataReference polyTypeReference = Assert.Single(
            compilation.References,
            reference => reference.Display == typeof(PolyType.ITypeShape).Assembly.Location);
        compilation = compilation.RemoveReferences(polyTypeReference);
        compilation.GetDiagnostics(TestContext.Current.CancellationToken).AssertMaxSeverity(DiagnosticSeverity.Info);

        GeneratorDriver driver = CompilationHelpers.CreatePolyTypeSourceGeneratorDriver(compilation);
        GeneratorDriverRunResult result = driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();
        result.Diagnostics.AssertMaxSeverity(DiagnosticSeverity.Info);
        Assert.Contains(result.GeneratedTrees, tree => tree.FilePath.EndsWith("ContractType.g.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(result.GeneratedTrees, tree => tree.FilePath.EndsWith("UnrelatedType.g.cs", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CustomAttributeRegistrationChangesInvalidateGeneration(bool initiallyMarked)
    {
        const string marker = "[assembly: PolyType.Abstractions.GenerateShapeAttribute(typeof(ContractAttribute))]";
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

    [Theory]
    [InlineData("[Contract]", "")]
    [InlineData("[ContractAttribute]", "")]
    [InlineData("[global::Contracts.Contract]", "")]
    [InlineData("[Alias]", "using Alias = Contracts.ContractAttribute;")]
    [InlineData("[Alias]", "global using Alias = Contracts.ContractAttribute;")]
    public static void RegisteredAttributeNamesAndAliasesAreResolved(string annotation, string usingDirective)
    {
        Compilation compilation = CreateCompilation("""
            using System;
            [assembly: PolyType.Abstractions.GenerateShapeAttribute(typeof(Contracts.ContractAttribute))]
            namespace Contracts { public sealed class ContractAttribute : Attribute; }
            namespace Other { public sealed class ContractAttribute : Attribute; }
            """);
        CSharpParseOptions options = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        compilation = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText($$"""
                {{usingDirective}}
                using Contracts;
                {{annotation}}
                public partial class ContractType;
                [Other.Contract]
                public partial class UnrelatedType;
                """, options, cancellationToken: TestContext.Current.CancellationToken));

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Equal("global::ContractType", Assert.Single(result.AllGeneratedTypes).Type.FullyQualifiedName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[GenerateShapeFor<int>]")]
    [InlineData("[GenerateShapeFor<int>, GenerateShapeFor<string>]")]
    public static void FirstCustomAttributeSuppliesDefaultsWithoutDroppingWitnesses(string witnessAnnotations)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCompilation($$"""
            using System;
            using PolyType;
            using PolyType.Abstractions;
            [assembly: GenerateShapeAttributeAttribute(typeof(FirstAttribute), IncludeMethods = MethodShapeFlags.PublicInstance)]
            [assembly: GenerateShapeAttributeAttribute(typeof(SecondAttribute))]
            public sealed class FirstAttribute : Attribute;
            public sealed class SecondAttribute : Attribute;
            [First, Second]
            {{witnessAnnotations}}
            public partial class ContractType
            {
                public int Method() => 42;
            }
            """));
        TypeShapeModel model = Assert.Single(result.AllGeneratedTypes, m => m.Type.FullyQualifiedName == "global::ContractType");
        Assert.Equal("Method", Assert.Single(model.Methods).Name);
        if (witnessAnnotations.Length > 0)
        {
            Assert.Contains(result.AllGeneratedTypes, m => m.Type.FullyQualifiedName == "int");
        }

        if (witnessAnnotations.Contains("string"))
        {
            Assert.Contains(result.AllGeneratedTypes, m => m.Type.FullyQualifiedName == "string");
        }
    }

    [Fact]
    public static void CurrentAssemblyRegistrationOverridesReferencedDefaults()
    {
        Compilation library = CreateCompilation("""
            using System;
            [assembly: PolyType.Abstractions.GenerateShapeAttribute(typeof(ContractAttribute), IncludeMethods = PolyType.MethodShapeFlags.PublicInstance)]
            public sealed class ContractAttribute : Attribute;
            """, assemblyName: "Contracts");
        using MemoryStream image = new();
        Assert.True(library.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCompilation("""
            [assembly: PolyType.Abstractions.GenerateShapeAttribute(typeof(ContractAttribute))]
            [Contract]
            public partial class ContractType
            {
                public int Method() => 42;
            }
            """, additionalReferences: [MetadataReference.CreateFromImage(image.ToArray())]));
        Assert.Empty(Assert.Single(result.AllGeneratedTypes).Methods);
    }

    [Fact]
    public static void UnregisteredAttributesDoNotTriggerGeneration()
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCompilation("""
            using System;
            [assembly: PolyType.Abstractions.GenerateShapeAttribute(typeof(RegisteredAttribute))]
            public sealed class RegisteredAttribute : Attribute;
            public sealed class UnregisteredAttribute : Attribute;
            [Unregistered]
            public partial class ContractType;
            """));
        Assert.Empty(result.AllGeneratedTypes);
    }

    [Fact]
    public static void GlobalAliasInAnotherFileTriggersGeneration()
    {
        Compilation compilation = CreateCompilation("""
            global using Alias = Contracts.ContractAttribute;
            using System;
            [assembly: PolyType.Abstractions.GenerateShapeAttribute(typeof(Contracts.ContractAttribute))]
            namespace Contracts { public sealed class ContractAttribute : Attribute; }
            """);
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            [Alias]
            public partial interface IContract;
            """, (CSharpParseOptions)compilation.SyntaxTrees.First().Options,
            cancellationToken: TestContext.Current.CancellationToken));

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Equal("global::IContract", Assert.Single(result.AllGeneratedTypes).Type.FullyQualifiedName);
    }

    [Fact]
    public static void UnrelatedEditDoesNotRegenerateRegisteredShapes()
    {
        Compilation compilation = CreateCompilation("""
            using System;
            [assembly: PolyType.Abstractions.GenerateShapeAttribute(typeof(ContractAttribute))]
            public sealed class ContractAttribute : Attribute;
            [Contract]
            public partial interface IContract { int Method(); }
            """);
        CSharpParseOptions options = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        SyntaxTree unrelated = CSharpSyntaxTree.ParseText("public class Unrelated { }", options,
            cancellationToken: TestContext.Current.CancellationToken);
        compilation = compilation.AddSyntaxTrees(unrelated);
        int generated = 0;
        GeneratorDriver driver = CompilationHelpers.CreatePolyTypeSourceGeneratorDriver(compilation,
            new PolyTypeGenerator { OnGeneratingSource = _ => generated++ });
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.Equal(1, generated);

        compilation = compilation.ReplaceSyntaxTree(unrelated, CSharpSyntaxTree.ParseText("public class Unrelated { public int Value; }", options,
            cancellationToken: TestContext.Current.CancellationToken));
        driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.Equal(1, generated);
    }

    private static Compilation CreateCompilation(
        [StringSyntax("c#-test")] string source,
        MetadataReference[]? additionalReferences = null,
        string assemblyName = "TestAssembly") =>
        CompilationHelpers.CreateCompilation(source, additionalReferences, assemblyName,
            parseOptions: CompilationHelpers.CreateParseOptions(LanguageVersion.CSharp12));
}
