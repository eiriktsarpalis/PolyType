using Microsoft.CodeAnalysis;
using PolyType.SourceGenerator.Model;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class ClosedTypeInferenceDiagnosticTests
{
    [Theory]
    [InlineData("class Root;", false)]
    [InlineData("sealed class Root;", false)]
    [InlineData("abstract class Root;", false)]
    [InlineData("interface Root;", false)]
    [InlineData("struct Root;", false)]
    [InlineData("enum Root { Value }", false)]
    [InlineData("class Root;", true)]
    public static void ClosedInferenceRejectsNonClosedTargets(string declaration, bool explicitHierarchy)
    {
        string registrations = explicitHierarchy ? "[DerivedTypeShape(typeof(Leaf))]" : "";
        string derived = explicitHierarchy ? "public sealed class Leaf : Root;" : "";
        PolyTypeSourceGeneratorResult result = Compile($$"""
            using PolyType;
            [TypeShape(InferClosedTypePolymorphism = true)]
            {{registrations}}
            public {{declaration}}
            {{derived}}
            [GenerateShapeFor(typeof(Root))]
            public partial class Witness;
            """);
        AssertError(result, "PT0032");
        Assert.DoesNotContain(result.AllGeneratedTypes, model => model.Type.FullyQualifiedName == "global::Root");
    }

    [Theory]
    [InlineData("Kind = TypeShapeKind.None")]
    [InlineData("Kind = TypeShapeKind.Object")]
    [InlineData("Kind = TypeShapeKind.Enumerable")]
    [InlineData("Marshaler = typeof(object)")]
    public static void ClosedInferenceRejectsIncompatibleConfiguration(string setting)
    {
        PolyTypeSourceGeneratorResult result = Compile($$"""
            using PolyType;
            [GenerateShape, TypeShape(InferClosedTypePolymorphism = true, {{setting}})]
            public closed partial class Root;
            """);
        AssertError(result, "PT0028");
    }

    [Theory]
    [InlineData("""
        [TypeShape(InferClosedTypePolymorphism = true)]
        public closed class Root;
        """)]
    [InlineData("""
        [assembly: TypeShapeExtension(typeof(Root), InferClosedTypePolymorphism = true)]
        public closed class Root;
        """)]
    [InlineData("""
        [assembly: TypeShapeExtension(typeof(Root<>), InferClosedTypePolymorphism = true)]
        public closed class Root<T>;
        """)]
    [InlineData("""
        [assembly: TypeShapeExtension(typeof(Root), InferClosedTypePolymorphism = true)]
        [assembly: TypeShapeExtension(typeof(Root), InferClosedTypePolymorphism = false)]
        public closed class Root;
        """)]
    public static void ClosedInferenceRejectsConflictingOptIns(string declaration)
    {
        bool generic = declaration.IndexOf("Root<>", StringComparison.Ordinal) >= 0;
        string target = generic ? "Root<int>" : "Root";
        PolyTypeSourceGeneratorResult result = Compile($$"""
            using PolyType;
            [assembly: TypeShapeExtension(typeof({{target}}), InferClosedTypePolymorphism = false)]
            {{declaration}}
            [GenerateShapeFor(typeof({{target}}))]
            public partial class Witness;
            """);
        AssertError(result, "PT0028");
    }

    [Theory]
    [InlineData("internal sealed class Leaf : Root;")]
    [InlineData("public static class Container { private sealed class Leaf : Root; }")]
    [InlineData("private static class Container { public sealed class Leaf : Root; }")]
    [InlineData("public closed class Middle : Root; internal sealed class Leaf : Middle;")]
    public static void ClosedInferenceRejectsLessAccessibleCases(string cases)
    {
        string declaration = cases.StartsWith("private ", StringComparison.Ordinal)
            ? $$"""
                public static partial class Holder
                {
                    [GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
                    public closed partial class Root;
                    {{cases}}
                }
                """
            : $$"""
                [GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
                public closed partial class Root;
                {{cases}}
                """;
        PolyTypeSourceGeneratorResult result = Compile("using PolyType;" + Environment.NewLine + declaration);
        AssertError(result, "PT0029");
        Assert.DoesNotContain(result.AllGeneratedTypes.OfType<UnionShapeModel>(), model => model.Type.FullyQualifiedName.EndsWith(".Root", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("DerivedTypeShape")]
    [InlineData("KnownType")]
    public static void ClosedInferenceWarnsWhenExplicitRegistrationsWin(string attribute)
    {
        PolyTypeSourceGeneratorResult result = Compile($$"""
            using PolyType;
            using System.Runtime.Serialization;
            [GenerateShape, TypeShape(InferClosedTypePolymorphism = true)]
            [{{attribute}}(typeof(Selected))]
            public closed partial class Root;
            public sealed class Selected : Root;
            public sealed class Ignored : Root;
            """);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "PT0030");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        UnionShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        Assert.Equal("Selected", Assert.Single(model.UnionCases).Name);
    }

    [Theory]
    [InlineData("""
        public sealed class Leaf<T> : Root<System.Collections.Generic.List<T>>;
        """, "int")]
    [InlineData("""
        public sealed class Leaf<T> : Root<System.Collections.Generic.List<T>>;
        """, "int[]")]
    [InlineData("""
        public sealed class Leaf<T> : Root<T> where T : struct;
        """, "string")]
    public static void ClosedInferenceDoesNotOmitIncompatibleGenericCases(string cases, string argument)
    {
        PolyTypeSourceGeneratorResult result = Compile($$"""
            using PolyType;
            [TypeShape(InferClosedTypePolymorphism = true)]
            public closed class Root<T>;
            {{cases}}
            [GenerateShapeFor(typeof(Root<{{argument}}>))]
            public partial class Witness;
            """);
        AssertError(result, "PT0013");
        Assert.Empty(result.AllGeneratedTypes.OfType<UnionShapeModel>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ClosedInferencePreservesMethodBasedKnownTypeDiagnostic(bool infer)
    {
        PolyTypeSourceGeneratorResult result = Compile($$"""
            using PolyType;
            using System;
            using System.Runtime.Serialization;
            [GenerateShape, TypeShape(InferClosedTypePolymorphism = {{(infer ? "true" : "false")}})]
            [KnownType(nameof(GetKnownTypes))]
            public closed partial class Root
            {
                public static Type[] GetKnownTypes() => Array.Empty<Type>();
            }
            """);
        AssertError(result, "PT0027");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "PT0031");
    }

    private static PolyTypeSourceGeneratorResult Compile(string source) =>
        CompilationHelpers.RunPolyTypeSourceGenerator(CompilationTests.CreateClosedClassCompilation(source), disableDiagnosticValidation: true);

    private static void AssertError(PolyTypeSourceGeneratorResult result, string id)
    {
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }
}
