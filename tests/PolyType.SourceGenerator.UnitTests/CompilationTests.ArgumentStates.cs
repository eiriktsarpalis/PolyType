using Microsoft.CodeAnalysis;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    [Fact]
    public static void Generation_UsesPooledArgumentStates()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;

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
        Assert.Contains("global::PolyType.SourceGenModel.SmallClassArgumentState<", generatedSource);
        Assert.Contains(".Rent(", generatedSource);
        Assert.Contains("finally { state.Return(); }", generatedSource);
    }

    [Fact]
    public static void Generation_UsesLargePooledArgumentStates()
    {
        string parameters = string.Join(", ", Enumerable.Range(0, 65).Select(index => $"int value{index}"));
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;

            [GenerateShape]
            public partial class Model
            {
                public Model({{parameters}}) { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);

        string generatedSource = string.Join(Environment.NewLine, result.NewCompilation.SyntaxTrees.Skip(1).Select(tree => tree.ToString()));
        Assert.Contains("global::PolyType.SourceGenModel.LargeClassArgumentState<", generatedSource);
        Assert.Contains(".Rent(", generatedSource);
    }
}
