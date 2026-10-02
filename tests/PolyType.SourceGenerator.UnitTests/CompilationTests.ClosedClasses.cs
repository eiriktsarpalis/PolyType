using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PolyType.SourceGenerator.Model;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    internal const string ClosedHierarchySource = """
        using PolyType;

        namespace TestNamespace
        {
            [GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
            public closed partial class Root
            {
                public int Number { get; set; }
            }

            public sealed class Zebra : Root;
            [TypeShape(InferClosedTypePolymorphism = false)]
            public closed class Middle : Root;
            public sealed class Collie : Middle;
            public sealed class Antelope : Root;
            [GenerateShape, TypeShape(InferClosedTypePolymorphism = true, Kind = TypeShapeKind.Union)]
            public closed partial class Empty;
        }
        """;

    private const string ClosedTypeContract = """
        namespace System.Runtime.CompilerServices
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
            public sealed class IsClosedTypeAttribute : System.Attribute
            {
                public System.Type[] DerivedTypes { get; set; } = System.Array.Empty<System.Type>();
            }
        }
        """;

    [Theory]
    [InlineData("internal", "internal", true)]
    [InlineData("protected internal", "protected internal", true)]
    [InlineData("public", "public", true)]
    [InlineData("public", "protected", false)]
    [InlineData("public", "private protected", false)]
    [InlineData("public", "protected internal", false)]
    [InlineData("public", "internal", false)]
    [InlineData("public", "private", false)]
    [InlineData("protected internal", "protected", false)]
    [InlineData("protected internal", "internal", false)]
    [InlineData("internal", "private", false)]
    public static void ClosedHierarchyVisibilityUsesTheEntireContainingScope(string rootAccessibility, string leafAccessibility, bool visible)
    {
        Compilation compilation = CreateClosedClassCompilation($$"""
            using PolyType;

            public partial class Container
            {
                [TypeShape(InferClosedTypePolymorphism = true)]
                {{rootAccessibility}} closed class Root;
                {{leafAccessibility}} sealed class Leaf : Root;

                [GenerateShapeFor(typeof(Root))]
                public partial class Witness;
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Equal(!visible, result.Diagnostics.Any(d => d.Id == "PT0029"));
        if (visible)
        {
            result.Diagnostics.AssertMaxSeverity(DiagnosticSeverity.Info);
            UnionShapeModel root = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>(),
                m => m.Type.FullyQualifiedName == "global::Container.Root");
            Assert.Equal("Leaf", Assert.Single(root.UnionCases).Name);
        }
        else
        {
            Assert.DoesNotContain(result.AllGeneratedTypes.OfType<UnionShapeModel>(), m => m.Type.FullyQualifiedName == "global::Container.Root");
        }
    }

    [Theory]
    [InlineData(ClosedHierarchySource, "global::TestNamespace.Root", 3)]
    [InlineData(ClosedHierarchySource, "global::TestNamespace.Empty", 0)]
    [InlineData("""
        using PolyType;
        [GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
        public closed partial class Root;
        public closed class Middle : Root;
        """, "global::Root", 0)]
    [InlineData("""
        using PolyType;
        [assembly: TypeShapeExtension(typeof(Root), InferClosedTypePolymorphism = true)]
        [GenerateShape, TypeShape]
        public closed partial class Root;
        public abstract class Branch : Root;
        public sealed class Leaf : Branch;
        """, "global::Root", 1)]
    public static void ClosedHierarchyProjectsUnionShape(string source, string rootName, int caseCount)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateClosedClassCompilation(source));
        UnionShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>(), model => model.Type.FullyQualifiedName == rootName);
        Assert.Equal(caseCount, model.UnionCases.Length);
        Assert.IsType<ObjectShapeModel>(model.UnderlyingModel);
        Assert.True(model.UnderlyingModel.IsContextual);
        Assert.All(model.UnionCases, caseModel => Assert.False(caseModel.IsTagSpecified));
        Assert.DoesNotContain(result.AllGeneratedTypes, model => model.Type.FullyQualifiedName is "global::Branch" && model is UnionShapeModel);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public static void ClosedHierarchyReferencedMetadataPreservesCompilerOrder(bool useMetadataReference, bool isGeneric)
    {
        string parameters = isGeneric ? "<T>" : "";
        string argument = isGeneric ? "<int>" : "";
        string declarations = $$"""
            using PolyType;
            public class Container
            {
                [TypeShape(InferClosedTypePolymorphism = true)]
                public closed class Root{{parameters}};
                public sealed class Zebra{{parameters}} : Root{{parameters}};
            }
            public sealed class Antelope{{parameters}} : Container.Root{{parameters}};
            """;
        string witness = $$"""
            [PolyType.GenerateShapeFor(typeof(Container.Root{{argument}}))]
            public partial class Witness;
            """;

        Compilation compilation;
        if (useMetadataReference)
        {
            Compilation library = CreateClosedClassCompilation(declarations, assemblyName: "ClosedLibrary");
            using MemoryStream image = new();
            var emit = library.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
            compilation = CreateClosedClassCompilation(witness, additionalReferences: [MetadataReference.CreateFromImage(image.ToArray())]);
        }
        else
        {
            compilation = CreateClosedClassCompilation(declarations + Environment.NewLine + witness);
        }

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        UnionShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        string[] expectedNames = isGeneric ? ["Zebra_T", "Antelope_T"] : ["Zebra", "Antelope"];
        Assert.Equal(expectedNames, model.UnionCases.OrderBy(caseModel => caseModel.Index).Select(caseModel => caseModel.Name));
        Assert.Equal(new[] { 0, 1 }, model.UnionCases.OrderBy(caseModel => caseModel.Index).Select(caseModel => caseModel.Tag));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ClosedHierarchyEquivalentCompilationsProduceEqualModels(bool metadata)
    {
        MetadataReference[] references = [];
        string source = ClosedHierarchySource;
        if (metadata)
        {
            Compilation library = CreateClosedClassCompilation(
                ClosedHierarchySource.Replace("[GenerateShape, ", "["),
                assemblyName: "ClosedLibrary");
            using MemoryStream image = new();
            var emit = library.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
            references = [MetadataReference.CreateFromImage(image.ToArray())];
            source = """
                [PolyType.GenerateShapeFor(typeof(TestNamespace.Root))]
                public partial class Witness;
                """;
        }

        PolyTypeSourceGeneratorResult first = Compile();
        PolyTypeSourceGeneratorResult second = Compile();
        CompilationHelpers.AssertStructurallyEqual(first.GeneratedModels, second.GeneratedModels);
        Assert.Equal(first.GeneratedModels, second.GeneratedModels);

        PolyTypeSourceGeneratorResult Compile() =>
            CompilationHelpers.RunPolyTypeSourceGenerator(CreateClosedClassCompilation(source, additionalReferences: references));
    }

    [Theory]
    [InlineData("true", "false")]
    [InlineData("Zebra : Root; public sealed class Antelope : Root", "Antelope : Root; public sealed class Zebra : Root")]
    public static void ClosedHierarchyContractChangesInvalidateModels(string first, string second)
    {
        string template = first is "true"
            ? """
                using PolyType;
                [GenerateShape, TypeShape(InferClosedTypePolymorphism = PLACEHOLDER)]
                public closed partial class Root;
                """
            : """
                using PolyType;
                [GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
                public closed partial class Root;
                public sealed class PLACEHOLDER;
                """;
        PolyTypeSourceGeneratorResult left = CompilationHelpers.RunPolyTypeSourceGenerator(
            CreateClosedClassCompilation(template.Replace("PLACEHOLDER", first)));
        PolyTypeSourceGeneratorResult right = CompilationHelpers.RunPolyTypeSourceGenerator(
            CreateClosedClassCompilation(template.Replace("PLACEHOLDER", second)));
        Assert.NotEqual(left.GeneratedModels, right.GeneratedModels);
    }

    [Theory]
    [InlineData("", typeof(ObjectShapeModel))]
    [InlineData(": System.Collections.Generic.List<int>", typeof(EnumerableShapeModel))]
    [InlineData(": System.Collections.Generic.Dictionary<string, int>", typeof(DictionaryShapeModel))]
    public static void ClosedHierarchyTraversalKeepsRegistrationsLocal(string inheritance, Type underlyingModelType)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateClosedClassCompilation($$"""
            using PolyType;

            [GenerateShape(InferClosedTypePolymorphism = true)]
            [AssociatedTypeShape(typeof(Other))]
            public closed partial class Root {{inheritance}};
            public sealed class RootLeaf : Root;

            [TypeShape(InferClosedTypePolymorphism = true)]
            public closed class Other
            {
                public Root? Parent { get; set; }
                public Empty? Empty { get; set; }
                public Ordinary? Ordinary { get; set; }
            }

            public sealed class OtherLeaf : Other;

            [TypeShape(InferClosedTypePolymorphism = true)]
            public closed class Empty;

            public class Ordinary;
            """));

        UnionShapeModel root = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>(),
            model => model.Type.FullyQualifiedName == "global::Root");
        Assert.Equal(underlyingModelType, root.UnderlyingModel.GetType());
        Assert.Equal("RootLeaf", Assert.Single(root.UnionCases).Name);
        UnionShapeModel other = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>(),
            model => model.Type.FullyQualifiedName == "global::Other");
        Assert.Equal("OtherLeaf", Assert.Single(other.UnionCases).Name);
        UnionShapeModel empty = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>(),
            model => model.Type.FullyQualifiedName == "global::Empty");
        Assert.Empty(empty.UnionCases);
        Assert.IsType<ObjectShapeModel>(Assert.Single(result.AllGeneratedTypes,
            model => model.Type.FullyQualifiedName == "global::Ordinary"));
    }

    [Theory]
    [InlineData("DerivedTypeShape", "None")]
    [InlineData("DerivedTypeShape", "Properties")]
    [InlineData("KnownType", "None")]
    [InlineData("KnownType", "Properties")]
    public static void ClosedHierarchyRemappingReportsSuppressionOnce(string attribute, string initialRequirements)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateClosedClassCompilation($$"""
            using PolyType;
            using System.Runtime.Serialization;

            [AssociatedTypeShape(typeof(Root), Requirements = TypeShapeRequirements.{{initialRequirements}})]
            public class Host;

            [TypeShape(InferClosedTypePolymorphism = true)]
            [{{attribute}}(typeof(Leaf))]
            public closed class Root
            {
                public int Number { get; set; }
            }

            public sealed class Leaf : Root;

            [GenerateShapeFor(typeof(Host))]
            [GenerateShapeFor(typeof(Root))]
            public partial class Witness;
            """), disableDiagnosticValidation: true);

        Diagnostic warning = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "PT0030");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        result.Diagnostics.Where(diagnostic => diagnostic.Id != "PT0030").AssertMaxSeverity(DiagnosticSeverity.Info);
        UnionShapeModel root = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        Assert.Equal("Leaf", Assert.Single(root.UnionCases).Name);
        ObjectShapeModel baseModel = Assert.IsType<ObjectShapeModel>(root.UnderlyingModel);
        Assert.Equal("Number", Assert.Single(baseModel.Properties).Name);
        ObjectShapeModel leaf = Assert.IsType<ObjectShapeModel>(Assert.Single(result.AllGeneratedTypes,
            model => model.Type.FullyQualifiedName == "global::Leaf"));
        Assert.NotNull(leaf.Constructor);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Properties")]
    public static void ClosedHierarchyRemappingPreservesEmptyUnion(string initialRequirements)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateClosedClassCompilation($$"""
            using PolyType;

            [AssociatedTypeShape(typeof(Root), Requirements = TypeShapeRequirements.{{initialRequirements}})]
            public class Host;

            [TypeShape(InferClosedTypePolymorphism = true)]
            public closed class Root
            {
                public int Number { get; set; }
            }

            [GenerateShapeFor(typeof(Host))]
            [GenerateShapeFor(typeof(Root))]
            public partial class Witness;
            """));

        UnionShapeModel root = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        Assert.Empty(root.UnionCases);
        ObjectShapeModel baseModel = Assert.IsType<ObjectShapeModel>(root.UnderlyingModel);
        Assert.Equal("Number", Assert.Single(baseModel.Properties).Name);
        Assert.True(baseModel.IsContextual);
    }

    [Theory]
    [InlineData("Marshaler = typeof(object)", "PT0010")]
    [InlineData("InferClosedTypePolymorphism = true", "PT0032")]
    public static void ClosedHierarchyFailedTraversalDoesNotAffectOtherRoots(string leafConfiguration, string diagnosticId)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateClosedClassCompilation($$"""
            using PolyType;

            [TypeShape(InferClosedTypePolymorphism = true)]
            public closed class Root;

            [TypeShape({{leafConfiguration}})]
            public sealed class Leaf : Root;

            [TypeShape(InferClosedTypePolymorphism = true)]
            public closed class Other;

            public class Ordinary;

            [GenerateShapeFor(typeof(Root))]
            [GenerateShapeFor(typeof(Other))]
            [GenerateShapeFor(typeof(Ordinary))]
            public partial class Witness;
            """), disableDiagnosticValidation: true);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PT0031");
        Assert.DoesNotContain(result.AllGeneratedTypes,
            model => model.Type.FullyQualifiedName is "global::Root" or "global::Leaf");
        UnionShapeModel other = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        Assert.Equal("global::Other", other.Type.FullyQualifiedName);
        Assert.Empty(other.UnionCases);
        Assert.IsType<ObjectShapeModel>(Assert.Single(result.AllGeneratedTypes,
            model => model.Type.FullyQualifiedName == "global::Ordinary"));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public static void ClosedInferenceOverridesCSharpUnionPattern(bool metadata, bool infer, bool empty)
    {
        string declarations = $$"""
            using PolyType;

            [System.Runtime.CompilerServices.Union]
            [TypeShape(InferClosedTypePolymorphism = {{(infer ? "true" : "false")}})]
            public closed class Root : Root.IUnionMembers
            {
                protected Root(int number) => Number = number;
                public int Number { get; }
                object IUnionMembers.Value => Number;

                public interface IUnionMembers
                {
                    public static Root Create(int number) => {{(empty ? "throw new System.NotSupportedException()" : "new Leaf(number)")}};
                    object Value { get; }
                }
            }

            {{(empty ? "" : "public sealed class Leaf(int number) : Root(number);")}}
            """;
        const string witness = """
            [PolyType.GenerateShapeFor(typeof(Root))]
            public partial class Witness;
            """;

        Compilation compilation;
        if (metadata)
        {
            Compilation library = AddCSharpUnionContracts(CreateClosedClassCompilation(declarations, assemblyName: "ClosedUnionLibrary"));
            using MemoryStream image = new();
            var emit = library.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
            compilation = CreateClosedClassCompilation(witness, additionalReferences: [MetadataReference.CreateFromImage(image.ToArray())]);
        }
        else
        {
            compilation = AddCSharpUnionContracts(CreateClosedClassCompilation(declarations + Environment.NewLine + witness));
        }

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        TypeShapeModel root = Assert.Single(result.AllGeneratedTypes, model => model.Type.FullyQualifiedName == "global::Root");
        if (infer)
        {
            UnionShapeModel hierarchy = Assert.IsType<UnionShapeModel>(root);
            Assert.IsType<ObjectShapeModel>(hierarchy.UnderlyingModel);
            Assert.True(hierarchy.UnderlyingModel.IsContextual);
            Assert.Equal(empty ? Array.Empty<string>() : new[] { "Leaf" }, hierarchy.UnionCases.Select(caseModel => caseModel.Name));
            Assert.All(hierarchy.UnionCases, caseModel => Assert.False(caseModel.IsTagSpecified));
        }
        else
        {
            CSharpUnionShapeModel payloadUnion = Assert.IsType<CSharpUnionShapeModel>(root);
            Assert.Equal("Int32", Assert.Single(payloadUnion.UnionCases).Name);
        }
    }

    internal static Compilation CreateClosedClassCompilation(
        string source,
        string assemblyName = "TestAssembly",
        MetadataReference[]? additionalReferences = null) =>
        AddClosedTypeContract(CompilationHelpers.CreateCompilation(
            source,
            additionalReferences: additionalReferences,
            assemblyName: assemblyName,
            parseOptions: CompilationHelpers.CreateParseOptions(LanguageVersion.Preview)));

    internal static Compilation AddClosedTypeContract(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.IsClosedTypeAttribute") is null)
        {
            compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                ClosedTypeContract, (CSharpParseOptions)compilation.SyntaxTrees.First().Options));
        }

        return compilation;
    }
}
