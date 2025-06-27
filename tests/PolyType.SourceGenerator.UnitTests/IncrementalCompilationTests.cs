﻿using Microsoft.CodeAnalysis;
using PolyType.SourceGenerator.Model;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class IncrementalCompilationTests
{
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
