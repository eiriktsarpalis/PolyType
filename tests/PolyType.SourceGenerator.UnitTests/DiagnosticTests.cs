using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PolyType.Abstractions;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class DiagnosticTests
{
    [Fact]
    public static void GenerateShapeOfT_UnsupportedType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShapeFor(typeof(MissingType))]
            public partial class ShapeProvider { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic? diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == "PT0001");

        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((2, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((2, 38), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShape_NonPartialClass_ProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public class TypeToGenerate { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 31), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShapeOfT_NonPartialClass_ProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShapeFor(typeof(TypeToGenerate))]
            public class ShapeProvider { }

            public class TypeToGenerate { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 30), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShape_GenericType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial class GenericType<T>
            {
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((5, 1), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShape_NestedGenericType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            public partial class GenericContainer<T>
            {
                [GenerateShape]
                public partial class TypeToGenerate
                {
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((4, 4), diagnostic.Location.GetStartPosition());
        Assert.Equal((7, 5), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShapeOfT_GenericType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShapeFor(typeof(string))]
            public partial class Witness<T>
            {
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((5, 1), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShapeOfT_NestedGenericType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            public partial class Container<T>
            {
                [GenerateShapeFor(typeof(string))]
                public partial class Witness
                {
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((4, 4), diagnostic.Location.GetStartPosition());
        Assert.Equal((7, 5), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShape_InaccessibleType_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            internal static partial class Container
            {
                [GenerateShape]
                private partial record TypeToGenerate(int x);
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((4, 5), diagnostic.Location.GetStartPosition());
        Assert.Equal((4, 18), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void DuplicateConstructorShapeAttribute_ProducesWarnings()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;

           [GenerateShape]
           partial class MyPoco
           {
               [ConstructorShape]
               public MyPoco() { }

               [ConstructorShape]
               public MyPoco(int value) { }
           }
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((9, 11), diagnostic.Location.GetStartPosition());
        Assert.Equal((9, 17), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShapeOnStaticClass_ProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;

           [GenerateShape]
           static partial class MyClass { }
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 32), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenerateShapeOfTOnStaticClass_ProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;

           [GenerateShapeFor(typeof(MyPoco))]
           static partial class Witness { }

           record MyPoco;
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 0), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 32), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShape_PrivateAssociatedType()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            partial class Wrapper
            {
                [AssociatedTypeShape(typeof(InternalAssociatedType))]
                [GenerateShape]
                internal partial class MyPoco { }

                class InternalAssociatedType { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((4, 5), diagnostic.Location.GetStartPosition());
        Assert.Equal((4, 56), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShape_ArityMismatch_0to1()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [AssociatedTypeShape(typeof(InternalAssociatedType<>))]
            [GenerateShape]
            partial class MyPoco { }
            public class InternalAssociatedType<T> { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0016", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((2, 54), diagnostic.Location.GetEndPosition());
    }


    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShape_ArityMismatch_2to1()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [AssociatedTypeShape(typeof(InternalAssociatedType<>))]
            partial class MyPoco<T1, T2> { }
            public class InternalAssociatedType<T> { }

            [GenerateShapeFor(typeof(MyPoco<int, string>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0016", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((2, 54), diagnostic.Location.GetEndPosition());
    }

    [Theory]
    [InlineData(LanguageVersion.CSharp2)]
    [InlineData(LanguageVersion.CSharp7_3)]
    [InlineData(LanguageVersion.CSharp8)]
    public static void UnsupportedLanguageVersions_ErrorDiagnostic(LanguageVersion langVersion)
    {
        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(langVersion);
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            partial class Default
            {
            }
            """, parseOptions: parseOptions, nullableContextOptions: NullableContextOptions.Disable);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Same(Location.None, diagnostic.Location);
    }

    [Fact]
    public static void SupportedLanguageVersions_NoErrorDiagnostics()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            partial class Default { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enum)]
    [InlineData(TypeShapeKind.Optional)]
    [InlineData(TypeShapeKind.Dictionary)]
    [InlineData(TypeShapeKind.Enumerable)]
    public static void TypeShapeAttribute_ClassWithInvalidKind_ProducesDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;

            [TypeShape(Kind = TypeShapeKind.{{kind}})]
            class MyPoco { }

            [GenerateShapeFor(typeof(MyPoco))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((2, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal(2, diagnostic.Location.GetEndPosition().startLine);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enum)]
    [InlineData(TypeShapeKind.Optional)]
    public static void TypeShapeAttribute_DictionaryWithInvalidKind_ProducesDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;
            using System.Collections.Generic;

            [TypeShape(Kind = TypeShapeKind.{{kind}})]
            class MyPoco : Dictionary<string, string> { }

            [GenerateShapeFor(typeof(MyPoco))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((3, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal(3, diagnostic.Location.GetEndPosition().startLine);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enum)]
    [InlineData(TypeShapeKind.Optional)]
    [InlineData(TypeShapeKind.Dictionary)]
    public static void TypeShapeAttribute_EnumerableWithInvalidKind_ProducesDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;
            using System.Collections.Generic;

            [TypeShape(Kind = TypeShapeKind.{{kind}})]
            class MyPoco : List<string> { }

            [GenerateShapeFor(typeof(MyPoco))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal((3, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal(3, diagnostic.Location.GetEndPosition().startLine);
    }

    [Theory]
    [InlineData(TypeShapeKind.Object)]
    [InlineData(TypeShapeKind.None)]
    public static void TypeShapeAttribute_ClassWithValidKind_NoDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;

            [TypeShape(Kind = TypeShapeKind.{{kind}})]
            class MyPoco { }

            [GenerateShapeFor(typeof(MyPoco))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enumerable)]
    [InlineData(TypeShapeKind.Dictionary)]
    [InlineData(TypeShapeKind.Object)]
    [InlineData(TypeShapeKind.None)]
    public static void TypeShapeAttribute_DictionaryWithValidKind_NoDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;
            using System.Collections.Generic;

            [TypeShape(Kind = TypeShapeKind.{{kind}})]
            class MyPoco : Dictionary<string, string> { }

            [GenerateShapeFor(typeof(MyPoco))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData(TypeShapeKind.Enumerable)]
    [InlineData(TypeShapeKind.Object)]
    [InlineData(TypeShapeKind.None)]
    public static void TypeShapeAttribute_EnumerableWithValidKind_NoDiagnostic(TypeShapeKind kind)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;
            using System.Collections.Generic;

            [TypeShape(Kind = TypeShapeKind.{{kind}})]
            class MyPoco : List<string> { }

            [GenerateShapeFor(typeof(MyPoco))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("""
        using PolyType;

        [GenerateShape, TypeShape(Marshaler = typeof(int))]
        partial class MyPoco { }
        """)]
    [InlineData("""
        using PolyType;

        [GenerateShape, TypeShape(Kind = TypeShapeKind.Surrogate)]
        partial class MyPoco { }
        """)]
    [InlineData("""
        using PolyType;

        [GenerateShape, TypeShape(Marshaler = typeof(Marshaler))]
        partial class MyPoco
        {
            private class Marshaler : IMarshaler<MyPoco, object>
            {
                public object? Marshal(MyPoco? source) => source;
                public MyPoco? Unmarshal(object? value) => (MyPoco?)value;
            }
        }
        """)]
    [InlineData("""
         using PolyType;

         [GenerateShape, TypeShape(Marshaler = typeof(Marshaler))]
         public partial class MyPoco
         {
             public class Marshaler : IMarshaler<MyPoco, object>
             {
                 private Marshaler() { }
                 public object? Marshal(MyPoco? source) => source;
                 public MyPoco? Unmarshal(object? value) => (MyPoco?)value;
             }
         }
         """)]
    [InlineData("""
        using PolyType;

        [GenerateShape, TypeShape(Marshaler = typeof(Marshaler))]
        public partial class MyPoco
        {
            public class Marshaler : IMarshaler<int, object>
            {
                public object? Marshal(int source) => null;
                public int Unmarshal(object? value) => 0;
            }
        }
        """)]
    [InlineData("""
        using PolyType;

        [GenerateShape, TypeShape(Marshaler = typeof(Marshaler))]
        public partial class MyPoco
        {
            public class Marshaler :
                IMarshaler<MyPoco, object>,
                IMarshaler<MyPoco, int>
            {
                public object? Marshal(MyPoco? source) => null;
                public MyPoco? Unmarshal(object? value) => null;
                int IMarshaler<MyPoco, int>.Marshal(MyPoco? source) => 0;
                MyPoco? IMarshaler<MyPoco, int>.Unmarshal(int value) => null;
            }
        }
        """)]
    public static void InvalidMarshaler_ErrorDiagnostic(string source)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation(source);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken));

        PolyTypeSourceGeneratorResult result =
            CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic? diagnostic = result.Diagnostics.SingleOrDefault(d => d.Id == "PT0010");
        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public static void ValidMarshaler_NoDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape, TypeShape(Marshaler = typeof(Marshaler))]
            public partial class MyPoco
            {
                public class Marshaler : IMarshaler<MyPoco, object>
                {
                    public object? Marshal(MyPoco? source) => source;
                    public MyPoco? Unmarshal(object? value) => (MyPoco?)value;
                }
            }
            """);

        PolyTypeSourceGeneratorResult result =
            CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("""
        using PolyType;

        [TypeShape(Marshaler = typeof(Marshaler<>))]
        public record MyPoco<T>(T Value);

        public class Marshaler<T> : IMarshaler<MyPoco<T>, T>
        {
            public T? Marshal(MyPoco<T>? source) => source is null ? default : source.Value;
            public MyPoco<T>? Unmarshal(T? value) => value is null ? null : new(value);
        }

        [GenerateShapeFor(typeof(MyPoco<int>))]
        public partial class Witness { }
        """)]
    [InlineData("""
        using PolyType;

        [TypeShape(Marshaler = typeof(MyPoco<>.Marshaler))]
        public record MyPoco<T>(T Value)
        {
            public class Marshaler : IMarshaler<MyPoco<T>, T>
            {
                public T? Marshal(MyPoco<T>? source) => source is null ? default : source.Value;
                public MyPoco<T>? Unmarshal(T? value) => value is null ? null : new(value);
            }
        }

        [GenerateShapeFor(typeof(MyPoco<int>))]
        public partial class Witness { }
        """)]
    [InlineData("""
        using PolyType;

        [TypeShape(Marshaler = typeof(Container<>.Container2.Marshaler<>))]
        public record MyPoco<T1, T2>(T1 Value1, T2 Value2);

        public static class Container<T1>
        {
            public class Container2
            {
                public class Marshaler<T2> : IMarshaler<MyPoco<T1, T2>, (T1, T2)>
                {
                    public (T1, T2) Marshal(MyPoco<T1, T2>? source) => source is null ? default : (source.Value1, source.Value2);
                    public MyPoco<T1, T2>? Unmarshal((T1, T2) pair) => new(pair.Item1, pair.Item2);
                }
            }
        }

        [GenerateShapeFor(typeof(MyPoco<int, string>))]
        public partial class Witness { }
        """)]
    public static void ValidGenericMarshaler_NoDiagnostic(string source)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation(source);

        PolyTypeSourceGeneratorResult result =
            CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void PolymorphicClassWithDerivedType_NotASubtype_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            [DerivedTypeShape(typeof(object))]
            partial class PolymorphicClassWithInvalidDerivedType_NotASubtype { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("'object'", diagnostic.GetMessage());
        Assert.Equal((3, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 33), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void PolymorphicClassWithConflictingDerivedTypes_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            [DerivedTypeShape(typeof(Derived), Name = "case1", Tag = 1)]
            [DerivedTypeShape(typeof(Derived), Name = "case2", Tag = 2)]
            public partial class PolymorphicClassWithInvalidDerivedType_ConflictingTypes
            {
                public class Derived : PolymorphicClassWithInvalidDerivedType_ConflictingTypes { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0012", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("type 'PolymorphicClassWithInvalidDerivedType_ConflictingTypes.Derived'", diagnostic.GetMessage());
        Assert.Equal((4, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((4, 59), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void ClosedAndOpenDerivedTypesResolvingToSameType_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Derived<int>), Name = "closed", Tag = 1)]
            [DerivedTypeShape(typeof(Derived<>), Name = "open", Tag = 2)]
            class Base<T> { }

            class Derived<T> : Base<T> { }

            [GenerateShapeFor(typeof(Base<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0012", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("type 'Derived<int>'", diagnostic.GetMessage());
    }

    [Theory]
    [MemberData(nameof(GetInvalidDerivedTypeMetadataCollisionCases))]
    public static void InvalidDerivedType_StillReportsMetadataCollisions(string source, string primaryDiagnosticId)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation(source);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == primaryDiagnosticId);
        Diagnostic[] duplicateDiagnostics = result.Diagnostics.Where(diagnostic => diagnostic.Id == "PT0012").ToArray();
        Assert.Equal(2, duplicateDiagnostics.Length);
        Assert.Contains(duplicateDiagnostics, diagnostic => diagnostic.GetMessage().Contains("tag '42'"));
        Assert.Contains(duplicateDiagnostics, diagnostic => diagnostic.GetMessage().Contains("name 'collision'"));
    }

    public static IEnumerable<object[]> GetInvalidDerivedTypeMetadataCollisionCases()
    {
        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Unrelated), Name = "collision", Tag = 42)]
            [DerivedTypeShape(typeof(Derived), Name = "collision", Tag = 42)]
            class Base { }

            class Unrelated { }
            class Derived : Base { }

            [GenerateShapeFor(typeof(Base))]
            partial class Witness { }
            """,
            "PT0011",
        ];

        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Unbound<,>), Name = "collision", Tag = 42)]
            [DerivedTypeShape(typeof(Derived<>), Name = "collision", Tag = 42)]
            class Base<T> { }

            class Unbound<T, U> : Base<T> { }
            class Derived<T> : Base<T> { }

            [GenerateShapeFor(typeof(Base<int>))]
            partial class Witness { }
            """,
            "PT0013",
        ];
    }

    [Theory]
    [MemberData(nameof(GetUnsuppressibleDerivedTypeDiagnosticCases))]
    public static void InvalidDerivedTypeDiagnostics_CannotBeSuppressed(string source, string diagnosticId)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation(source);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        Assert.False(diagnostic.IsSuppressed);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    public static IEnumerable<object[]> GetUnsuppressibleDerivedTypeDiagnosticCases()
    {
        yield return
        [
            """
            using PolyType;

            #pragma warning disable PT0011
            [DerivedTypeShape(typeof(Unrelated))]
            class Base { }
            #pragma warning restore PT0011

            class Unrelated { }

            [GenerateShapeFor(typeof(Base))]
            partial class Witness { }
            """,
            "PT0011",
        ];

        yield return
        [
            """
            using PolyType;

            #pragma warning disable PT0012
            [DerivedTypeShape(typeof(Derived), Name = "first")]
            [DerivedTypeShape(typeof(Derived), Name = "second")]
            class Base { }
            #pragma warning restore PT0012

            class Derived : Base { }

            [GenerateShapeFor(typeof(Base))]
            partial class Witness { }
            """,
            "PT0012",
        ];

        yield return
        [
            """
            using PolyType;

            #pragma warning disable PT0013
            [DerivedTypeShape(typeof(Derived<,>))]
            class Base<T> { }
            #pragma warning restore PT0013

            class Derived<T, U> : Base<T> { }

            [GenerateShapeFor(typeof(Base<int>))]
            partial class Witness { }
            """,
            "PT0013",
        ];
    }

    [Fact]
    public static void PolymorphicClassWithConflictingDerivedTypeNames_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            [DerivedTypeShape(typeof(Derived1), Name = "case1", Tag = 1)]
            [DerivedTypeShape(typeof(Derived2), Name = "case1", Tag = 2)]
            public partial class PolymorphicClassWithInvalidDerivedType_ConflictingNames
            {
                public class Derived1 : PolymorphicClassWithInvalidDerivedType_ConflictingNames { }
                public class Derived2 : PolymorphicClassWithInvalidDerivedType_ConflictingNames { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0012", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("name 'case1'", diagnostic.GetMessage());
        Assert.Equal((4, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((4, 60), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void PolymorphicClassWithConflictingDerivedTypeTags_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            [DerivedTypeShape(typeof(Derived1), Name = "case1", Tag = 42)]
            [DerivedTypeShape(typeof(Derived2), Name = "case2", Tag = 42)]
            public partial class PolymorphicClassWithInvalidDerivedType_ConflictingTags
            {
                public class Derived1 : PolymorphicClassWithInvalidDerivedType_ConflictingTags { }
                public class Derived2 : PolymorphicClassWithInvalidDerivedType_ConflictingTags { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0012", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("tag '42'", diagnostic.GetMessage());
        Assert.Equal((4, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((4, 61), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void PolymorphicClassWithGenericDerivedType_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Derived<>))]
            class PolymorphicClassWithGenericDerivedType
            {
                public class Derived<T> : PolymorphicClassWithGenericDerivedType { }
            }

            [GenerateShapeFor(typeof(PolymorphicClassWithGenericDerivedType))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("not assignable to the base type", diagnostic.GetMessage());
        Assert.Equal((2, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((2, 36), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void GenericPolymorphicClassWithMismatchingGenericDerivedType_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(Derived<>))]
            class GenericPolymorphicClassWithMismatchingGenericDerivedType<T>
            {
                public class Derived<S> : GenericPolymorphicClassWithMismatchingGenericDerivedType<List<S>> { }
            }

            [GenerateShapeFor(typeof(GenericPolymorphicClassWithMismatchingGenericDerivedType<string>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        // The nested Derived<S> requires both the enclosing T and its own S; only S is bound by the
        // matching ancestor's arguments, so T is reported as the unbound parameter.
        Assert.Contains("'T'", diagnostic.GetMessage());
        Assert.Contains("not bound by the base type's arguments", diagnostic.GetMessage());
        Assert.Equal((3, 1), diagnostic.Location.GetStartPosition());
        Assert.Equal((3, 36), diagnostic.Location.GetEndPosition());
    }

    [Fact]
    public static void OpenGenericDerivedType_UnboundParameter_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Derived<,>))]
            class Base<T>
            {
                public class Inner { }
            }

            class Derived<T1, T2> : Base<T1> { }

            [GenerateShapeFor(typeof(Base<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("'T2'", diagnostic.GetMessage());
        Assert.Contains("not bound by the base type's arguments", diagnostic.GetMessage());
    }

    [Fact]
    public static void OpenGenericDerivedType_AmbiguousMatch_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(Impl<>))]
            interface IBase<T> { }

            class Impl<T> : IBase<T>, IBase<List<T>> { }

            [GenerateShapeFor(typeof(IBase<List<int>>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("matches the base type through multiple ancestors", diagnostic.GetMessage());
    }

    [Fact]
    public static void OpenGenericDerivedType_ConstraintViolation_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Derived<>))]
            class Base<T> { }

            class Derived<T> : Base<T> where T : struct { }

            [GenerateShapeFor(typeof(Base<string>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("constraint", diagnostic.GetMessage());
    }

    [Fact]
    public static void OpenGenericDerivedType_Identity_NoDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Derived<>))]
            class Base<T> { }

            class Derived<T> : Base<T> { }

            [GenerateShapeFor(typeof(Base<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void OpenGenericDerivedType_ReorderedParameters_NoDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Reordered<,>))]
            class Base<T1, T2> { }

            class Reordered<U, V> : Base<V, U> { }

            [GenerateShapeFor(typeof(Base<int, string>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void OpenGenericDerivedType_PartialConcretization_NoDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Partial<>))]
            class Base<T1, T2> { }

            class Partial<T> : Base<T, int> { }

            [GenerateShapeFor(typeof(Base<bool, int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void OpenGenericDerivedType_WrappedArgument_NoDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(Wrapped<>))]
            class Base<T> { }

            class Wrapped<T> : Base<List<T>> { }

            [GenerateShapeFor(typeof(Base<List<int>>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void OpenGenericDerivedType_InterfaceBase_NoDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Impl<>))]
            interface IBase<T> { }

            class Impl<T> : IBase<T> { }

            [GenerateShapeFor(typeof(IBase<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(GetAdvancedValidOpenGenericDerivedTypeCases))]
    public static void OpenGenericDerivedType_AdvancedValidPattern_NoDiagnostic(string source)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation(source);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    public static IEnumerable<object[]> GetAdvancedValidOpenGenericDerivedTypeCases()
    {
        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Outer<>.Middle.Leaf<>))]
            class Base<T> { }

            class Outer<T>
            {
                public class Middle
                {
                    public class Leaf<U> : Base<(T, U)> { }
                }
            }

            [GenerateShapeFor(typeof(Base<(int, string)>))]
            partial class Witness { }
            """,
        ];

        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Derived<>))]
            class Base<T> { }

            class Outer<T>
            {
                public class Box<U> { }
            }

            class Derived<T> : Base<Outer<T>.Box<int>> { }

            [GenerateShapeFor(typeof(Base<Outer<string>.Box<int>>))]
            partial class Witness { }
            """,
        ];

        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Impl<>))]
            interface IBase<T> { }

            interface ILeft<T> : IBase<T> { }
            interface IRight<T> : IBase<T> { }
            interface IDiamond<T> : ILeft<T>, IRight<T> { }
            class Impl<T> : IDiamond<T> { }

            [GenerateShapeFor(typeof(IBase<int>))]
            partial class Witness { }
            """,
        ];

        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Impl<>))]
            interface IBase1<T> { }

            [DerivedTypeShape(typeof(Impl<>))]
            interface IBase2<T> { }

            class Impl<T> : IBase1<T>, IBase2<T> { }

            [GenerateShapeFor(typeof(IBase1<int>))]
            [GenerateShapeFor(typeof(IBase2<string>))]
            partial class Witness { }
            """,
        ];

        yield return
        [
            """
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(Impl<>))]
            interface IBase<T> { }

            class Impl<T> : IBase<T>, IBase<List<T>> { }

            [GenerateShapeFor(typeof(IBase<int>))]
            partial class Witness { }
            """,
        ];

        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(CovariantImpl<>))]
            interface ICovariant<out T> { }
            class CovariantImpl<T> : ICovariant<T> { }

            [DerivedTypeShape(typeof(ContravariantImpl<>))]
            interface IContravariant<in T> { }
            class ContravariantImpl<T> : IContravariant<T> { }

            [DerivedTypeShape(typeof(BivariantImpl<,>))]
            interface IBivariant<in TIn, out TOut> { }
            class BivariantImpl<TIn, TOut> : IBivariant<TIn, TOut> { }

            [GenerateShapeFor(typeof(ICovariant<object>))]
            [GenerateShapeFor(typeof(IContravariant<string>))]
            [GenerateShapeFor(typeof(IBivariant<string, object>))]
            partial class Witness { }
            """,
        ];

        yield return
        [
            """
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(Derived<>))]
            class Base<T> { }

            class Derived<T> : Base<T> where T : IEnumerable<object> { }

            [GenerateShapeFor(typeof(Base<List<string>>))]
            partial class Witness { }
            """,
        ];

        yield return
        [
            """
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(Derived<,>))]
            class Base<T, U> { }

            class Derived<T, U> : Base<T, U> where T : IEnumerable<U[]> { }

            [GenerateShapeFor(typeof(Base<List<int[]>, int>))]
            partial class Witness { }
            """,
        ];

        yield return
        [
            """
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(KeyValueDerived<>))]
            class KeyValueBase<T> { }
            class KeyValueDerived<T> : KeyValueBase<KeyValuePair<string, T>> { }

            [DerivedTypeShape(typeof(TupleDerived<,>))]
            class TupleBase<T> { }
            class TupleDerived<T1, T2> : TupleBase<(T1, T2)> { }

            [GenerateShapeFor(typeof(KeyValueBase<KeyValuePair<string, int>>))]
            [GenerateShapeFor(typeof(TupleBase<(string, int)>))]
            partial class Witness { }
            """,
        ];
    }

    [Fact]
    public static void ClosedDerived_TargetingDifferentBaseInstantiation_ErrorDiagnostics()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Cat))]
            [DerivedTypeShape(typeof(Dog))]
            class Animal<T> { }

            class Cat : Animal<int> { }
            class Dog : Animal<string> { }

            [GenerateShapeFor(typeof(Animal<int>))]
            [GenerateShapeFor(typeof(Animal<string>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal("PT0011", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        });
    }

    [Fact]
    public static void ClosedDerived_TargetingDifferentBaseInstantiation_AllInvalid_ErrorDiagnostics()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Cat))]
            [DerivedTypeShape(typeof(Dog))]
            class Animal<T> { }

            class Cat : Animal<int> { }
            class Dog : Animal<string> { }

            [GenerateShapeFor(typeof(Animal<bool>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, diagnostic =>
        {
            Assert.Equal("PT0011", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        });
    }

    [Fact]
    public static void ClosedDerived_NotAssignable_NonGenericBase_StillErrorDiagnostic()
    {
        // Non-generic base: there's no other instantiation to consider. Hard error preserved.
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Unrelated))]
            class NonGenericBase { }

            class Unrelated { }

            [GenerateShapeFor(typeof(NonGenericBase))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public static void ClosedDerived_NoRelationToBaseDefinition_StillErrorDiagnostic()
    {
        // Even with a generic base, a derived type that has NO inheritance relation to any
        // instantiation of the base is a hard misregistration.
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Unrelated))]
            class GenericBase<T> { }

            class Unrelated { }

            [GenerateShapeFor(typeof(GenericBase<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public static void OpenGenericDerivedType_UnificationFailed_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(Wrapped<>))]
            class Base<T> { }

            class Wrapped<T> : Base<List<T>> { }

            [GenerateShapeFor(typeof(Base<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("arguments do not match", diagnostic.GetMessage());
    }

    [Fact]
    public static void OpenGenericDerivedType_InterfaceConstraint_InvalidSpecializationProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(Derived<>))]
            class Base<T> { }

            class Derived<T> : Base<T> where T : IEnumerable<int> { }

            [GenerateShapeFor(typeof(Base<System.Collections.Generic.List<int>>))]
            [GenerateShapeFor(typeof(Base<string>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("constraint", diagnostic.GetMessage());
        Assert.Contains("Base<string>", diagnostic.GetMessage());
    }

    [Theory]
    [MemberData(nameof(GetInvalidOpenGenericSpecializationCases))]
    public static void OpenGenericDerivedType_InvalidSpecialization_ErrorDiagnostic(
        string source,
        string expectedDerivedType,
        string expectedBaseType,
        string expectedReason)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation(source);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(expectedDerivedType, diagnostic.GetMessage());
        Assert.Contains(expectedBaseType, diagnostic.GetMessage());
        Assert.Contains(expectedReason, diagnostic.GetMessage());
    }

    public static IEnumerable<object[]> GetInvalidOpenGenericSpecializationCases()
    {
        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Derived<>))]
            class Base<T1, T2> { }
            class Derived<T> : Base<T, T> { }

            [GenerateShapeFor(typeof(Base<int, string>))]
            partial class Witness { }
            """,
            "Derived<>",
            "Base<int, string>",
            "arguments do not match",
        ];

        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Derived<>))]
            class Base<T1, T2> { }
            class Derived<T> : Base<T, int> { }

            [GenerateShapeFor(typeof(Base<string, string>))]
            partial class Witness { }
            """,
            "Derived<>",
            "Base<string, string>",
            "arguments do not match",
        ];

        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Derived<>))]
            class Base<T> { }
            class Derived<T> : Base<T> where T : class, new() { }
            class NoDefaultConstructorArgument
            {
                public NoDefaultConstructorArgument(int value) { }
            }

            [GenerateShapeFor(typeof(Base<NoDefaultConstructorArgument>))]
            partial class Witness { }
            """,
            "Derived<>",
            "Base<NoDefaultConstructorArgument>",
            "constraint",
        ];

        yield return
        [
            """
            using PolyType;
            using System.Collections.Generic;

            [DerivedTypeShape(typeof(Silly<>))]
            class Animal<T> { }
            class Silly<T> : Animal<List<T[][][]>> { }

            [GenerateShapeFor(typeof(Animal<List<int[][]>>))]
            partial class Witness { }
            """,
            "Silly<>",
            "Animal<System.Collections.Generic.List<int[][]>>",
            "arguments do not match",
        ];

        yield return
        [
            """
            using PolyType;

            [DerivedTypeShape(typeof(Derived<>))]
            class Base<T> { }

            class Outer<T>
            {
                public class Box<U> { }
            }

            class Derived<T> : Base<Outer<int>.Box<T>> { }

            [GenerateShapeFor(typeof(Base<Outer<string>.Box<int>>))]
            partial class Witness { }
            """,
            "Derived<>",
            "Base<Outer<string>.Box<int>>",
            "arguments do not match",
        ];
    }

    [Fact]
    public static void OpenGenericDerivedType_OverlappingArities_HigherArityIsHardError()
    {
        // Cat<T,U> : Animal<T> has an extra type parameter U that no base spec can bind.
        // This is a fundamental misregistration (UnboundParameter) → hard error preserved.
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Cat<,>))]
            class Animal<T> { }

            class Cat<T, U> : Animal<T> { }

            [GenerateShapeFor(typeof(Animal<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("'U'", diagnostic.GetMessage());
        Assert.Contains("not bound", diagnostic.GetMessage());
    }

    [Fact]
    public static void OpenGenericDerivedType_OverlappingArities_BothRegistered_HigherArityErrors()
    {
        // User registers both Cat<T> and Cat<T,U>. The first is valid, the second has an
        // unbound parameter. The well-formed registration succeeds; the malformed one still
        // produces a diagnostic.
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Cat<>))]
            [DerivedTypeShape(typeof(Cat<,>))]
            class Animal<T> { }

            class Cat<T> : Animal<T> { }
            class Cat<T, U> : Animal<T> { }

            [GenerateShapeFor(typeof(Animal<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("'U'", diagnostic.GetMessage());
    }

    [Fact]
    public static void MixedValidAndInvalidDerived_InvalidRegistrationProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(IntSpecial))]
            [DerivedTypeShape(typeof(General<>))]
            class Base<T> { }

            class IntSpecial : Base<int> { }
            class General<T> : Base<T> where T : class { }

            [GenerateShapeFor(typeof(Base<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("constraint", diagnostic.GetMessage());
    }

    [Fact]
    public static void OpenGenericDerivedType_SameNameDifferentArities_ErrorDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(Cat<>), Name = "cat")]
            [DerivedTypeShape(typeof(Cat<,>), Name = "cat")]
            class Animal<T1, T2> { }

            class Cat<T> : Animal<T, T> { }
            class Cat<T1, T2> : Animal<T1, T2> { }

            [GenerateShapeFor(typeof(Animal<int, int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PT0012", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("name 'cat'", diagnostic.GetMessage());
    }

    [Fact]
    public static void MethodShape_GenericMethod_ProducesError()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial class MyPoco
            {
                [MethodShape]
                public void GenericMethod<T>(T value) { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0020", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal((6, 16), diagnostic.Location.GetStartPosition());
        Assert.Equal((6, 29), diagnostic.Location.GetEndPosition());
    }

    [Theory]
    [InlineData("public System.Span<int> MethodWithSpanReturn() => default;")]
    [InlineData("public void MethodWithReadOnlySpanParameter(System.ReadOnlySpan<byte> span) { }")]
    [InlineData("public unsafe int* MethodWithPointerReturn() => null;")]
    [InlineData("public unsafe void MethodWithPointerParameter(int* ptr) { }")]
    public static void MethodShape_UnsupportedParameterOrReturnType_ProducesWarning(string methodSignature)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;

            [GenerateShape]
            public partial class MyPoco
            {
                [MethodShape]
                {{methodSignature}}
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal("PT0021", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public static void DuplicatePropertyShapeName_AttributeConflict_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            partial class ConflictingPropertyNames
            {
                public int X { get; set; }
                [PropertyShape(Name = "X")]
                public int Y { get; set; }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "PT0022");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Conflicting members named 'X' were found", diagnostic.GetMessage());
        Assert.Contains("PropertyShape", diagnostic.GetMessage());
    }

    [Fact]
    public static void DuplicatePropertyShapeName_DiamondConflict_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            interface IPropA { int X { get; set; } }
            interface IPropB { int X { get; set; } }

            [GenerateShape]
            partial interface IPropC : IPropA, IPropB { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "PT0022");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Conflicting members named 'X' were found", diagnostic.GetMessage());
        Assert.Contains("PropertyShape", diagnostic.GetMessage());
    }

    [Fact]
    public static void DuplicateMethodShapeName_AttributeConflict_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
            partial class ConflictingMethodNames
            {
                public void M() { }
                [MethodShape(Name = "M")] public void N() { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "PT0022");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Conflicting members named 'M' were found", diagnostic.GetMessage());
        Assert.Contains("MethodShape", diagnostic.GetMessage());
    }

    [Fact]
    public static void DuplicateMethodShapeName_DiamondConflict_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            interface IM1 { void M(); }
            interface IM2 { void M(); }

            [GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
            partial interface IM3 : IM1, IM2 { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "PT0022");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Conflicting members named 'M' were found", diagnostic.GetMessage());
        Assert.Contains("MethodShape", diagnostic.GetMessage());
    }

    [Fact]
    public static void DuplicateEventShapeName_AttributeConflict_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System;

            [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
            partial class ConflictingEventNames
            {
                public event Action E; // included via IncludeMethods
                [EventShape(Name = "E")] public event Action F; // conflicting named via attribute
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "PT0022");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Conflicting members named 'E' were found", diagnostic.GetMessage());
        Assert.Contains("EventShape", diagnostic.GetMessage());
    }

    [Fact]
    public static void DuplicateEventShapeName_DiamondConflict_ProducesWarning()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System;

            interface IE1 { event Action E; }
            interface IE2 { event Action E; }

            [GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
            partial interface IE3 : IE1, IE2 { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "PT0022");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Conflicting members named 'E' were found", diagnostic.GetMessage());
        Assert.Contains("EventShape", diagnostic.GetMessage());
    }

    [Fact]
    public static void PolymorphicClassWithGenericDerivedTypes_AutoGeneratedNames_NoDiagnostic()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            [DerivedTypeShape(typeof(Horse))]
            [DerivedTypeShape(typeof(Cow<SolidHoof>), Tag = 1)]
            [DerivedTypeShape(typeof(Cow<ClovenHoof>), Tag = 2)]
            partial record Animal(string Name)
            {
                public record Horse(string Name) : Animal(Name);
                public record Cow<THoof>(string Name, THoof Hoof) : Animal(Name);
                public record SolidHoof;
                public record ClovenHoof;
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);

        // Should have no diagnostics - names are auto-generated as "Cow_SolidHoof" and "Cow_ClovenHoof"
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void Diagnostic_CanBeSuppressedViaPragma()
    {
        // Verify that generator diagnostics can be suppressed using #pragma warning disable.
        // This is a regression test for https://github.com/dotnet/runtime/issues/92509.
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial class MyPoco
            {
            #pragma warning disable PT0020
                [MethodShape]
                public void GenericMethod<T>(T value) { }
            #pragma warning restore PT0020
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);

        // With a proper SourceLocation, the diagnostic should either be absent
        // or marked as IsSuppressed by the pragma directive.
        Diagnostic? diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == "PT0020");
        Assert.True(diagnostic is null || diagnostic.IsSuppressed,
            $"Expected PT0020 to be suppressed by #pragma, but it was present with IsSuppressed={diagnostic?.IsSuppressed}");
    }
}
