using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.SourceGenerator.Model;
using System.Reflection;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    private const string CSharpUnionContracts = """
        namespace System.Runtime.CompilerServices
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, Inherited = false)]
            public sealed class UnionAttribute : System.Attribute { }
            public interface IUnion { object? Value { get; } }
        }
        """;

    [Theory]
    [InlineData("""
        using PolyType;
        [GenerateShape]
        public partial union U(int, string?);
        """, "U")]
    [InlineData("""
        using PolyType;
        public union U<T>(T, string?);
        [GenerateShapeFor(typeof(U<int>))]
        public partial class Witness { }
        """, "U_Int32")]
    [InlineData("""
        using PolyType;
        public partial union Container(int)
        {
            [GenerateShape]
            public partial union U(int, string?);
        }
        """, "U")]
    [InlineData("""
        using PolyType;
        [System.Runtime.CompilerServices.Union, GenerateShape]
        public partial class U
        {
            public U(in int value) => Value = value;
            public U(string? value) => Value = value;
            public object? Value { get; }
        }
        """, "U")]
    public static void CSharpUnion_ProjectsCasesAndEmptyBase(string source, string identifier)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation(source));
        CSharpUnionShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
        Assert.Equal(identifier, model.SourceIdentifier);
        Assert.False(model.IsContextual);
        Assert.True(model.UnderlyingModel.IsContextual);
        Assert.Equal(["Int32", "String"], model.UnionCases.Select(c => c.Name));
        Assert.Equal([false, true], model.UnionCases.Select(c => c.IsNullable));
        Assert.Equal([0, 1], model.UnionCases.Select(c => c.Index));
        Assert.Equal(1, model.NullableCaseIndex);
        Assert.Null(model.UnderlyingModel.Constructor);
        Assert.Empty(model.UnderlyingModel.Properties);

        string generated = GetCSharpUnionGeneratedSource(result, model.SourceIdentifier);
        Assert.Contains("UnionKind = global::PolyType.Abstractions.UnionTypeShapeKind.CSharpUnion", generated);
        Assert.Contains("return value switch", generated);
        Assert.Contains("{ Value: int } => 0", generated);
        Assert.Contains("switch (value)", generated);
        Assert.Contains("case { Value: int caseValue }:", generated);
        Assert.Contains("case { Value: string caseValue }:", generated);
        Assert.Contains("case null:", generated);
        Assert.DoesNotContain("if (value is", generated);
        Assert.DoesNotContain("__UnionValue_", generated);
        Assert.DoesNotContain("object? payload", generated);
        Assert.DoesNotContain("default:", generated);
        Assert.Contains("__ThrowInvalidUnionCase(nameof(value));", generated);
        Assert.Contains("return default;", generated);
        Assert.DoesNotContain("throw ", generated);
        Assert.DoesNotContain("new global::System.ArgumentException", generated);
        Assert.DoesNotContain("_ =>", generated);
        MethodDeclarationSyntax throwStub = Assert.Single(
            result.NewCompilation.SyntaxTrees.SelectMany(tree => tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>()),
            method => method.Identifier.ValueText == "__ThrowInvalidUnionCase");
        Assert.Null(throwStub.TypeParameterList);
        Assert.IsType<ThrowExpressionSyntax>(throwStub.ExpressionBody!.Expression);
        Assert.Contains("private static void __ThrowInvalidUnionCase(string paramName)", throwStub.ToString());
        Assert.Contains("MethodImplOptions.NoInlining", throwStub.ToString());
        Assert.Contains("""new global::System.ArgumentException("The union value does not match this case.", paramName)""", throwStub.ToString());
        Assert.All(result.GeneratedModels.SelectMany(m => m.AnnotatedTypes), declaration =>
        {
            if (!declaration.IsWitnessTypeDeclaration)
            {
                Assert.DoesNotContain("(", declaration.TypeDeclarationHeader);
            }
        });
    }

    [Theory]
    [InlineData("class")]
    [InlineData("struct")]
    public static void CSharpUnion_UsesFixedCreatorsAndMostSpecificPayload(string kind)
    {
        string actual = ExecuteCSharpUnionSource($$"""
            using System;
            using PolyType;
            using PolyType.Abstractions;
            public class Animal { }
            public class Dog : Animal { }
            public class Wolf : Animal { }
            [System.Runtime.CompilerServices.Union]
            public {{kind}} U
            {
                public U(Animal? value) { Value = value; Creator = "animal"; }
                public U(Dog? value) { Value = value; Creator = "dog"; }
                public U(int value) { Value = value; Creator = "int"; }
                public U(object? value, bool ignored) { Value = value; Creator = "other"; }
                public object? Value { get; }
                public string Creator { get; }
                public static implicit operator U(int value) => throw new InvalidOperationException("op_Implicit");
            }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    var shape = (IUnionTypeShape<U>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(U))!;
                    var animal = ((IUnionCaseShape<Animal, U>)shape.UnionCases[0]).Marshaler;
                    var dog = ((IUnionCaseShape<Dog, U>)shape.UnionCases[1]).Marshaler;
                    var number = ((IUnionCaseShape<int, U>)shape.UnionCases[2]).Marshaler;
                    var payload = new Dog();
                    U value = animal.Marshal(payload)!;
                    int dogIndex = shape.GetGetUnionCaseIndex()(ref value);
                    bool samePayload = ReferenceEquals(payload, dog.Unmarshal(value));
                    string creator = value.Creator;
                    value = animal.Marshal(new Wolf())!;
                    int wolfIndex = shape.GetGetUnionCaseIndex()(ref value);
                    value = number.Marshal(42)!;
                    string numberCreator = value.Creator;
                    value = animal.Marshal(null)!;
                    int nullPayloadIndex = shape.GetGetUnionCaseIndex()(ref value);
                    bool nullExtracted = animal.Unmarshal(value) is null;
                    int nullWrapperIndex = 0;
                    {{(kind == "class" ? "value = null!; nullWrapperIndex = shape.GetGetUnionCaseIndex()(ref value);" : "")}}
                    value = new U(new object(), false);
                    bool rejectsInvalid = false;
                    try { shape.GetGetUnionCaseIndex()(ref value); }
                    catch (InvalidOperationException) { rejectsInvalid = true; }
                    bool rejectsWrongCase = false;
                    try { dog.Unmarshal(number.Marshal(1)); }
                    catch (ArgumentException ex) { rejectsWrongCase = ex.ParamName == "value"; }
                    return $"{creator},{dogIndex},{samePayload},{wolfIndex},{numberCreator},{nullPayloadIndex},{nullWrapperIndex},{nullExtracted},{rejectsInvalid},{rejectsWrongCase}";
                }
            }
            """);
        Assert.Equal("animal,1,True,0,int,0,0,True,True,True", actual);
    }

    [Theory]
    [InlineData("class")]
    [InlineData("struct")]
    public static void CSharpUnion_TryGetValueOrdersExactCaseTypes(string kind)
    {
        string actual = ExecuteCSharpUnionSource($$"""
            using System;
            using PolyType;
            using PolyType.Abstractions;
            [System.Runtime.CompilerServices.Union]
            public {{kind}} U
            {
                private readonly object? _value;
                public U(int? value) => _value = value;
                public U(object? value) => _value = value;
                public U(int value) => _value = value;
                public object? Value => _value is int ? throw new InvalidOperationException("Value must not be read for an integer payload.") : _value;
                public bool HasValue => _value is not null;
                public bool TryGetValue(out int? value)
                {
                    value = _value as int?;
                    return value.HasValue;
                }
                public bool TryGetValue(out object? value)
                {
                    value = _value;
                    return value is not null;
                }
                public bool TryGetValue(out int value)
                {
                    value = _value is int number ? number : default;
                    return _value is int;
                }
            }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    ITypeShapeProvider[] providers =
                    [
                        Witness.GeneratedTypeShapeProvider,
                        PolyType.ReflectionProvider.ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = true }),
                        PolyType.ReflectionProvider.ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = false }),
                    ];
                    string result = "";
                    foreach (ITypeShapeProvider provider in providers)
                    {
                        var shape = (IUnionTypeShape<U>)provider.GetTypeShape(typeof(U))!;
                        var nullable = ((IUnionCaseShape<int?, U>)shape.UnionCases[0]).Marshaler;
                        var obj = ((IUnionCaseShape<object, U>)shape.UnionCases[1]).Marshaler;
                        var number = ((IUnionCaseShape<int, U>)shape.UnionCases[2]).Marshaler;
                        U value = new U((int?)42);
                        int numberIndex = shape.GetGetUnionCaseIndex()(ref value);
                        int extracted = number.Unmarshal(value);
                        int? nullableExtracted = nullable.Unmarshal(value);
                        value = new U((int?)null);
                        int nullIndex = shape.GetGetUnionCaseIndex()(ref value);
                        bool nullExtracted = nullable.Unmarshal(value) is null;
                        value = new U((object)"text");
                        int objectIndex = shape.GetGetUnionCaseIndex()(ref value);
                        result += $"{numberIndex},{nullIndex},{objectIndex},{extracted},{nullableExtracted},{nullExtracted},{obj.Unmarshal(value)};";
                    }
                    return result;
                }
            }
            """);
        Assert.Equal(string.Concat(Enumerable.Repeat("2,0,1,42,42,True,text;", 3)), actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CSharpUnion_TryGetValueUsesValueTypeReceiverCopies(bool success)
    {
        string actual = ExecuteCSharpUnionSource($$"""
            using PolyType;
            using PolyType.Abstractions;
            [System.Runtime.CompilerServices.Union]
            public struct U
            {
                private readonly bool _isNumber;
                private readonly bool _boolean;
                public U(int value) { Number = value; _isNumber = true; _boolean = false; }
                public U(bool value) { Number = 42; _isNumber = false; _boolean = value; }
                public int Number { get; private set; }
                public object Value => _isNumber ? Number : _boolean;
                public bool TryGetValue(out int value)
                {
                    value = Number++;
                    return _isNumber;
                }
            }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    ITypeShapeProvider[] providers =
                    [
                        Witness.GeneratedTypeShapeProvider,
                        PolyType.ReflectionProvider.ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = true }),
                        PolyType.ReflectionProvider.ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = false }),
                    ];
                    string result = "";
                    foreach (ITypeShapeProvider provider in providers)
                    {
                        var shape = (IUnionTypeShape<U>)provider.GetTypeShape(typeof(U))!;
                        var marshaler = ((IUnionCaseShape<int, U>)shape.UnionCases[0]).Marshaler;
                        var boolean = ((IUnionCaseShape<bool, U>)shape.UnionCases[1]).Marshaler;
                        U value = {{(success ? "new U(42)" : "new U(false)")}};
                        int index = shape.GetGetUnionCaseIndex()(ref value);
                        string payload = {{(success ? "marshaler.Unmarshal(value)" : "boolean.Unmarshal(value)")}}.ToString();
                        result += $"{index},{value.Number},{payload},{value.Number};";
                    }
                    return result;
                }
            }
            """);
        Assert.Equal(string.Concat(Enumerable.Repeat(success ? "0,42,42,42;" : "1,42,False,42;", 3)), actual);
    }

    [Fact]
    public static void CSharpUnion_NullableBoxingDoesNotCollapseMetadata()
    {
        string actual = ExecuteCSharpUnionSource("""
            using PolyType;
            using PolyType.Abstractions;
            public union U(int?, int, string?);
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    var shape = (IUnionTypeShape<U>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(U))!;
                    var nullable = ((IUnionCaseShape<int?, U>)shape.UnionCases[0]).Marshaler;
                    U value = nullable.Marshal(42);
                    int nonNullIndex = shape.GetGetUnionCaseIndex()(ref value);
                    int? number = nullable.Unmarshal(value);
                    value = nullable.Marshal(null);
                    int nullIndex = shape.GetGetUnionCaseIndex()(ref value);
                    bool nullExtracted = nullable.Unmarshal(value) is null;
                    return $"{shape.UnionCases.Count},{nonNullIndex},{number},{nullIndex},{nullExtracted},{shape.UnionCases[0].IsNullable},{shape.UnionCases[1].IsNullable},{shape.UnionCases[2].IsNullable}";
                }
            }
            """);
        Assert.Equal("3,1,42,0,True,True,False,True", actual);
    }

    [Theory]
    [InlineData("int?", 0)]
    [InlineData("int", -1)]
    public static void CSharpUnion_DefaultAndSoleNullableValueCase(string caseType, int nullIndex)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation($$"""
            using PolyType;
            [GenerateShape]
            public partial union U({{caseType}});
            """));
        CSharpUnionShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
        Assert.Equal(nullIndex, model.NullableCaseIndex);
        Assert.Equal([0], model.CaseDispatchOrder);
        Assert.Equal(SpecialType.System_Int32, Assert.Single(model.UnionCases).PatternType.SpecialType);
        string generated = GetCSharpUnionGeneratedSource(result, "U");
        if (nullIndex >= 0)
        {
            Assert.Contains($"null => {nullIndex},", generated);
        }
        else
        {
            Assert.DoesNotContain("{ Value: null }", generated);
        }
    }

    [Theory]
    [InlineData("class", false, "0,throw,throw")]
    [InlineData("class", true, "0,1,1")]
    [InlineData("struct", false, "0,throw,n/a")]
    [InlineData("struct", true, "0,1,n/a")]
    public static void CSharpUnion_NullPayloadRequiresNullableCase(string kind, bool hasNullableCase, string expected)
    {
        string actual = ExecuteCSharpUnionSource($$"""
            using System;
            using PolyType;
            using PolyType.Abstractions;
            [System.Runtime.CompilerServices.Union]
            public {{kind}} U
            {
                public U(int value) => Value = value;
                {{(hasNullableCase ? "public U(string? value) => Value = value;" : "")}}
                public object? Value { get; set; }
            }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    var shape = (IUnionTypeShape<U>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(U))!;
                    U value = new U(42);
                    string validIndex = Classify(shape, value);
                    value.Value = null;
                    string nullPayload = Classify(shape, value);
                    string nullWrapper = {{(kind == "class" ? "Classify(shape, null!)" : "\"n/a\"")}};
                    return $"{validIndex},{nullPayload},{nullWrapper}";
                }

                private static string Classify(IUnionTypeShape<U> shape, U value)
                {
                    try { return shape.GetGetUnionCaseIndex()(ref value).ToString(); }
                    catch (InvalidOperationException) { return "throw"; }
                }
            }
            """);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void CSharpUnion_NativeDefaultThrowsWithoutCallingParameterlessConstructor()
    {
        string actual = ExecuteCSharpUnionSource("""
            using System;
            using PolyType;
            using PolyType.Abstractions;
            public union U(int)
            {
                public U() : this(123) { ConstructorCalls++; }
                public static int ConstructorCalls;
            }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    var shape = (IUnionTypeShape<U>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(U))!;
                    U value = default;
                    bool rejectsDefault = false;
                    try { shape.GetGetUnionCaseIndex()(ref value); }
                    catch (InvalidOperationException) { rejectsDefault = true; }
                    int callsAfterDefault = U.ConstructorCalls;
                    value = new U();
                    int explicitIndex = shape.GetGetUnionCaseIndex()(ref value);
                    return $"{rejectsDefault},{callsAfterDefault},{explicitIndex},{value.Value},{U.ConstructorCalls}";
                }
            }
            """);
        Assert.Equal("True,0,0,123,1", actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CSharpUnion_DefaultStructUsesItsNonNullPayload(bool hasNullableCase)
    {
        string actual = ExecuteCSharpUnionSource($$"""
            using PolyType;
            using PolyType.Abstractions;
            [System.Runtime.CompilerServices.Union]
            public readonly struct U
            {
                private readonly int _number;
                private readonly bool _isText;
                private readonly string? _text;
                public U(int value) { _number = value; _isText = false; _text = null; }
                {{(hasNullableCase ? "public U(string? value) { _number = 0; _isText = true; _text = value; }" : "")}}
                public object? Value => _isText ? _text : _number;
            }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    var shape = (IUnionTypeShape<U>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(U))!;
                    U value = default;
                    int index = shape.GetGetUnionCaseIndex()(ref value);
                    int payload = ((IUnionCaseShape<int, U>)shape.UnionCases[0]).Marshaler.Unmarshal(value);
                    return $"{index},{payload}";
                }
            }
            """);
        Assert.Equal("0,0", actual);
    }

    [Fact]
    public static void CSharpUnion_RecursiveAndWrapperInterfaceCasesInspectPayload()
    {
        string actual = ExecuteCSharpUnionSource("""
            using PolyType;
            using PolyType.Abstractions;
            using System.Runtime.CompilerServices;
            public union Nat(int, Nat, IUnion?);
            public sealed class OtherUnion : IUnion { public object? Value => "other"; }
            [GenerateShapeFor(typeof(Nat))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    var shape = (IUnionTypeShape<Nat>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(Nat))!;
                    var self = ((IUnionCaseShape<Nat, Nat>)shape.UnionCases[1]).Marshaler;
                    var contract = ((IUnionCaseShape<IUnion, Nat>)shape.UnionCases[2]).Marshaler;
                    Nat inner = new Nat(42);
                    Nat outer = self.Marshal(inner);
                    int selfIndex = shape.GetGetUnionCaseIndex()(ref outer);
                    Nat extracted = self.Unmarshal(outer);
                    outer = contract.Marshal(inner);
                    int interfaceCreatorIndex = shape.GetGetUnionCaseIndex()(ref outer);
                    var other = new OtherUnion();
                    outer = contract.Marshal(other);
                    int otherIndex = shape.GetGetUnionCaseIndex()(ref outer);
                    bool sameOther = object.ReferenceEquals(other, contract.Unmarshal(outer));
                    outer = default;
                    int defaultIndex = shape.GetGetUnionCaseIndex()(ref outer);
                    return $"{selfIndex},{extracted.Value},{interfaceCreatorIndex},{otherIndex},{sameOther},{defaultIndex}";
                }
            }
            """);
        Assert.Equal("1,42,1,2,True,2", actual);
    }

    [Theory]
    [InlineData("""
        public interface IZebra { }
        public interface IAntelope { }
        public class Both : IZebra, IAntelope { }
        public union U(IZebra, IAntelope);
        """, "new U((IAntelope)new Both())", 0, 1)]
    [InlineData("""
        public class Root { }
        public class Leaf : Root { }
        public interface IOther { }
        public class Both : Root, IOther { }
        public union U(Root, Leaf, IOther);
        """, "new U((IOther)new Both())", 0, 2)]
    [InlineData("""
        public union U(IEnumerable<object>, IEnumerable<string>);
        """, """new U((IEnumerable<object>)new string[] { "value" })""", 1)]
    public static void CSharpUnion_DispatchOrderingMatchesReflection(string declarations, string valueExpression, params int[] expectedIndices)
    {
        string actual = ExecuteCSharpUnionSource($$"""
            using System.Collections.Generic;
            using PolyType;
            using PolyType.Abstractions;
            using PolyType.ReflectionProvider;
            {{declarations}}
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    U value = {{valueExpression}};
                    var generated = (IUnionTypeShape<U>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(U))!;
                    var reflection = (IUnionTypeShape<U>)ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = false }).GetTypeShape(typeof(U))!;
                    var emit = (IUnionTypeShape<U>)ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = true }).GetTypeShape(typeof(U))!;
                    return $"{generated.GetGetUnionCaseIndex()(ref value)},{reflection.GetGetUnionCaseIndex()(ref value)},{emit.GetGetUnionCaseIndex()(ref value)}";
                }
            }
            """);

        int[] indices = actual.Split(',').Select(int.Parse).ToArray();
        Assert.Equal(3, indices.Length);
        Assert.Contains(indices[0], expectedIndices);
        Assert.All(indices, index => Assert.Equal(indices[0], index));
    }

    [Fact]
    public static void CSharpUnion_InheritedGetterIgnoresUnsuitableHidingProperty()
    {
        string actual = ExecuteCSharpUnionSource("""
            using PolyType;
            using PolyType.Abstractions;
            public class Parent
            {
                public Parent(object? value) => Value = value;
                public object? Value { get; }
            }
            [System.Runtime.CompilerServices.Union]
            public class U : Parent
            {
                public U(int value) : base(value) { }
                public new string Value => "not the union payload";
            }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    var shape = (IUnionTypeShape<U>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(U))!;
                    var marshaler = ((IUnionCaseShape<int, U>)shape.UnionCases[0]).Marshaler;
                    U value = marshaler.Marshal(42)!;
                    return $"{shape.GetGetUnionCaseIndex()(ref value)},{marshaler.Unmarshal(value)}";
                }
            }
            """);
        Assert.Equal("0,42", actual);
    }

#if NET
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CSharpUnion_GenericProviderUsesBoundFactoriesAndValue(bool virtualDefault)
    {
        string actual = ExecuteCSharpUnionSource($$"""
            using PolyType;
            using PolyType.Abstractions;
            public interface IFactory<TUnion, T>
            {
                static abstract TUnion Create(in T value);
                object? Value { get; }
            }
            public class Outer<T>
            {
                [System.Runtime.CompilerServices.Union]
                public sealed class U : U.IUnionMembers
                {
                    private readonly object? _value;
                    public string Creator { get; }
                    private U(object? value, string creator) { _value = value; Creator = creator; }
                    public interface IUnionMembers : IFactory<U, T>
                    {
                        public static {{(virtualDefault ? "virtual " : "")}}U Create(string? value) => new U(value, "default");
                    }
                    static U IFactory<U, T>.Create(in T value) => new U(value, "explicit");
                    object? IFactory<U, T>.Value => _value;
                }
            }
            [GenerateShapeFor(typeof(Outer<int>.U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    var shape = (IUnionTypeShape<Outer<int>.U>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(Outer<int>.U))!;
                    var text = ((IUnionCaseShape<string, Outer<int>.U>)shape.UnionCases[0]).Marshaler;
                    var number = ((IUnionCaseShape<int, Outer<int>.U>)shape.UnionCases[1]).Marshaler;
                    Outer<int>.U value = text.Marshal("text")!;
                    string textCreator = value.Creator;
                    string? payload = text.Unmarshal(value);
                    value = number.Marshal(42)!;
                    string numberCreator = value.Creator;
                    int numberIndex = shape.GetGetUnionCaseIndex()(ref value);
                    int numberValue = number.Unmarshal(value);
                    value = text.Marshal(null)!;
                    int nullPayloadIndex = shape.GetGetUnionCaseIndex()(ref value);
                    value = null!;
                    int nullWrapperIndex = shape.GetGetUnionCaseIndex()(ref value);
                    return $"{textCreator},{payload},{numberCreator},{numberIndex},{numberValue},{nullPayloadIndex},{nullWrapperIndex}";
                }
            }
            """);
        Assert.Equal("default,text,explicit,1,42,0,0", actual);
    }
#endif

    [Fact]
    public static void CSharpUnion_ExplicitHierarchyDispatchesOnWrapper()
    {
        string actual = ExecuteCSharpUnionSource("""
            using PolyType;
            using PolyType.Abstractions;
            [System.Runtime.CompilerServices.Union]
            [DerivedTypeShape(typeof(Derived)), DerivedTypeShape(typeof(U))]
            public class U
            {
                public U(string value) => Value = value;
                public object? Value { get; }
            }
            public class Derived : U { public Derived(string value) : base(value) { } }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            public static class Test
            {
                public static string Run()
                {
                    var shape = (IUnionTypeShape<U>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(U))!;
                    U value = new Derived("payload");
                    int derivedIndex = shape.GetGetUnionCaseIndex()(ref value);
                    value = null!;
                    int nullIndex = shape.GetGetUnionCaseIndex()(ref value);
                    return $"{shape.UnionKind},{derivedIndex},{nullIndex},{shape.UnionCases[0].IsNullable},{shape.UnionCases[1].IsNullable}";
                }
            }
            """);
        Assert.Equal("TypeHierarchy,0,1,False,False", actual);
    }

    [Theory]
    [InlineData("", "DerivedTypeShape(typeof(Derived))", typeof(ObjectShapeModel))]
    [InlineData(" : List<int>", "DerivedTypeShape(typeof(Derived))", typeof(EnumerableShapeModel))]
    [InlineData(" : Dictionary<string, int>", "DerivedTypeShape(typeof(Derived))", typeof(DictionaryShapeModel))]
    [InlineData("", "System.Runtime.Serialization.KnownType(typeof(Derived))", typeof(ObjectShapeModel))]
    [InlineData(" : List<int>", "System.Runtime.Serialization.KnownType(typeof(Derived))", typeof(EnumerableShapeModel))]
    [InlineData(" : Dictionary<string, int>", "System.Runtime.Serialization.KnownType(typeof(Derived))", typeof(DictionaryShapeModel))]
    public static void CSharpUnion_ExplicitHierarchyRetainsUnderlyingKind(string baseTypes, string registration, Type underlyingModelType)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation($$"""
            using System.Collections.Generic;
            using PolyType;
            [System.Runtime.CompilerServices.Union]
            [{{registration}}]
            public class U{{baseTypes}}
            {
                public U() { }
                public U(string value) => Value = value;
                public object? Value { get; }
            }
            public class Derived : U { }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            """));

        UnionShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        Assert.Equal(underlyingModelType, model.UnderlyingModel.GetType());
        Assert.DoesNotContain(result.AllGeneratedTypes, candidate => candidate is CSharpUnionShapeModel);
        Assert.Single(model.UnionCases);
    }

    [Theory]
    [InlineData("DerivedTypeShape(typeof(Derived))")]
    [InlineData("System.Runtime.Serialization.KnownType(typeof(Derived))")]
    public static void CSharpUnion_ExplicitHierarchyDoesNotAddUnionPatternDiagnostics(string registration)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation($$"""
            using PolyType;
            [System.Runtime.CompilerServices.Union]
            [{{registration}}]
            public class U { }
            public class Derived : U { }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            """), disableDiagnosticValidation: true);

        Assert.Single(result.AllGeneratedTypes.OfType<UnionShapeModel>());
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "PT0025" or "CS8785");
        Assert.Empty(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
    }

    [Theory]
    [InlineData("Object")]
    [InlineData("None")]
    public static void CSharpUnion_ExplicitKindOverridesInference(string kind)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation($$"""
            using PolyType;
            [System.Runtime.CompilerServices.Union, GenerateShape, TypeShape(Kind = TypeShapeKind.{{kind}})]
            public partial class U
            {
                public U(int value) => Value = value;
                public object? Value { get; }
            }
            """));
        Assert.DoesNotContain(result.AllGeneratedTypes, model => model is CSharpUnionShapeModel);
    }

    [Fact]
    public static void CSharpUnion_SurrogateOverridesInference()
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation("""
            using PolyType;
            [System.Runtime.CompilerServices.Union, GenerateShape, TypeShape(Marshaler = typeof(Converter))]
            public partial class U
            {
                public U(int value) => Value = value;
                public object? Value { get; }
            }
            public class Converter : IMarshaler<U, int>
            {
                public int Marshal(U? value) => 0;
                public U? Unmarshal(int value) => new U(value);
            }
            """));
        Assert.Single(result.AllGeneratedTypes.OfType<SurrogateShapeModel>());
        Assert.Empty(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
    }

    [Theory]
    [InlineData("""
        using PolyType;
        namespace Left { public class Case { } }
        namespace Right { public class Case { } }
        [GenerateShape]
        public partial union U(Left.Case, Right.Case);
        """, "name")]
    [InlineData("""
        using PolyType;
        [System.Runtime.CompilerServices.Union]
        public class U<T>
        {
            public U(T value) => Value = value;
            public U(int value) => Value = value;
            public object? Value { get; }
        }
        [GenerateShapeFor(typeof(U<int>))]
        public partial class Witness { }
        """, "type")]
    public static void CSharpUnion_DuplicateMetadataIsDiagnosed(string source, string metadataKind)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation(source), disableDiagnosticValidation: true);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PT0026" && diagnostic.GetMessage().Contains($"case {metadataKind}"));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
        Assert.Empty(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
    }

    [Theory]
    [InlineData("int", true)]
    [InlineData("string", false)]
    public static void CSharpUnion_InheritedFactoryCollisionsAfterSubstitutionAreDiagnosed(string argument, bool hasCollision)
    {
        Compilation compilation = CreateCSharpUnionCompilation($$"""
            using PolyType;
            public interface IParent<T>
            {
                public static U<T> Create(int value) => new U<T>();
                object? Value { get; }
            }
            [System.Runtime.CompilerServices.Union]
            public class U<T> : U<T>.IUnionMembers
            {
                public interface IUnionMembers : IParent<T>
                {
                    public static U<T> Create(T value) => new U<T>();
                }
                public object? Value => null;
            }
            [GenerateShapeFor(typeof(U<{{argument}}>))]
            public partial class Witness { }
            """);
        compilation.GetDiagnostics(TestContext.Current.CancellationToken).AssertMaxSeverity(DiagnosticSeverity.Info);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: hasCollision);
        if (hasCollision)
        {
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PT0026" && diagnostic.GetMessage().Contains("case type"));
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
            Assert.Empty(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
        }
        else
        {
            CSharpUnionShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
            Assert.Equal(["String", "Int32"], model.UnionCases.Select(c => c.Name));
        }
    }

    [Theory]
    [InlineData("public U(int value) { }")]
    [InlineData("public object? Value => null;")]
    [InlineData("public U(ref int value) { } public object? Value => null;")]
    [InlineData("public U(int value) { } public static object? Value => null;")]
    [InlineData("public U(int value) { } public object? Value { private get; set; }")]
    public static void CSharpUnion_InvalidMemberPatternIsDiagnosed(string members)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation($$"""
            using PolyType;
            [System.Runtime.CompilerServices.Union, GenerateShape]
            public partial class U { {{members}} }
            """), disableDiagnosticValidation: true);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PT0025");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
        Assert.Empty(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
    }

    [Theory]
    [InlineData("public U(int value) { }", "PT0025")]
    [InlineData("public U(int value) { Value = value; } public U(int? value) { Value = value; } public object? Value { get; }", null)]
    public static void CSharpUnion_NestedMemberPatternValidation(string members, string? expectedDiagnostic)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation($$"""
            using PolyType;
            [System.Runtime.CompilerServices.Union]
            public class U { {{members}} }
            [GenerateShape]
            public partial class Container { public U? Value { get; set; } }
            """), disableDiagnosticValidation: expectedDiagnostic is not null);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS8785");
        if (expectedDiagnostic is not null)
        {
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expectedDiagnostic);
            Assert.Empty(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
        }
        else
        {
            Assert.Single(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
        }
    }

    [Fact]
    public static void CSharpUnion_OrdinaryTypeNamedUnionRemainsAClass()
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CreateCSharpUnionCompilation("""
            #pragma warning disable CS8981
            using PolyType;
            [GenerateShape]
            public partial class @union { public int Value { get; set; } }
            """));
        Assert.Empty(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
        TypeDeclarationModel declaration = Assert.Single(Assert.Single(result.GeneratedModels).AnnotatedTypes);
        Assert.Contains("partial class", declaration.TypeDeclarationHeader);
        Assert.DoesNotContain(result.NewCompilation.SyntaxTrees,
            tree => tree.GetText(TestContext.Current.CancellationToken).ToString().Contains("__ThrowInvalidUnionCase"));
    }

    [Fact]
    public static void FSharpUnion_NullRepresentationDoesNotMakeCasesNullable()
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(CompilationHelpers.CreateCompilation("""
            using Microsoft.FSharp.Core;
            using PolyType;
            [CompilationMapping(SourceConstructFlags.SumType)]
            [CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)]
            public class U
            {
                [CompilationMapping(SourceConstructFlags.UnionCase, 0)]
                public static U NewA() => null!;
                [CompilationMapping(SourceConstructFlags.UnionCase, 1)]
                public static U NewB(int item) => new U();
                [CompilationMapping(SourceConstructFlags.Field, 1, 0)]
                public int Item => 42;
                public static int GetTag(U? value) => value is null ? 0 : 1;
            }
            [GenerateShapeFor(typeof(U))]
            public partial class Witness { }
            """));
        FSharpUnionShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<FSharpUnionShapeModel>());
        Assert.False(model.IsContextual);
        Assert.True(model.UnderlyingModel.IsContextual);
        Assert.All(model.UnionCases, unionCase =>
        {
            Assert.True(unionCase.TypeModel.IsContextual);
            AssertContextualInitializer(result, unionCase.TypeModel);
        });
        Assert.DoesNotContain("IsNullable =", GetCSharpUnionGeneratedSource(result, "U"));
        Assert.Contains("UnionKind = global::PolyType.Abstractions.UnionTypeShapeKind.FSharpUnion", GetCSharpUnionGeneratedSource(result, "U"));
    }

    internal static Compilation CreateCSharpUnionCompilation(string source, string assemblyName = "TestAssembly") =>
        AddCSharpUnionContracts(CompilationHelpers.CreateCompilation(
            source, assemblyName: assemblyName, parseOptions: CompilationHelpers.CreateParseOptions(LanguageVersion.Preview)));

    internal static Compilation AddCSharpUnionContracts(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.UnionAttribute") is null)
        {
            compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                CSharpUnionContracts,
                (CSharpParseOptions)compilation.SyntaxTrees.First().Options));
        }

        return compilation;
    }

    private static string GetCSharpUnionGeneratedSource(PolyTypeSourceGeneratorResult result, string sourceIdentifier)
    {
        string prefix = Assert.Single(result.GeneratedModels).ProviderDeclaration.SourceFilenamePrefix;
        return Assert.Single(result.NewCompilation.SyntaxTrees, tree => tree.FilePath.EndsWith(prefix + "." + sourceIdentifier + ".g.cs", StringComparison.Ordinal))
            .GetText().ToString();
    }

    private static string ExecuteCSharpUnionSource(string source)
    {
        Compilation compilation = CreateCSharpUnionCompilation(source, "UnionExecution_" + Guid.NewGuid().ToString("N"));
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        using var stream = new MemoryStream();
        var emit = result.NewCompilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        Assembly assembly = Assembly.Load(stream.ToArray());
        return (string)assembly.GetType("Test")!.GetMethod("Run")!.Invoke(null, null)!;
    }
}
