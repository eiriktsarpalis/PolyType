﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static class CompilationTests
{
    [Fact]
    public static void CompileSimplePoco_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections.Generic;

            [GenerateShape]
            public partial class MyPoco
            {
                public MyPoco(bool @bool = true, string @string = "str")
                {
                    Bool = @bool;
                    String = @string;
                }

                public bool Bool { get; }
                public string String { get; }
                public List<int>? List { get; set; }
                public HashSet<int>? HashSet { get; set; }
                public Dictionary<string, int>? Dict { get; set; }

            #if NET8_0_OR_GREATER
                public static PolyType.Abstractions.ITypeShape<MyPoco> Test()
                    => PolyType.Abstractions.TypeShapeProvider.Resolve<MyPoco>();
            #endif
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CompileSimpleRecord_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial record MyRecord(string value);
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShapeExtensionWithAssociatedTypes()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;

            [assembly: TypeShapeExtension(typeof(GenericClass<,>), Requirements = TypeShapeRequirements.Constructor, AssociatedTypes = new Type[] { typeof(GenericConverter<,>) })]

            public class GenericClass<T1, T2> { }
            public class GenericConverter<T1, T2> { }

            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShapeWithAssociatedTypes()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [AssociatedTypeShape(typeof(GenericConverter<,>))]
            public class GenericClass<T1, T2> { }
            public class GenericConverter<T1, T2> { }

            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShapeWithAssociatedShapes()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [AssociatedTypeShape(typeof(GenericHelper<,>))]
            public class GenericClass<T1, T2> { }
            public class GenericHelper<T1, T2> { }

            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShapeExtensionWithAssociatedShapes()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;

            [assembly: TypeShapeExtension(typeof(GenericClass<,>), AssociatedTypes = new Type[] { typeof(GenericHelper<,>) })]

            public class GenericClass<T1, T2> { }
            public class GenericHelper<T1, T2> { }

            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShapeExtensionWithAssociatedShapes_UnionFlags()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;

            [assembly: TypeShapeExtension(typeof(GenericClass<,>), Requirements = TypeShapeRequirements.Constructor, AssociatedTypes = new Type[] { typeof(GenericHelper<,>) })]
            [assembly: TypeShapeExtension(typeof(GenericClass<,>), Requirements = TypeShapeRequirements.Properties, AssociatedTypes = new Type[] { typeof(GenericHelper<,>) })]

            public class GenericClass<T1, T2> { }
            public class GenericHelper<T1, T2> { public int Prop { get; set; } }

            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void UnionShapeDepthFlags_AssociatedTypeFirst()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;

            [assembly: TypeShapeExtension(typeof(GenericClass<,>), Requirements = TypeShapeRequirements.Constructor, AssociatedTypes = new Type[] { typeof(GenericHelper<,>) })]

            public class GenericClass<T1, T2> { }
            public class GenericHelper<T1, T2> { public int Prop { get; set; } }

            public record AnotherShapeReference(GenericHelper<int, string> helper);

            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            [GenerateShapeFor(typeof(AnotherShapeReference))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void UnionShapeDepthFlags_FullShapeReferenceFirst()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;

            [assembly: TypeShapeExtension(typeof(GenericClass<,>), Requirements = TypeShapeRequirements.Constructor, AssociatedTypes = new Type[] { typeof(GenericHelper<,>) })]

            public class GenericClass<T1, T2> { }
            public class GenericHelper<T1, T2> { public int Prop { get; set; } }

            public record AnotherShapeReference(GenericHelper<int, string> helper);

            [GenerateShapeFor(typeof(AnotherShapeReference))]
            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void AssociatedTypeAttribute_Shapes()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;
            using PolyType.Abstractions;

            [AssociatedTypeAttribute("converterType", TypeShapeRequirements.Constructor)]
            [AssociatedTypeAttribute(nameof(Shapes), TypeShapeRequirements.Full)]
            [AttributeUsage(AttributeTargets.Class)]
            public class CustomAttribute : Attribute
            {
                public CustomAttribute(Type converterType) => ConverterType = converterType;

                public Type ConverterType { get; }

                public Type[] Shapes { get; set; } = Type.EmptyTypes;
            }

            [Custom(typeof(GenericConverter<,>), Shapes = new Type[] { typeof(GenericHelper<,>) })]
            public class GenericClass<T1, T2> { }
            public class GenericConverter<T1, T2> { }
            public record GenericHelper<T1, T2>(T1 Value) { }

            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void AssociatedTypeAttribute_RequirementsNone()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [AssociatedTypeShape(typeof(GenericHelper<,>), Requirements = TypeShapeRequirements.None)]
            public class GenericClass<T1, T2> { }
            public class GenericHelper<T1, T2> { }

            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShapeWithAssociatedTypes_Duplicates()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [AssociatedTypeShape(typeof(GenericConverter<,>), typeof(GenericConverter<,>))]
            public class GenericClass<T1, T2> { }
            public class GenericConverter<T1, T2> { }

            [GenerateShapeFor(typeof(GenericClass<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void TypeShapeWithAssociatedTypes_GenericNestedInGeneric()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            namespace PolyType.Tests
            {
                public partial class AssociatedTypesTests
                {
                    [AssociatedTypeShape(typeof(GenericWrapper<>.GenericNested<>))]
                    public class GenericClass<T1, T2> { }

                    public class GenericWrapper<T1>
                    {
                        public class GenericNested<T2> { }
                    }

                    [GenerateShapeFor(typeof(GenericClass<int, string>))]
                    public partial class Witness { }
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("AssociatedTypes", "true")]
    public static void AssociatedTypeAttribute_Usage()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType.Abstractions;

            namespace PolyType.Tests
            {
                [AssociatedTypeAttribute("Factories", TypeShapeRequirements.Constructor)]
                [AssociatedTypeAttribute("Shapes", TypeShapeRequirements.Full)]
                [AttributeUsage(AttributeTargets.Class)]
                public class CustomAttribute : Attribute
                {
                    public Type? Factory { get; set; }

                    public Type? Shape { get; set; }
                }

                [CustomAttribute(Factory = typeof(AnotherClass<>), Shape = typeof(YetAnotherClass<>))]
                public class GenericClass<T> { }

                public class AnotherClass<T> { }

                public class YetAnotherClass<T> { }

                [GenerateShapeFor(typeof(GenericClass<int>))]
                public partial class Witness { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CompileSimpleCollection_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections;

            [GenerateShapeFor(typeof(ICollection))]
            public partial class MyWitness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CompileClassWithMultipleSetters_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial class ClassWithParameterizedConstructorAndMultiplePropertySetters
            {
                public ClassWithParameterizedConstructorAndMultiplePropertySetters(int x00) => this.X00 = x00;

                public int X00 { get; set; }

                public int X01 { get; set; }
                public int X02 { get; set; }
                public int X03 { get; set; }
                public int X04 { get; set; }
                public int X05 { get; set; }
                public int X06 { get; set; }
                public int X07 { get; set; }
                public int X08 { get; set; }
                public int X09 { get; set; }
                public int X10 { get; set; }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CtorWithNullableGenericParameterAndDefault()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            public record GenericClass<T>(T? Value = default);

            [GenerateShapeFor(typeof(GenericClass<int>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CtorWithNullableGenericParameterAndDefault_ValueConstrained()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            public record GenericClass<T>(T? Value = default) where T : struct;

            [GenerateShapeFor(typeof(GenericClass<int>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ClassWithSetsRequiredMembersConstructor_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Diagnostics.CodeAnalysis;

            [GenerateShape]
            public partial class MyClass
            {
                [SetsRequiredMembers]
                public MyClass(int value)
                {
                    Value = value;
                }

                public required int Value { get; set; }
            }
            """, parseOptions: CompilationHelpers.CreateParseOptions(LanguageVersion.CSharp12));

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public static void UseTypesWithNullableAnnotations_NoWarnings()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections.Generic;
            #nullable enable

            public static class Test
            {
                public static void TestMethod()
                {
                    Dictionary<int, string?> dict = new();
                    GenericMethods.TestTypeShapeProvider(dict, new MyProvider());
                }
            }

            public static class GenericMethods
            {
                public static void TestTypeShapeProvider<T, TProvider>(T value, TProvider provider)
                    where TProvider : IShapeable<T> { }
            }

            [GenerateShapeFor(typeof(Dictionary<int, string>))]
            public partial class MyProvider { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
#endif

    [Fact]
    public static void DerivedClassWithShadowedMembers_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            #nullable enable

            public record BaseClassWithShadowingMembers
            {
                public string? PropA { get; init; }
                public string? PropB { get; init; }
                public int FieldA;
                public int FieldB;
            }

            [GenerateShape]
            public partial record DerivedClassWithShadowingMember : BaseClassWithShadowingMembers
            {
                public new string? PropA { get; init; }
                public required new int PropB { get; init; }
                public new int FieldA;
                public required new string FieldB;
            }
            """, parseOptions: CompilationHelpers.CreateParseOptions(LanguageVersion.CSharp11));

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ExpandoObject()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShapeFor(typeof(System.Dynamic.ExpandoObject))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void IsRequiredProperty()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            #nullable enable

            [GenerateShape]
            public partial class Foo
            {
                [PropertyShape(IsRequired = true)]
                public string? PropA { get; set; }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MultiplePartialContextDeclarations_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            #if NET8_0_OR_GREATER
            public static class Test
            {
                public static void TestMethod()
                {
                    PolyType.Abstractions.ITypeShape<string> stringShape = PolyType.Abstractions.TypeShapeProvider.Resolve<string, MyWitness>();
                    PolyType.Abstractions.ITypeShape<int> intShape = PolyType.Abstractions.TypeShapeProvider.Resolve<int, MyWitness>();
                }
            }
            #endif

            [GenerateShapeFor(typeof(int))]
            public partial class MyWitness { }

            [GenerateShapeFor(typeof(string))]
            public partial class MyWitness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void EnumGeneration_NoErrors()
    {
        // Regression test for https://github.com/eiriktsarpalis/PolyType/issues/29
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;

           enum MyEnum { A, B, C }

           [GenerateShapeFor(typeof(MyEnum))]
           partial class Witness { }
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void XmlDocumentGeneration_GenerateShapeOfT_NoErrors()
    {
        // Regression test for https://github.com/eiriktsarpalis/PolyType/issues/35
        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(documentationMode: DocumentationMode.Diagnose);
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;

           /// <summary>My poco.</summary>
           public class MyPoco<T> { }

           /// <summary>My Witness.</summary>
           [GenerateShapeFor(typeof(MyPoco<int>))]
           public partial class Witness { }
           """,
           parseOptions: parseOptions);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void XmlDocumentGeneration_GenerateShape_NoErrors()
    {
        // Regression test for https://github.com/eiriktsarpalis/PolyType/issues/35
        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(documentationMode: DocumentationMode.Diagnose);
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;

           /// <summary>My poco.</summary>
           [GenerateShape]
           public partial class MyPoco { }
           """,
           parseOptions: parseOptions);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void TypeUsingKeywordIdentifiers_GenerateShape_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            partial class @class
            {
                public @class(string @string, int @__makeref, bool @yield)
                {
                    this.@string = @string;
                    this.@__makeref = @__makeref;
                    this.yield = yield;
                }

                public string @string { get; set; }
                public int @__makeref { get; set; }
                public bool @yield { get; set; }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void NestedWitnessClass_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            internal partial class Outer1
            {
                public partial class Outer2
                {
                    [GenerateShapeFor(typeof(MyPoco))]
                    private partial class Witness { }

                    internal record MyPoco(int Value);
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("partial class")]
    [InlineData("sealed partial class")]
    [InlineData("partial struct")]
    [InlineData("partial record")]
    [InlineData("sealed partial record")]
    [InlineData("partial record struct")]
    public static void SupportedWitnessTypeKinds_NoErrors(string kind)
    {
        CSharpParseOptions? parseOptions = kind.Contains("record struct") ? CompilationHelpers.CreateParseOptions(LanguageVersion.CSharp12) : null;
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;
            using PolyType.Abstractions;

            ITypeShape<MyPoco> shape;
            #if NET8_0_OR_GREATER
            shape = TypeShapeProvider.Resolve<MyPoco, Witness>();
            #endif
            shape = TypeShapeProvider.Resolve<MyPoco>(Witness.ShapeProvider);

            record MyPoco(string[] Values);

            [GenerateShapeFor(typeof(MyPoco))]
            {{kind}} Witness { }
            """, outputKind: OutputKind.ConsoleApplication, parseOptions: parseOptions);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ConflictingTypeNames_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            public partial class MyPoco { }

            namespace Foo.Bar
            {
                partial class Container
                {
                    [GenerateShape]
                    public partial class MyPoco { }
                }
            }

            namespace Foo.Baz
            {
                partial class Container
                {
                    [GenerateShape]
                    public partial class MyPoco { }
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ConflictingTypeNamesInNestedGenerics_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            class Container<T>
            {
                public partial class MyPoco { }
            }

            [GenerateShapeFor(typeof(Container<int>.MyPoco))]
            [GenerateShapeFor(typeof(Container<string>.MyPoco))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void TypesUsingReservedIdentifierNames_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            partial class Default { }

            [GenerateShape]
            partial class GetShape { }

            [GenerateShape]
            partial class @class { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CustomTypeShapeKind_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape, TypeShape(Kind = TypeShapeKind.None)]
            public partial record ObjectAsNone(string Name, int Age);
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void TupleTypes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;
           using System;

           [GenerateShapeFor(typeof((int, int, int, int, int, int, int, int, int, int)))]
           [GenerateShapeFor(typeof(System.Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>))]
           partial class Witness { }
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void NullableTypes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
           using PolyType;
           using System;

           [GenerateShapeFor(typeof(int?))]
           [GenerateShapeFor(typeof(Guid?))]
           partial class Witness { }
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("int[,]")]
    [InlineData("System.ReadOnlyMemory<int>")]
    [InlineData("System.Memory<int>")]
    [InlineData("System.Collections.Immutable.ImmutableArray<int>")]
    [InlineData("System.Collections.Immutable.ImmutableList<int>")]
    [InlineData("System.Collections.Immutable.ImmutableQueue<int>")]
    [InlineData("System.Collections.Immutable.ImmutableStack<int>")]
    [InlineData("System.Collections.Immutable.ImmutableHashSet<int>")]
    [InlineData("System.Collections.Immutable.ImmutableSortedSet<int>")]
    [InlineData("System.Collections.Generic.IEnumerable<int>")]
    [InlineData("System.Collections.Generic.ISet<int>")]
    [InlineData("System.Collections.Generic.List<int>")]
    [InlineData("System.Collections.Generic.HashSet<int>")]
    public static void EnumerableTypes_NoErrors(string type)
    {
        if (CompilationHelpers.IsMonoRuntime && type.Contains("Memory<"))
        {
            return; // Ambiguity between types in System.Memory and mscorlib.
        }

        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
           using PolyType;

           [GenerateShapeFor(typeof({{type}}))]
           partial class Witness { }
           """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DictionaryTypes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Collections;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Linq;
            
            [GenerateShapeFor(typeof(Dictionary<string, int>))]
            [GenerateShapeFor(typeof(ImmutableDictionary<string, int>))]
            [GenerateShapeFor(typeof(ImmutableSortedDictionary<string, int>))]
            [GenerateShapeFor(typeof(IReadOnlyDictionary<string, int>))]
            [GenerateShapeFor(typeof(IDictionary))]
            [GenerateShapeFor(typeof(Hashtable))]
            [GenerateShapeFor(typeof(CustomDictionary1))]
            partial class Witness { }
            
            class CustomDictionary1 : Dictionary<string, int>
            {
                public CustomDictionary1(IEnumerable<KeyValuePair<string, int>> inner)
                    : base(inner.ToDictionary(kv => kv.Key, kv => kv.Value))
                {
                }
            }

            class CustomDictionary2 : Dictionary<string, int>
            {
                public CustomDictionary2(Dictionary<string, int> inner) : base(inner) { }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void PrivateMemberAccessors_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            #pragma warning disable CS0169

            [GenerateShape]
            partial class MyPoco
            {
                [PropertyShape]
                private int PrivateField;
                [PropertyShape]
                private int PrivateProperty { get; set; }

                [ConstructorShape]
                private MyPoco(int privateField, int privateProperty)
                {
                    PrivateField = privateField;
                    PrivateProperty = privateProperty;
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DefaultConstructorParameters_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System;
            #nullable enable

            [GenerateShape]
            public partial record RecordWithNullableDefaultParams2(ulong? x1 = 10, float? x2 = 3.1f, double? x3 = 3.1d, decimal? x4 = -3.1415926m, string? x5 = "str", string? x6 = null, object? x7 = null);

            [GenerateShape]
            public partial record RecordWithSpecialValueDefaultParams(
                double d1 = double.PositiveInfinity, double d2 = double.NegativeInfinity, double d3 = double.NaN,
                double? dn1 = double.PositiveInfinity, double? dn2 = double.NegativeInfinity, double? dn3 = double.NaN,
                float f1 = float.PositiveInfinity, float f2 = float.NegativeInfinity, float f3 = float.NaN,
                float? fn1 = float.PositiveInfinity, float? fn2 = float.NegativeInfinity, float? fn3 = float.NaN,
                string s = "\"😀葛🀄\r\n🤯𐐀𐐨\"", char c = '\'');

            [Flags]
            public enum MyEnum { A = 1, B = 2, C = 4, D = 8, E = 16, F = 32, G = 64, H = 128 }

            [GenerateShape]
            public partial record RecordWithEnumAndNullableParams(MyEnum flags1, MyEnum? flags2, MyEnum flags3 = MyEnum.A, MyEnum? flags4 = null);

            [GenerateShape]
            public partial record RecordWithNullableDefaultEnum(MyEnum? flags = MyEnum.A | MyEnum.B);

            [GenerateShape]
            public partial record LargeClassRecord(
                int x0 = 0, int x1 = 1, int x2 = 2, int x3 = 3, int x4 = 4, int x5 = 5, int x6 = 5,
                int x7 = 7, int x8 = 8, string x9 = "str", LargeClassRecord? nested = null);
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void NullabilityAttributes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;
            using System.Diagnostics.CodeAnalysis;

            #nullable enable

            [GenerateShape]
            public partial class ClassWithNullabilityAttributes
            {
                private string? _maybeNull = "str";
                private string? _allowNull = "str";
                private string? _notNull = "str";
                private string? _disallowNull = "str";

                public ClassWithNullabilityAttributes() { }

                public ClassWithNullabilityAttributes([AllowNull] string allowNull, [DisallowNull] string? disallowNull)
                {
                    _allowNull = allowNull;
                    _disallowNull = disallowNull;
                }

                [MaybeNull]
                public string MaybeNullProperty
                {
                    get => _maybeNull;
                    set => _maybeNull = value;
                }

                [AllowNull]
                public string AllowNullProperty
                {
                    get => _allowNull ?? "str";
                    set => _allowNull = value;
                }

                [NotNull]
                public string? NotNullProperty
                {
                    get => _notNull ?? "str";
                    set => _notNull = value;
                }

                [DisallowNull]
                public string? DisallowNullProperty
                {
                    get => _disallowNull;
                    set => _disallowNull = value;
                }

                [MaybeNull]
                public string MaybeNullField = "str";
                [AllowNull]
                public string AllowNullField = "str";
                [NotNull]
                public string? NotNullField = "str";
                [DisallowNull]
                public string? DisallowNullField = "str";
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void PolymorphicClass_NoWarnings()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [GenerateShape]
            [DerivedTypeShape(typeof(PolymorphicClass))]
            [DerivedTypeShape(typeof(Derived))]
            public partial record PolymorphicClass(int Int)
            {
                public record Derived(int Int, string String) : PolymorphicClass(Int);
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void PolymorphicGenericClass_NoWarnings()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [DerivedTypeShape(typeof(GenericTree<>.Leaf), Name = "leaf", Tag = 10)]
            [DerivedTypeShape(typeof(GenericTree<>.Node), Name = "node", Tag = 1000)]
            public partial record GenericTree<T>
            {
                public record Leaf : GenericTree<T>;
                public record Node(T Value, GenericTree<T> Left, GenericTree<T> Right) : GenericTree<T>;
            }

            [GenerateShapeFor(typeof(GenericTree<string>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void FSharpUnion_NoWarnings()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using Microsoft.FSharp.Core;
            using PolyType;

            [GenerateShapeFor(typeof(FSharpOption<int>))]
            [GenerateShapeFor(typeof(FSharpValueOption<int>))]
            [GenerateShapeFor(typeof(FSharpResult<string, int>))]
            [GenerateShapeFor(typeof(FSharpChoice<int, string, bool>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void HashSet()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections.Generic;
            using PolyType;

            [GenerateShapeFor(typeof(HashSet<string>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void Dictionary()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections.Generic;
            using PolyType;

            [GenerateShapeFor(typeof(Dictionary<string, int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ConcurrentDictionary()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections.Concurrent;
            using PolyType;

            [GenerateShapeFor(typeof(ConcurrentDictionary<string, int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ImmutableDictionary()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections.Immutable;
            using PolyType;

            [GenerateShapeFor(typeof(ImmutableDictionary<string, int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void SortedDictionary()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections.Generic;
            using PolyType;

            [GenerateShapeFor(typeof(SortedDictionary<string, int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void SortedSet()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections.Generic;
            using PolyType;

            [GenerateShapeFor(typeof(SortedSet<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ReadOnlyDictionary()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections.ObjectModel;
            using PolyType;

            [GenerateShapeFor(typeof(ReadOnlyDictionary<int, int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void DictionaryWithSpanCtor()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using System.Collections.Generic;
            using PolyType;

            [GenerateShape]
            public partial class DictionaryWithSpanCtor : Dictionary<string, int>
            {
                public DictionaryWithSpanCtor(ReadOnlySpan<KeyValuePair<string, int>> values)
                {
                    foreach (var value in values)
                    {
                        this[value.Key] = value.Value;
                    }
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CollectionWithSpanCtor()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using System.Collections.Generic;
            using PolyType;

            [GenerateShape]
            public partial class CollectionWithSpanCtor : List<int>
            {
                public CollectionWithSpanCtor(ReadOnlySpan<int> values)
                {
                    foreach (var value in values)
                    {
                        Add(value);
                    }
                }
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MemoryOfInt()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;

            [GenerateShapeFor(typeof(Memory<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ImmutableQueueOfInt()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections.Immutable;
            using PolyType;

            [GenerateShapeFor(typeof(ImmutableQueue<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CustomCollection()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;
            using PolyType;

            public class CollectionWithNullableElement<T> : IEnumerable<(T?, int)>
            {
                public CollectionWithNullableElement(IEnumerable<(T?, int)> values) => _values = values.ToArray();
                private readonly (T?, int)[] _values;
                public IEnumerator<(T?, int)> GetEnumerator() => ((IEnumerable<(T?, int)>)_values).GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
            }

            [GenerateShapeFor(typeof(CollectionWithNullableElement<int>))]
            partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CustomCollection2()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System.Collections;
            using System.Collections.Generic;
            using PolyType;

            [GenerateShape]
            internal partial class EnumerableEnumerableEC : IEnumerable<int>
            {
                private readonly IEnumerable<int> values;
                
                public EnumerableEnumerableEC(IEnumerable<int> values, IEqualityComparer<int>? eq)
                {
                    this.Comparer = eq ?? EqualityComparer<int>.Default;
                    this.values = values;
                }
                public EnumerableEnumerableEC(IEnumerable<int> values) : this(values, null) { }

                public IEqualityComparer<int> Comparer { get; }

                public IEnumerator<int> GetEnumerator() => this.values.GetEnumerator();

                IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CustomCollection3()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using PolyType;

            [GenerateShape]
            internal partial class DictionarySpan : IReadOnlyDictionary<int, bool>
            {
                private Dictionary<int, bool> inner;

                internal DictionarySpan(ReadOnlySpan<KeyValuePair<int, bool>> span)
                    : this(span, null)
                {
                }

                internal DictionarySpan(ReadOnlySpan<KeyValuePair<int, bool>> span, IEqualityComparer<int>? ec)
                {
                    this.inner = new(ec);
                    foreach (var item in span)
                    {
                        this.inner.Add(item.Key, item.Value);
                    }
                }

                public IEqualityComparer<int> Comparer => inner.Comparer;

                public bool this[int key] => throw new NotImplementedException();

                public IEnumerable<int> Keys => throw new NotImplementedException();

                public IEnumerable<bool> Values => throw new NotImplementedException();

                public int Count => this.inner.Count;

                public bool ContainsKey(int key) => throw new NotImplementedException();

                public IEnumerator<KeyValuePair<int, bool>> GetEnumerator() => this.inner.GetEnumerator();

                public bool TryGetValue(int key, out bool value) => throw new NotImplementedException();

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void EnumWithCustomNames()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using PolyType;

            internal enum MyEnum
            {
                [EnumMemberShape(Name = "A2")]
                A = 1,
                B,
                [EnumMemberShape(Name = "E2")]
                E,
            }

            [GenerateShapeFor(typeof(MyEnum))]
            internal partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void ObsoleteMembers()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;

            internal class MyObject
            {
                [Obsolete]
                public int ObsoleteProperty { get; set; }
            }

            [GenerateShapeFor(typeof(MyObject))]
            internal partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
}
