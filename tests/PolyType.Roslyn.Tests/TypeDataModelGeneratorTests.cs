using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Collections.Immutable;
using Xunit;

namespace PolyType.Roslyn.Tests;

public static class TypeDataModelGeneratorTests
{
    [Theory]
    [InlineData("int?", TypeDataKind.Optional, TypeDataKind.Optional)]
    [InlineData("System.ValueTuple<int, string>", TypeDataKind.Tuple, TypeDataKind.Tuple)]
    [InlineData("System.Action<int>", TypeDataKind.Delegate, TypeDataKind.None)]
    [InlineData("System.Collections.Generic.Dictionary<int, string>", TypeDataKind.Dictionary, TypeDataKind.Dictionary)]
    [InlineData("System.Collections.Generic.Dictionary<int, string>", TypeDataKind.Enumerable, TypeDataKind.Enumerable)]
    [InlineData("Payload", TypeDataKind.Enum, TypeDataKind.None)]
    [InlineData("Payload", TypeDataKind.Optional, TypeDataKind.None)]
    [InlineData("Payload", TypeDataKind.Tuple, TypeDataKind.None)]
    [InlineData("Payload", TypeDataKind.Delegate, TypeDataKind.None)]
    public static void ExplicitKindPolicy_ModelsSupportedContractsAndFallsBackForIncompatibleKinds(
        string typeName, TypeDataKind requestedKind, TypeDataKind expectedKind)
    {
        CSharpCompilation compilation = CreateCompilation($$"""
            public class Payload { public int Value { get; set; } }
            public class Consumer { public {{typeName}} Value => throw new System.NotSupportedException(); }
            """);
        ITypeSymbol type = ((IPropertySymbol)compilation.GetTypeByMetadataName("Consumer")!.GetMembers("Value").Single()).Type;
        var generator = new KindPolicyGenerator(compilation, requestedKind);
        Assert.Equal(TypeDataModelGenerationStatus.Success, generator.IncludeType(type));
        Assert.Equal(expectedKind, generator.GeneratedModels[type].Kind);
        Assert.True(generator.GeneratedModels[type].IsRootType);
    }

    private sealed class KindPolicyGenerator(CSharpCompilation compilation, TypeDataKind requestedKind)
        : TypeDataModelGenerator(compilation.Assembly, new KnownSymbols(compilation), TestContext.Current.CancellationToken)
    {
        protected override TypeDataModelGenerationStatus MapType(
            ITypeSymbol type, TypeDataKind? kind, BindingFlags? flags,
            ImmutableArray<AssociatedTypeModel> associatedTypes, ref TypeDataModelGenerationContext context,
            TypeShapeRequirements requirements, out TypeDataModel? model) =>
            base.MapType(type, requestedKind, flags, associatedTypes, ref context, requirements, out model);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void DelegateParameterPolicyControlsTheGeneratedTypeGraph(bool includeParameters)
    {
        CSharpCompilation compilation = CreateCompilation("public delegate string Callback(int value);");
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("Callback")!;
        var generator = new DelegateModelGenerator(compilation, includeParameters);

        Assert.Equal(TypeDataModelGenerationStatus.Success, generator.IncludeType(type));
        Assert.True(generator.GeneratedModels[type].IsRootType);

        if (includeParameters)
        {
            var model = Assert.IsType<DelegateDataModel>(generator.GeneratedModels[type]);
            Assert.Equal("Invoke", model.InvokeMethod.Name);
            Assert.Equal(SpecialType.System_String, model.ReturnedValueType!.SpecialType);
            Assert.Equal(SpecialType.System_Int32, Assert.Single(model.Parameters).Parameter.Type.SpecialType);
            Assert.Equal(TypeShapeRequirements.Full, model.Requirements);
        }
        else
        {
            var model = Assert.IsType<TypeDataModel>(generator.GeneratedModels[type]);
            Assert.Equal(TypeShapeRequirements.None, model.Requirements);
            Assert.Single(generator.GeneratedModels);
        }
    }

    [Theory]
    [InlineData("public unsafe delegate int Callback(int* value);")]
    [InlineData("public unsafe delegate int* Callback();")]
    public static void RequestedDelegateSignaturesRejectUnsupportedTypes(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        var generator = new DelegateModelGenerator(compilation, includeParameters: true);

        Assert.Equal(TypeDataModelGenerationStatus.UnsupportedType, generator.IncludeType(compilation.GetTypeByMetadataName("Callback")!));
        Assert.Empty(generator.GeneratedModels);
    }

#if NET
    [Theory]
    [InlineData("System.Span<int>", EnumerableKind.SpanOfT)]
    [InlineData("System.ReadOnlySpan<int>", EnumerableKind.ReadOnlySpanOfT)]
    [InlineData("System.Memory<int>", EnumerableKind.MemoryOfT)]
    [InlineData("System.ReadOnlyMemory<int>", EnumerableKind.ReadOnlyMemoryOfT)]
    public static void ModelsContiguousCollectionKinds(string typeName, EnumerableKind expectedKind)
    {
        CSharpCompilation compilation = CreateCompilation($$"""
            public class Container
            {
                public {{typeName}} Value => default;
            }
            """);
        ITypeSymbol type = ((IPropertySymbol)compilation.GetTypeByMetadataName("Container")!.GetMembers("Value").Single()).Type;
        TypeDataModelGenerator generator = CreateGenerator(compilation);

        Assert.Equal(TypeDataModelGenerationStatus.Success, generator.IncludeType(type));
        var model = Assert.IsType<EnumerableDataModel>(generator.GeneratedModels[type]);
        Assert.Equal(expectedKind, model.EnumerableKind);
        Assert.Equal(SpecialType.System_Int32, model.ElementType.SpecialType);
        Assert.True(model.IsRootType);
        Assert.Null(model.AppendMethod);
        Assert.Null(model.FactoryMethod);
    }
#endif

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void RejectedTraversalDoesNotCommitPartialModels(bool rejectRoot)
    {
        CSharpCompilation compilation = CreateCompilation("public class Container : System.Collections.Generic.List<int> { }");
        var generator = new TraversalPolicyGenerator(compilation, rejectRoot ? "Container" : "Int32");
        ITypeSymbol boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
        Assert.Equal(TypeDataModelGenerationStatus.Success, generator.IncludeType(boolType));
        var modelsBefore = generator.GeneratedModels;

        Assert.Equal(TypeDataModelGenerationStatus.UnsupportedType, generator.IncludeType(compilation.GetTypeByMetadataName("Container")!));
        Assert.Same(modelsBefore, generator.GeneratedModels);
        Assert.Single(generator.GeneratedModels);
        Assert.Equal(TypeDataModelGenerationStatus.Success, generator.IncludeType(boolType));
        Assert.Same(modelsBefore, generator.GeneratedModels);
    }

    private sealed class DelegateModelGenerator(CSharpCompilation compilation, bool includeParameters)
        : TypeDataModelGenerator(compilation.Assembly, new KnownSymbols(compilation), TestContext.Current.CancellationToken)
    {
        protected override bool IncludeDelegateParameters => includeParameters;
    }

    private sealed class TraversalPolicyGenerator(CSharpCompilation compilation, string rejectedType)
        : TypeDataModelGenerator(compilation.Assembly, new KnownSymbols(compilation), TestContext.Current.CancellationToken)
    {
        protected override bool OnTypeTraversalStarting(ITypeSymbol type) => type.Name != rejectedType;
    }

    private const string Contracts = """
        namespace System.Runtime.CompilerServices
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, Inherited = false)]
            public sealed class UnionAttribute : System.Attribute { }
            public interface IUnion { object? Value { get; } }
        }
        """;

    [Theory]
    [InlineData("public union U(int, string?);")]
    [InlineData("""
        [System.Runtime.CompilerServices.Union]
        public struct U
        {
            public U(int value) => Value = value;
            public U(string? value) => Value = value;
            public object? Value { get; }
        }
        """)]
    [InlineData("""
        [System.Runtime.CompilerServices.Union]
        public class U
        {
            public U(in int value) => Value = value;
            public U(string? value) => Value = value;
            public object? Value { get; }
        }
        """)]
    public static void DiscoversNativeAndHandwrittenUnions(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        CSharpUnionDataModel model = GetUnionModel(compilation, compilation.GetTypeByMetadataName("U")!);

        Assert.Equal(TypeDataKind.CSharpUnion, model.Kind);
        Assert.Equal(["int", "string?"], model.UnionCases.Select(c => c.Type.ToDisplayString()));
        Assert.Equal([false, true], model.UnionCases.Select(c => c.IsNullable));
        Assert.All(model.UnionCases, c => Assert.Equal(MethodKind.Constructor, c.CreationMember.MethodKind));
        Assert.Null(model.UnionMemberProvider);
        Assert.Equal("Value", model.ValueProperty.Name);
    }

    [Theory]
    [InlineData("ref", "value = 0;")]
    [InlineData("out", "value = 0;")]
    [InlineData("private", "")]
    [InlineData("multiple", "")]
    public static void ExcludesUnsuitableCreationMembers(string kind, string body)
    {
        string excluded = kind switch
        {
            "private" => "private U(string value) { }",
            "multiple" => "public U(string value, bool ignored) { }",
            _ => $"public U({kind} int value) {{ {body} }}",
        };
        CSharpCompilation compilation = CreateCompilation($$"""
            [System.Runtime.CompilerServices.Union]
            public class U
            {
                public U(bool value) { }
                {{excluded}}
                public object? Value => null;
            }
            """);

        CSharpUnionDataModel model = GetUnionModel(compilation, compilation.GetTypeByMetadataName("U")!);
        Assert.Equal(SpecialType.System_Boolean, Assert.Single(model.UnionCases).Type.SpecialType);
    }

    [Theory]
    [InlineData("public bool TryGetValue(out T value)", true)]
    [InlineData("public bool TryGetValue(ref T value)", false)]
    [InlineData("public bool TryGetValue(T value)", false)]
    [InlineData("public static bool TryGetValue(out T value)", false)]
    [InlineData("private bool TryGetValue(out T value)", false)]
    [InlineData("public bool TryGetValue<TIgnored>(out T value)", false)]
    [InlineData("public int TryGetValue(out T value)", false)]
    [InlineData("public ref bool TryGetValue(out T value)", false)]
    [InlineData("public bool TryGetValue(out T value, bool other)", false)]
    [InlineData("public bool TryGetValue(out long value)", false)]
    public static void DiscoversOnlySuitableInheritedTryGetValueOverloads(string signature, bool expected)
    {
        CSharpCompilation compilation = CreateCompilation($$"""
            public class Parent<T>
            {
                {{signature}} => throw new System.NotSupportedException();
                public object? Value => null;
            }
            [System.Runtime.CompilerServices.Union]
            public class U : Parent<int>
            {
                public U(int value) { }
                public U(string? value) { }
            }
            """);

        CSharpUnionDataModel model = GetUnionModel(compilation, compilation.GetTypeByMetadataName("U")!);
        IMethodSymbol? method = model.UnionCases[0].TryGetValueMethod;
        Assert.Equal(expected, method is not null);
        if (method is not null)
        {
            Assert.Equal("Parent<int>", method.ContainingType.ToDisplayString());
            Assert.Equal(SpecialType.System_Int32, Assert.Single(method.Parameters).Type.SpecialType);
            Assert.Equal(RefKind.Out, method.Parameters[0].RefKind);
        }

        Assert.Null(model.UnionCases[1].TryGetValueMethod);
    }

    [Theory]
    [InlineData("public object? Value => null;", false)]
    [InlineData("public new int Value => 0;", true)]
    [InlineData("private new object? Value => null;", true)]
    public static void ResolvesInheritedGetterUsingCompilerLookup(string property, bool inherited)
    {
        CSharpCompilation compilation = CreateCompilation($$"""
            public class Parent { public object? Value => null; }
            [System.Runtime.CompilerServices.Union]
            public class U {{(inherited ? ": Parent" : "")}}
            {
                public U(int value) { }
                {{property}}
            }
            """);

        CSharpUnionDataModel model = GetUnionModel(compilation, compilation.GetTypeByMetadataName("U")!);
        Assert.Equal(inherited ? "Parent" : "U", model.ValueProperty.ContainingType.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("private object? Value => null;")]
    [InlineData("public static object? Value => null;")]
    [InlineData("public int Value => 0;")]
    [InlineData("public object? Value { private get; set; }")]
    [InlineData("public ref object? Value => throw new System.NotSupportedException();")]
    public static void InvalidValuePatternDoesNotFallBackToObject(string property)
    {
        CSharpCompilation compilation = CreateCompilation($$"""
            [System.Runtime.CompilerServices.Union]
            public class U
            {
                public U(int value) { }
                {{property}}
            }
            """, validateInput: false);
        var generator = CreateGenerator(compilation);
        Assert.Equal(TypeDataModelGenerationStatus.UnsupportedType, generator.IncludeType(compilation.GetTypeByMetadataName("U")!));
        Assert.Empty(generator.GeneratedModels);
    }

    [Theory]
    [InlineData("""
        public class U : System.Runtime.CompilerServices.IUnion
        {
            public U(int value) { }
            public object? Value => null;
        }
        """)]
    [InlineData("""
        #pragma warning disable CS8981
        public class union { }
        public class U : union { }
        """)]
    public static void InterfaceAndIdentifierDoNotOptIntoUnionDiscovery(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        var generator = CreateGenerator(compilation);
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("U")!;
        Assert.Equal(TypeDataModelGenerationStatus.Success, generator.IncludeType(type));
        Assert.IsNotType<CSharpUnionDataModel>(generator.GeneratedModels[type]);
    }

    [Fact]
    public static void MetadataNativeUnionUsesMarkerAndSynthesizedMembers()
    {
        CSharpCompilation original = CreateCompilation("public union U(int, string?);");
        using var stream = new MemoryStream();
        var result = original.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Consumer",
            references: original.References.Append(MetadataReference.CreateFromImage(stream.ToArray())),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("U")!;
        Assert.Empty(type.DeclaringSyntaxReferences);
        CSharpUnionDataModel model = GetUnionModel(compilation, type);
        Assert.Equal(["int", "string?"], model.UnionCases.Select(c => c.Type.ToDisplayString()));
    }

    [Fact]
    public static void PreservesCaseDeclarationIdentityAfterGenericSubstitution()
    {
        CSharpCompilation compilation = CreateCompilation("""
            [System.Runtime.CompilerServices.Union]
            public class U<T>
            {
                public U(T value) => Value = value;
                public U(int value) => Value = value;
                public object? Value { get; }
            }
            """);
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("U`1")!.Construct(compilation.GetSpecialType(SpecialType.System_Int32));
        CSharpUnionDataModel model = GetUnionModel(compilation, type);

        Assert.Equal(2, model.UnionCases.Length);
        Assert.True(SymbolEqualityComparer.Default.Equals(model.UnionCases[0].Type, model.UnionCases[1].Type));
        Assert.False(SymbolEqualityComparer.Default.Equals(model.UnionCases[0].DeclaredType, model.UnionCases[1].DeclaredType));
        Assert.False(SymbolEqualityComparer.Default.Equals(model.UnionCases[0].CreationMember, model.UnionCases[1].CreationMember));
    }

    [Theory]
    [InlineData(SpecialType.System_Int32, false)]
    [InlineData(SpecialType.System_String, false)]
    [InlineData(SpecialType.System_Int32, true)]
    [InlineData(SpecialType.System_String, true)]
    public static void ProviderFactoriesRetainCasesCollapsingAfterSubstitution(SpecialType argument, bool fromMetadata)
    {
        CSharpCompilation compilation = CreateCompilation("""
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
            """);
        if (fromMetadata)
        {
            compilation = CreateMetadataCompilation(compilation);
        }

        INamedTypeSymbol type = compilation.GetTypeByMetadataName("U`1")!.Construct(compilation.GetSpecialType(argument));
        CSharpUnionDataModel model = GetUnionModel(compilation, type);

        Assert.Equal(2, model.UnionCases.Length);
        Assert.Equal(["IUnionMembers", "IParent"], model.UnionCases.Select(c => c.CreationMember.ContainingType.Name));
        Assert.Equal(argument == SpecialType.System_Int32, SymbolEqualityComparer.Default.Equals(model.UnionCases[0].Type, model.UnionCases[1].Type));
        Assert.False(SymbolEqualityComparer.Default.Equals(model.UnionCases[0].DeclaredType, model.UnionCases[1].DeclaredType));
    }

    [Theory]
    [InlineData(SpecialType.System_Int32, false)]
    [InlineData(SpecialType.System_String, false)]
    [InlineData(SpecialType.System_Int32, true)]
    [InlineData(SpecialType.System_String, true)]
    public static void ProviderFactoryHidingUsesDeclarationContext(SpecialType argument, bool fromMetadata)
    {
        CSharpCompilation compilation = CreateCompilation("""
            public interface IParent<T>
            {
                public static U<T> Create(T value) => new U<T>();
                public static U<T> Create(bool value) => new U<T>();
                object? Value { get; }
            }
            [System.Runtime.CompilerServices.Union]
            public class U<T> : U<T>.IUnionMembers
            {
                public interface IUnionMembers : IParent<T>
                {
                    public new static U<T> Create(T value) => new U<T>();
                }
                public object? Value => null;
            }
            """);
        if (fromMetadata)
        {
            compilation = CreateMetadataCompilation(compilation);
        }

        INamedTypeSymbol type = compilation.GetTypeByMetadataName("U`1")!.Construct(compilation.GetSpecialType(argument));
        CSharpUnionDataModel model = GetUnionModel(compilation, type);

        Assert.Equal(2, model.UnionCases.Length);
        Assert.Equal(["IUnionMembers", "IParent"], model.UnionCases.Select(c => c.CreationMember.ContainingType.Name));
        Assert.Equal([argument, SpecialType.System_Boolean], model.UnionCases.Select(c => c.Type.SpecialType));
        Assert.True(SymbolEqualityComparer.Default.Equals(type.OriginalDefinition.TypeParameters[0], model.UnionCases[0].DeclaredType));
    }

#if NET
    [Fact]
    public static void ResolvesGenericProviderWithExplicitAndInheritedMembers()
    {
        CSharpCompilation compilation = CreateCompilation("""
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
                    private U(object? value, bool ignored) => _value = value;
                    public interface IUnionMembers : IFactory<U, T>
                    {
                        public static virtual U Create(string? value) => new U(value, false);
                    }

                    static U IFactory<U, T>.Create(in T value) => new U(value, false);
                    object? IFactory<U, T>.Value => _value;
                }
            }
            """);
        INamedTypeSymbol type = compilation.GetTypeByMetadataName("Outer`1")!
            .Construct(compilation.GetSpecialType(SpecialType.System_Int32))
            .GetTypeMembers("U").Single();
        CSharpUnionDataModel model = GetUnionModel(compilation, type);

        Assert.NotNull(model.UnionMemberProvider);
        Assert.Equal(0, model.UnionMemberProvider.Arity);
        Assert.True(model.UnionMemberProvider.IsGenericType);
        Assert.Equal(["string?", "int"], model.UnionCases.Select(c => c.Type.ToDisplayString()));
        Assert.Equal([RefKind.None, RefKind.In], model.UnionCases.Select(c => c.CreationMember.Parameters[0].RefKind));
        Assert.Equal("IFactory", model.ValueProperty.ContainingType.Name);
    }

    [Fact]
    public static void ProviderFactoriesRespectHidingWithoutLosingOtherOverloads()
    {
        CSharpCompilation compilation = CreateCompilation("""
            public interface IParent
            {
                public static U Create(int value) => new U();
                public static U Create(bool value) => new U();
                object? Value { get; }
            }
            [System.Runtime.CompilerServices.Union]
            public class U : U.IUnionMembers
            {
                public interface IUnionMembers : IParent
                {
                    public new static U Create(int value) => new U();
                    public static U Create(string value) => new U();
                }
                public object? Value => null;
            }
            """);
        CSharpUnionDataModel model = GetUnionModel(compilation, compilation.GetTypeByMetadataName("U")!);
        Assert.Equal(["int", "string", "bool"], model.UnionCases.Select(c => c.Type.ToDisplayString()));
        Assert.Equal(["IUnionMembers", "IUnionMembers", "IParent"], model.UnionCases.Select(c => c.CreationMember.ContainingType.Name));
    }
#endif

    private static CSharpUnionDataModel GetUnionModel(CSharpCompilation compilation, INamedTypeSymbol type)
    {
        TypeDataModelGenerator generator = CreateGenerator(compilation);
        Assert.Equal(TypeDataModelGenerationStatus.Success, generator.IncludeType(type));
        return Assert.IsType<CSharpUnionDataModel>(generator.GeneratedModels[type]);
    }

    private static TypeDataModelGenerator CreateGenerator(CSharpCompilation compilation) =>
        new(compilation.Assembly, new KnownSymbols(compilation), TestContext.Current.CancellationToken);

    private static CSharpCompilation CreateMetadataCompilation(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return CSharpCompilation.Create(
            "Consumer",
            references: compilation.References.Append(MetadataReference.CreateFromImage(stream.ToArray())),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateCompilation(string source, bool validateInput = true)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var references = new List<MetadataReference> { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
#if NET
        references.Add(MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Runtime")).Location));
#endif
        CSharpCompilation compilation = CSharpCompilation.Create(
            "UnionModels",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: true));
        if (compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.UnionAttribute") is null)
        {
            compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(Contracts, parseOptions));
        }

        if (validateInput)
        {
            Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic => diagnostic.Severity is DiagnosticSeverity.Error);
        }

        return compilation;
    }
}
