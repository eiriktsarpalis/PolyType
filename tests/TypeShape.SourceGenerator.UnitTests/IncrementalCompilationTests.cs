﻿using Microsoft.CodeAnalysis;
using TypeShape.SourceGenerator.Model;
using Xunit;

namespace TypeShape.SourceGenerator.UnitTests;

public static class IncrementalCompilationTests
{
    [Theory]
    [InlineData("""
        using TypeShape;

        namespace Test;

        public record MyPoco(int x, string[] ys);
        """)]
    [InlineData("""
        using TypeShape;

        namespace Test;

        public record MyPoco(int x, string[] ys);

        [GenerateShape(typeof(MyPoco))]
        public partial class MyContext { }
        """)]
    public static void CompilingTheSameSourceResultsInEqualModels(string source)
    {
        Compilation compilation1 = CompilationHelpers.CreateCompilation(source);
        Compilation compilation2 = CompilationHelpers.CreateCompilation(source);

        TypeShapeSourceGeneratorResult result1 = CompilationHelpers.RunTypeShapeSourceGenerator(compilation1);
        TypeShapeSourceGeneratorResult result2 = CompilationHelpers.RunTypeShapeSourceGenerator(compilation2);

        CompilationHelpers.AssertStructurallyEqual(result1.GeneratedModels, result2.GeneratedModels);
        Assert.Equal(result1.GeneratedModels, result2.GeneratedModels);
        Assert.Equal(result1.Diagnostics, result2.Diagnostics);
    }

    [Theory]
    [InlineData("""
        using TypeShape;

        namespace Test;

        public record MyPoco(int x, string[] ys, bool z);

        [GenerateShape(typeof(MyPoco))]
        public partial class MyContext { }
        """,
        """
        using TypeShape;

        namespace Test;

        public record MyPoco(int x, string[] ys);

        [GenerateShape(typeof(MyPoco))]
        public partial class MyContext { }
        """)]
    public static void CompilingDifferentSourcesResultsInNotEqualModels(string source1, string source2)
    {
        Compilation compilation1 = CompilationHelpers.CreateCompilation(source1);
        Compilation compilation2 = CompilationHelpers.CreateCompilation(source2);

        TypeShapeSourceGeneratorResult result1 = CompilationHelpers.RunTypeShapeSourceGenerator(compilation1);
        TypeShapeSourceGeneratorResult result2 = CompilationHelpers.RunTypeShapeSourceGenerator(compilation2);

        Assert.NotEqual(result1.GeneratedModels, result2.GeneratedModels);
    }

    [Theory]
    [InlineData("""
        using TypeShape;

        namespace Test;

        public class MyPoco
        {
            public int X { get => _x; init { _x = value; } }
            private int _x;
        }

        [GenerateShape(typeof(MyPoco))]
        public partial class MyContext { }
        """,
        """
        using TypeShape;

        namespace Test;

        public class MyPoco
        {

            public int X 
            { 
                get => 42; 
                init 
                {
                    throw new NotSupportedException();
                }
            }
        }

        [GenerateShape(typeof(MyPoco))]
        public partial class MyContext { }
        """)]
    public static void CompilingEquivalentSourcesResultsInEqualModels(string source1, string source2)
    {
        Compilation compilation1 = CompilationHelpers.CreateCompilation(source1);
        Compilation compilation2 = CompilationHelpers.CreateCompilation(source2);

        TypeShapeSourceGeneratorResult result1 = CompilationHelpers.RunTypeShapeSourceGenerator(compilation1);
        TypeShapeSourceGeneratorResult result2 = CompilationHelpers.RunTypeShapeSourceGenerator(compilation2);

        CompilationHelpers.AssertStructurallyEqual(result1.GeneratedModels, result2.GeneratedModels);
        Assert.Equal(result1.GeneratedModels, result2.GeneratedModels);
    }
}
