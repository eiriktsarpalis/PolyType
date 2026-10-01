using Microsoft.CodeAnalysis;
using PolyType.SourceGenerator.Model;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class IncrementalCompilationTests
{
    [Theory]
    [InlineData(CompilationTests.IsContextualHierarchySource, false)]
    [InlineData(CompilationTests.IsContextualFSharpUnionSource, false)]
    [InlineData(CompilationTests.IsContextualCSharpUnionSource, true)]
    public static void IsContextual_ContextualModelsPreserveIncrementalEquality(string source, bool isCSharpUnion)
    {
        PolyTypeSourceGeneratorResult first = Compile();
        PolyTypeSourceGeneratorResult second = Compile();
        CompilationHelpers.AssertStructurallyEqual(first.GeneratedModels, second.GeneratedModels);
        Assert.Equal(first.GeneratedModels, second.GeneratedModels);
        Assert.All(first.AllGeneratedTypes, model =>
        {
            Assert.False(model.IsContextual);
            Assert.NotEqual(model, model with { IsContextual = true });
        });

        PolyTypeSourceGeneratorResult Compile() =>
            CompilationHelpers.RunPolyTypeSourceGenerator(isCSharpUnion
                ? CompilationTests.CreateCSharpUnionCompilation(source)
                : CompilationHelpers.CreateCompilation(source));
    }

    [Theory]
    [InlineData("""
        using PolyType;
        [GenerateShape]
        public partial union U(int, string?);
        """)]
    [InlineData("""
        using PolyType;
        public union U<T>(T, string?);
        [GenerateShapeFor(typeof(U<int>))]
        public partial class Witness { }
        """)]
    public static void CSharpUnion_EquivalentCompilationsProduceEqualModels(string source)
    {
        PolyTypeSourceGeneratorResult first = CompilationHelpers.RunPolyTypeSourceGenerator(CompilationTests.CreateCSharpUnionCompilation(source));
        PolyTypeSourceGeneratorResult second = CompilationHelpers.RunPolyTypeSourceGenerator(CompilationTests.CreateCSharpUnionCompilation(source));
        CompilationHelpers.AssertStructurallyEqual(first.GeneratedModels, second.GeneratedModels);
        Assert.Equal(first.GeneratedModels, second.GeneratedModels);
    }

    [Theory]
    [InlineData("int, string?", "string?, int")]
    [InlineData("int, string?", "int, string")]
    [InlineData("int, string?", "int?, string?")]
    public static void CSharpUnion_ContractChangesInvalidateModels(string firstCases, string secondCases)
    {
        PolyTypeSourceGeneratorResult first = Compile(firstCases);
        PolyTypeSourceGeneratorResult second = Compile(secondCases);
        Assert.NotEqual(first.GeneratedModels, second.GeneratedModels);

        static PolyTypeSourceGeneratorResult Compile(string cases) =>
            CompilationHelpers.RunPolyTypeSourceGenerator(CompilationTests.CreateCSharpUnionCompilation($$"""
                using PolyType;
                [GenerateShape]
                public partial union U({{cases}});
                """));
    }

    [Fact]
    public static void CSharpUnion_ChangingWrapperRepresentationInvalidatesModels()
    {
        PolyTypeSourceGeneratorResult reference = Compile("class");
        PolyTypeSourceGeneratorResult value = Compile("struct");
        Assert.NotEqual(reference.GeneratedModels, value.GeneratedModels);

        static PolyTypeSourceGeneratorResult Compile(string kind) =>
            CompilationHelpers.RunPolyTypeSourceGenerator(CompilationTests.CreateCSharpUnionCompilation($$"""
                using PolyType;
                [System.Runtime.CompilerServices.Union]
                public {{kind}} U
                {
                    public U(int value) => Value = value;
                    public object? Value { get; }
                }
                [GenerateShapeFor(typeof(U))]
                public partial class Witness { }
                """));
    }

    [Fact]
    public static void CSharpUnion_ImplementationChangesPreserveModels()
    {
        PolyTypeSourceGeneratorResult first = Compile("Value = value;");
        PolyTypeSourceGeneratorResult second = Compile("Value = value == 0 ? null : (object)value;");
        CompilationHelpers.AssertStructurallyEqual(first.GeneratedModels, second.GeneratedModels);
        Assert.Equal(first.GeneratedModels, second.GeneratedModels);

        static PolyTypeSourceGeneratorResult Compile(string body) =>
            CompilationHelpers.RunPolyTypeSourceGenerator(CompilationTests.CreateCSharpUnionCompilation($$"""
                using PolyType;
                [System.Runtime.CompilerServices.Union, GenerateShape]
                public partial class U
                {
                    public U(int value) { {{body}} }
                    public object? Value { get; }
                }
                """));
    }

    [Theory]
    [InlineData("""
        using PolyType;

        namespace Test;

        public record MyPoco(int x, string[] ys);
        """)]
    [InlineData("""
        using PolyType;

        namespace Test;

        public record MyPoco(int x, string[] ys);

        [GenerateShape<MyPoco>]
        public partial class MyContext { }
        """)]
    [InlineData("""
        using PolyType;

        namespace Test;

        [GenerateShape]
        public partial record MyPoco(int x, string[] ys);
        """)]
    [InlineData("""
        using PolyType;

        namespace Test;

        public record MyPoco(int x, string[] ys);

        [GenerateShape<MyPoco>]
        public class MyContext { } // Non-partial class with warning
        """)]
    public static void CompilingTheSameSourceResultsInEqualModels(string source)
    {
        Compilation compilation1 = CompilationHelpers.CreateCompilation(source);
        Compilation compilation2 = CompilationHelpers.CreateCompilation(source);

        PolyTypeSourceGeneratorResult result1 = CompilationHelpers.RunPolyTypeSourceGenerator(compilation1, disableDiagnosticValidation: true);
        PolyTypeSourceGeneratorResult result2 = CompilationHelpers.RunPolyTypeSourceGenerator(compilation2, disableDiagnosticValidation: true);

        CompilationHelpers.AssertStructurallyEqual(result1.GeneratedModels, result2.GeneratedModels);
        Assert.Equal(result1.GeneratedModels, result2.GeneratedModels);
    }

    [Theory]
    [InlineData("""
        using PolyType;

        namespace Test
        {
            public record MyPoco(int x, string[] ys, bool z);

            [GenerateShapeFor(typeof(MyPoco))]
            public partial class MyContext { }
        }
        """,
        """
        using PolyType;

        namespace Test
        {
            public record MyPoco(int x, string[] ys);

            [GenerateShapeFor(typeof(MyPoco))]
            public partial class MyContext { }
        }
        """)]
    public static void CompilingDifferentSourcesResultsInNotEqualModels(string source1, string source2)
    {
        Compilation compilation1 = CompilationHelpers.CreateCompilation(source1);
        Compilation compilation2 = CompilationHelpers.CreateCompilation(source2);

        PolyTypeSourceGeneratorResult result1 = CompilationHelpers.RunPolyTypeSourceGenerator(compilation1);
        PolyTypeSourceGeneratorResult result2 = CompilationHelpers.RunPolyTypeSourceGenerator(compilation2);

        Assert.NotEqual(result1.GeneratedModels, result2.GeneratedModels);
    }

    [Theory]
    [InlineData("""
        using PolyType;

        namespace Test
        {
            public class MyPoco
            {
                public int X { get => _x; init { _x = value; } }
                private int _x;
            }

            [GenerateShapeFor(typeof(MyPoco))]
            public partial class MyContext { }
        }
        """,
        """
        using PolyType;

        namespace Test
        {
            public class MyPoco
            {

                public int X 
                { 
                    get => 42; 
                    init 
                    {
                        throw new System.NotSupportedException();
                    }
                }
            }

            [GenerateShapeFor(typeof(MyPoco))]
            public partial class MyContext { }
        }
        """)]
    public static void CompilingEquivalentSourcesResultsInEqualModels(string source1, string source2)
    {
        Compilation compilation1 = CompilationHelpers.CreateCompilation(source1);
        Compilation compilation2 = CompilationHelpers.CreateCompilation(source2);

        PolyTypeSourceGeneratorResult result1 = CompilationHelpers.RunPolyTypeSourceGenerator(compilation1);
        PolyTypeSourceGeneratorResult result2 = CompilationHelpers.RunPolyTypeSourceGenerator(compilation2);

        CompilationHelpers.AssertStructurallyEqual(result1.GeneratedModels, result2.GeneratedModels);
        Assert.Equal(result1.GeneratedModels, result2.GeneratedModels);
    }
}
