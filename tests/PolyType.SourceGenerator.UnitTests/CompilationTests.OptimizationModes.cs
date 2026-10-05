using Microsoft.CodeAnalysis;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    [Theory]
    [InlineData("Performance", "global::PolyType.SourceGenModel.SmallArgumentState<")]
    [InlineData("AppSize", "global::PolyType.SourceGenModel.ClassSmallArgumentState<")]
    public static void GenerationOptimizationMode_SelectsArgumentStateRepresentation(string mode, string expectedArgumentStateType)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;

            [assembly: PolyTypeSourceGenerationOptions(OptimizationMode = PolyTypeOptimizationMode.{{mode}})]

            [GenerateShape]
            public partial class Model
            {
                public Model(int value) { }

                [MethodShape]
                public int Increment(int value) => value + 1;
            }

            [GenerateShapeFor(typeof(System.Func<int, int>))]
            public partial class FunctionWitness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);

        string generatedSource = string.Join(Environment.NewLine, result.NewCompilation.SyntaxTrees.Skip(1).Select(tree => tree.ToString()));
        Assert.Contains(expectedArgumentStateType, generatedSource);

        if (mode is "AppSize")
        {
            Assert.Contains(".Rent(", generatedSource);
        }
    }

    [Theory]
    [InlineData("Performance", "global::PolyType.SourceGenModel.LargeArgumentState<")]
    [InlineData("AppSize", "global::PolyType.SourceGenModel.ClassLargeArgumentState<")]
    public static void OptimizationMode_SelectsLargeArgumentStateRepresentation(string mode, string expectedArgumentStateType)
    {
        string parameters = string.Join(", ", Enumerable.Range(0, 65).Select(index => $"int value{index}"));
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;

            [assembly: PolyTypeSourceGenerationOptions(OptimizationMode = PolyTypeOptimizationMode.{{mode}})]

            [GenerateShape]
            public partial class Model
            {
                public Model({{parameters}}) { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);

        string generatedSource = string.Join(Environment.NewLine, result.NewCompilation.SyntaxTrees.Skip(1).Select(tree => tree.ToString()));
        Assert.Contains(expectedArgumentStateType, generatedSource);

        if (mode is "AppSize")
        {
            Assert.Contains(".Rent(", generatedSource);
        }
    }
}
