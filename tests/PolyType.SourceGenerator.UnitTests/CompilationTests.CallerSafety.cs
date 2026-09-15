using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.ReflectionProvider;
using System.Reflection;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    public static bool SupportsMemorySafetyRulesVersion =>
        typeof(IModuleSymbol).GetProperty("MemorySafetyRulesVersion") is not null &&
        typeof(CSharpCompilationOptions).GetMethod("WithMemorySafetyRulesVersion") is not null;

    [Theory(Skip = "The compiler host does not expose the memory safety version APIs.", SkipUnless = nameof(SupportsMemorySafetyRulesVersion))]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public static void MemorySafetyRulesVersion_ControlsGeneration(int version, bool hasUnsafeMember)
    {
        string source = CreateSafetyFixture(hasUnsafeMember
            ? "[MethodShape] private unsafe T Read(T value) => value;"
            : "[ConstructorShape] private Model(T value) { }") + """
            [PolyType.GenerateShapeFor(typeof(Model<int>))]
            public partial class Witness { }
            """;
        var compilation = (CSharpCompilation)CompilationHelpers.CreateCompilation(source,
            parseOptions: CreateSafetyParseOptions(enabled: false));
        compilation = WithMemorySafetyRulesVersion(compilation, version);
        Assert.All(compilation.SyntaxTrees, tree => Assert.False(tree.Options.Features.ContainsKey("updated-memory-safety-rules")));

        bool rejectType = version == 2 && hasUnsafeMember;
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: rejectType);
        Assert.Equal(version == 2, Assert.Single(result.GeneratedModels).UsesUpdatedMemorySafetyRules);
        if (rejectType)
        {
            AssertUnsafeTypeRejected(result, "Model_Int32");
        }
        else
        {
            Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Model_Int32");
            foreach (MethodDeclarationSyntax accessor in result.NewCompilation.SyntaxTrees
                .SelectMany(tree => tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(method => method.Modifiers.Any(SyntaxKind.ExternKeyword)))
            {
                Assert.Equal(version == 2, accessor.Modifiers.Any(modifier => modifier.ValueText is "safe"));
            }
        }
    }

    [Theory(Skip = "The compiler host does not expose the memory safety version APIs.", SkipUnless = nameof(SupportsMemorySafetyRulesVersion))]
    [InlineData(1, 1, false)]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, true)]
    public static void MemorySafetyRulesVersion_RespectsDefiningCompilation(int dependencyVersion, int consumerVersion, bool fromMetadata)
    {
        CSharpCompilation dependency = WithMemorySafetyRulesVersion(
            (CSharpCompilation)CompilationHelpers.CreateCompilation(
                CreateSafetyFixture("[MethodShape] public unsafe int Read(int value) => value;"),
                assemblyName: "MemorySafetyRulesDependency",
                parseOptions: CreateSafetyParseOptions(enabled: false)),
            dependencyVersion);
        using var stream = new MemoryStream();
        var emit = dependency.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        MetadataReference reference = fromMetadata
            ? MetadataReference.CreateFromImage(stream.ToArray())
            : dependency.ToMetadataReference();

        CSharpCompilation consumer = WithMemorySafetyRulesVersion(
            (CSharpCompilation)CompilationHelpers.CreateCompilation("""
                [PolyType.GenerateShapeFor(typeof(Model<int>))]
                public partial class Witness { }
                """, additionalReferences: [reference], parseOptions: CreateSafetyParseOptions(enabled: false)),
            consumerVersion);
        bool rejectType = dependencyVersion == 2 && consumerVersion == 2;
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(consumer, disableDiagnosticValidation: rejectType);
        Assert.Equal(consumerVersion == 2, Assert.Single(result.GeneratedModels).UsesUpdatedMemorySafetyRules);
        if (rejectType)
        {
            AssertUnsafeTypeRejected(result, "Model_Int32");
        }
        else
        {
            Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Model_Int32");
        }
    }

    private static CSharpCompilation WithMemorySafetyRulesVersion(CSharpCompilation compilation, int version)
    {
        MethodInfo method = Assert.IsAssignableFrom<MethodInfo>(typeof(CSharpCompilationOptions).GetMethod("WithMemorySafetyRulesVersion"));
        object versionValue = Enum.ToObject(method.GetParameters()[0].ParameterType, version);
        var options = Assert.IsType<CSharpCompilationOptions>(method.Invoke(compilation.Options, [versionValue]));
        return compilation.WithOptions(options);
    }

    public static IEnumerable<object[]> CallerUnsafeMembers()
    {
        yield return ["[ConstructorShape] public unsafe Model(int value) { }"];
        yield return ["[ConstructorShape] private unsafe Model(int value) { }"];
        yield return ["[MethodShape] public unsafe int Read(int address) => address;"];
        yield return ["[MethodShape] private unsafe T Read(T value) => value;"];
        yield return ["[MethodShape] private static unsafe T Read(T value) => value;"];
        yield return ["[System.Runtime.CompilerServices.CompilerGenerated] public unsafe T Read(T value) => value;"];
        yield return ["[MethodShape] private unsafe ref T Read(ref T value) => ref value;"];
        yield return ["[MethodShape(Ignore = true)] public unsafe T Ignored(T value) => value;"];
        yield return ["private unsafe int Unused(int value) => value;"];
        yield return ["[PropertyShape] public unsafe int Value { get => 42; set { } }"];
        yield return ["[PropertyShape] private unsafe T Value { get; init; }"];
        yield return ["[PropertyShape(Ignore = true)] public unsafe int Value { get; set; }"];
        yield return ["public int Value { unsafe get => 42; set { } }"];
        yield return ["public int Value { get => 42; unsafe set { } }"];
        yield return ["public int Value { get => 42; unsafe init { } }"];
        yield return ["public int Value { get => 42; private unsafe set { } }"];
        yield return ["public int Value { get => 42; private unsafe init { } }"];
        yield return ["public unsafe int this[int index] => index;"];
        yield return ["[PropertyShape] public unsafe int Value;"];
        yield return ["[PropertyShape(Ignore = true)] public unsafe int Value;"];
        yield return ["[EventShape] public unsafe event Action<T> Changed { add { } remove { } }"];
        yield return ["[EventShape] private static unsafe event Action<T> Changed { add { } remove { } }"];
        yield return ["[EventShape(Ignore = true)] public unsafe event Action<T> Changed { add { } remove { } }"];
    }

    public static IEnumerable<object[]> CallerUnsafeMetadataCases()
    {
        foreach (object[] member in CallerUnsafeMembers())
        {
            yield return [member[0], false];
            yield return [member[0], true];
        }
    }

    [Theory]
    [MemberData(nameof(CallerUnsafeMembers))]
    public static void CallerUnsafeSourceMembers_RejectContainingType(string member)
    {
        string source = CreateSafetyFixture(member) + """
            [PolyType.GenerateShapeFor(typeof(Model<int>))]
            [PolyType.GenerateShapeFor(typeof(Unrelated))]
            public partial class Witness { }
            public class Unrelated { public int Value { get; set; } }
            """;

        Compilation compilation = CompilationHelpers.CreateCompilation(source, parseOptions: CreateSafetyParseOptions(enabled: true),
            nullableContextOptions: NullableContextOptions.Disable);
        compilation.GetDiagnostics(TestContext.Current.CancellationToken).AssertMaxSeverity(DiagnosticSeverity.Info);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        AssertUnsafeTypeRejected(result, "Model_Int32");
        Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Unrelated");
    }

    [Theory]
    [MemberData(nameof(CallerUnsafeMembers))]
    public static void LegacyUnsafeSourceMembers_DoNotBecomeCallerUnsafe(string member)
    {
        string source = CreateSafetyFixture(member) + """
            [PolyType.GenerateShapeFor(typeof(Model<int>))]
            public partial class Witness { }
            """;

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CompilationHelpers.CreateCompilation(source, parseOptions: CreateSafetyParseOptions(enabled: false),
                nullableContextOptions: NullableContextOptions.Disable));
        Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Model_Int32");
    }

    [Theory]
    [MemberData(nameof(CallerUnsafeMetadataCases))]
    public static void CallerUnsafeMetadataMembers_RequireConsumerOptIn(string member, bool consumerUsesUpdatedRules)
    {
        Compilation dependency = CompilationHelpers.CreateCompilation(CreateSafetyFixture(member),
            parseOptions: CreateSafetyParseOptions(enabled: true), nullableContextOptions: NullableContextOptions.Disable);
        using var stream = new MemoryStream();
        var emit = dependency.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        Compilation consumer = CompilationHelpers.CreateCompilation("""
            [PolyType.GenerateShapeFor(typeof(Model<int>))]
            public partial class Witness { }
            """, additionalReferences: [MetadataReference.CreateFromImage(stream.ToArray())],
            parseOptions: consumerUsesUpdatedRules ? CreateSafetyParseOptions(enabled: true) : null);
        Assert.Equal(MetadataImportOptions.Public, consumer.Options.MetadataImportOptions);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(consumer, disableDiagnosticValidation: consumerUsesUpdatedRules);
        if (consumerUsesUpdatedRules)
        {
            AssertUnsafeTypeRejected(result, "Model_Int32");
        }
        else
        {
            Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Model_Int32");
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public static void ReferencedMembers_RespectMemorySafetyModes(bool dependencyUsesUpdatedRules, bool consumerUsesUpdatedRules)
    {
        Compilation dependency = CompilationHelpers.CreateCompilation(CreateSafetyFixture("public unsafe int Value { get; set; }"),
            assemblyName: $"CallerSafetyDependency_{dependencyUsesUpdatedRules}_{consumerUsesUpdatedRules}",
            parseOptions: CreateSafetyParseOptions(dependencyUsesUpdatedRules), nullableContextOptions: NullableContextOptions.Disable);
        using var stream = new MemoryStream();
        var emit = dependency.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        byte[] image = stream.ToArray();
        Compilation consumer = CompilationHelpers.CreateCompilation("""
            [PolyType.GenerateShapeFor(typeof(Model<int>))]
            public partial class Witness { }
            """, additionalReferences: [MetadataReference.CreateFromImage(image)],
            parseOptions: CreateSafetyParseOptions(consumerUsesUpdatedRules));
        bool rejectType = dependencyUsesUpdatedRules && consumerUsesUpdatedRules;
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(consumer, disableDiagnosticValidation: rejectType);
        if (rejectType)
        {
            AssertUnsafeTypeRejected(result, "Model_Int32");
        }
        else
        {
            Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Model_Int32");
        }

        Type type = System.Reflection.Assembly.Load(image).GetType("Model`1")!.MakeGenericType(typeof(int));
        foreach (bool useReflectionEmit in new[] { false, true })
        {
            ReflectionTypeShapeProvider provider = ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = useReflectionEmit });
            if (dependencyUsesUpdatedRules)
            {
                Assert.Throws<NotSupportedException>(() => provider.GetTypeShape(type));
            }
            else
            {
                Assert.NotNull(provider.GetTypeShape(type));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnsafeMembersInBaseType_RejectDerivedType(bool fromMetadata)
    {
        const string declaration = """
            public class Base<T>
            {
                private unsafe T Unused(T value) => value;
            }

            public class Model : Base<int> { }
            """;
        const string witness = """
            [PolyType.GenerateShapeFor(typeof(Model))]
            public partial class Witness { }
            """;

        Compilation compilation;
        if (fromMetadata)
        {
            Compilation dependency = CompilationHelpers.CreateCompilation(declaration, parseOptions: CreateSafetyParseOptions(enabled: true));
            using var stream = new MemoryStream();
            var emit = dependency.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
            compilation = CompilationHelpers.CreateCompilation(witness, additionalReferences: [MetadataReference.CreateFromImage(stream.ToArray())],
                parseOptions: CreateSafetyParseOptions(enabled: true));
        }
        else
        {
            compilation = CompilationHelpers.CreateCompilation(declaration + witness, parseOptions: CreateSafetyParseOptions(enabled: true));
        }

        AssertUnsafeTypeRejected(CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true), "Model");
    }

    [Theory]
    [InlineData("public unsafe Marshaler() { }")]
    [InlineData("private unsafe int Unused(int value) => value;")]
    public static void CallerUnsafeMarshalerMembers_RejectSurrogateType(string member)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;

            [GenerateShape, TypeShape(Marshaler = typeof(Marshaler))]
            public partial class Model { }

            public class Marshaler : IMarshaler<Model, int>
            {
                {{member}}
                public int Marshal(Model value) => 0;
                public Model Unmarshal(int value) => new Model();
            }
            """, parseOptions: CreateSafetyParseOptions(enabled: true), nullableContextOptions: NullableContextOptions.Disable);
        AssertUnsafeTypeRejected(CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true), "Model");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ReferencedUnsafeMarshaler_RequiresConsumerOptIn(bool consumerUsesUpdatedRules)
    {
        Compilation dependency = CompilationHelpers.CreateCompilation("""
            using PolyType;

            [TypeShape(Marshaler = typeof(Marshaler))]
            public class Model { }

            public class Marshaler : IMarshaler<Model, int>
            {
                public unsafe Marshaler() { }
                public int Marshal(Model value) => 0;
                public Model Unmarshal(int value) => new Model();
            }
            """, parseOptions: CreateSafetyParseOptions(enabled: true), nullableContextOptions: NullableContextOptions.Disable);
        using var stream = new MemoryStream();
        var emit = dependency.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        Compilation consumer = CompilationHelpers.CreateCompilation("""
            [PolyType.GenerateShapeFor(typeof(Model))]
            public partial class Witness { }
            """, additionalReferences: [MetadataReference.CreateFromImage(stream.ToArray())],
            parseOptions: consumerUsesUpdatedRules ? CreateSafetyParseOptions(enabled: true) : null);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(consumer, disableDiagnosticValidation: consumerUsesUpdatedRules);
        if (consumerUsesUpdatedRules)
        {
            AssertUnsafeTypeRejected(result, "Model");
        }
        else
        {
            Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Model");
        }
    }

    [Theory]
    [InlineData("List<int>", "public unsafe Model() { }")]
    [InlineData("List<int>", "public new unsafe void Add(int item) { }")]
    [InlineData("Dictionary<int, int>", "public new unsafe void Add(int key, int value) { }")]
    [InlineData("Dictionary<int, int>", "public new int this[int key] { get => base[key]; unsafe set => base[key] = value; }")]
    public static void CallerUnsafeCollectionOperations_RejectContainingType(string baseType, string member)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using System.Collections.Generic;
            using PolyType;

            [GenerateShape]
            public partial class Model : {{baseType}}
            {
                {{member}}
            }
            """, parseOptions: CreateSafetyParseOptions(enabled: true));
        AssertUnsafeTypeRejected(CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true), "Model");
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData("List<int>", "int", true)]
    [InlineData("List<int>", "int", false)]
    [InlineData("Dictionary<int, int>", "KeyValuePair<int, int>", true)]
    [InlineData("Dictionary<int, int>", "KeyValuePair<int, int>", false)]
    public static void CallerUnsafeBuilderMembers_RejectCollectionType(string baseType, string elementType, bool unsafeFactory)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using System;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using PolyType;

            [GenerateShape, CollectionBuilder(typeof(Builder), nameof(Builder.Create))]
            public partial class Model : {{baseType}}
            {
                private Model() { }
                internal static Model New() => new Model();
            }

            public static class Builder
            {
                public static {{(unsafeFactory ? "unsafe " : "")}}Model Create(ReadOnlySpan<{{elementType}}> values) => Model.New();
                {{(unsafeFactory ? "" : "private static unsafe int Unused(int value) => value;")}}
            }
            """, parseOptions: CreateSafetyParseOptions(enabled: true));
        AssertUnsafeTypeRejected(CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true), "Model");
    }

    [Theory]
    [InlineData("List<int>", "int", false)]
    [InlineData("List<int>", "int", true)]
    [InlineData("Dictionary<int, int>", "KeyValuePair<int, int>", false)]
    [InlineData("Dictionary<int, int>", "KeyValuePair<int, int>", true)]
    public static void ReferencedUnsafeBuilder_RequiresConsumerOptIn(string baseType, string elementType, bool consumerUsesUpdatedRules)
    {
        Compilation dependency = CompilationHelpers.CreateCompilation($$"""
            using System;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;

            [CollectionBuilder(typeof(Builder), nameof(Builder.Create))]
            public class Model : {{baseType}}
            {
                private Model() { }
                internal static Model New() => new Model();
            }

            public static class Builder
            {
                public static unsafe Model Create(ReadOnlySpan<{{elementType}}> values) => Model.New();
            }
            """, parseOptions: CreateSafetyParseOptions(enabled: true));
        using var stream = new MemoryStream();
        var emit = dependency.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

        Compilation consumer = CompilationHelpers.CreateCompilation("""
            [PolyType.GenerateShapeFor(typeof(Model))]
            public partial class Witness { }
            """, additionalReferences: [MetadataReference.CreateFromImage(stream.ToArray())],
            parseOptions: consumerUsesUpdatedRules ? CreateSafetyParseOptions(enabled: true) : null);
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(consumer, disableDiagnosticValidation: consumerUsesUpdatedRules);
        if (consumerUsesUpdatedRules)
        {
            AssertUnsafeTypeRejected(result, "Model");
        }
        else
        {
            Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Model");
        }
    }

    [Theory]
    [InlineData("List<Element>", "Element")]
    [InlineData("Dictionary<string, Element>", "KeyValuePair<string, Element>")]
    public static void UnsafeBuilder_IsRejectedBeforeTraversingCollectionElements(string baseType, string elementType)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using System;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using PolyType;

            [GenerateShape, CollectionBuilder(typeof(Builder), nameof(Builder.Create))]
            public partial class Model : {{baseType}}
            {
                private Model() { }
                internal static Model New() => new Model();
            }

            public class Element
            {
                public unsafe int Read(int value) => value;
            }

            public static class Builder
            {
                public static unsafe Model Create(ReadOnlySpan<{{elementType}}> values) => Model.New();
            }
            """, parseOptions: CreateSafetyParseOptions(enabled: true));
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation, disableDiagnosticValidation: true);
        AssertUnsafeTypeRejected(result, "Model");
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id is "PT0024");
        Assert.Contains("'Builder'", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("List<int>", "int")]
    [InlineData("Dictionary<int, int>", "KeyValuePair<int, int>")]
    public static void UnusedUnsafeBuilder_DoesNotRejectCollectionType(string baseType, string elementType)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using System;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using PolyType;

            [GenerateShape, CollectionBuilder(typeof(Builder), nameof(Builder.Create))]
            public partial class Model : {{baseType}}
            {
                public Model() { }
            }

            public static class Builder
            {
                public static unsafe Model Create(ReadOnlySpan<{{elementType}}> values) => new Model();
            }
            """, parseOptions: CreateSafetyParseOptions(enabled: true));
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Model");
    }
#endif

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void SafeMembersWithUnsafeBlocks_AndUnvisitedTypes_AreSupported(bool fromMetadata)
    {
        const string declaration = """
            using PolyType;

            public class Unvisited
            {
                private unsafe int Unused(int value) => value;
            }

            public class Model
            {
                public Model() { }
                public Model(Unvisited value) { }

                [MethodShape(Ignore = true)]
                public void Ignored(Unvisited value) { }

                [MethodShape]
                public int ReadValue(int value)
                {
                    unsafe int ReadCore(int* address)
                    {
                        unsafe { return *address; }
                    }

                    unsafe { return ReadCore(&value); }
                }
            }
            """;
        const string witness = """
            [PolyType.GenerateShapeFor(typeof(Model))]
            public partial class Witness { }
            """;

        Compilation compilation;
        if (fromMetadata)
        {
            Compilation dependency = CompilationHelpers.CreateCompilation(declaration,
                assemblyName: "SafeWrapperDependency", parseOptions: CreateSafetyParseOptions(enabled: true));
            using var stream = new MemoryStream();
            var emit = dependency.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
            byte[] image = stream.ToArray();
            compilation = CompilationHelpers.CreateCompilation(witness, additionalReferences: [MetadataReference.CreateFromImage(image)],
                parseOptions: CreateSafetyParseOptions(enabled: true));
            Type type = System.Reflection.Assembly.Load(image).GetType("Model")!;
            Assert.NotNull(ReflectionTypeShapeProvider.Default.GetTypeShape(type));
        }
        else
        {
            compilation = CompilationHelpers.CreateCompilation(declaration + witness, parseOptions: CreateSafetyParseOptions(enabled: true));
        }

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Contains(result.AllGeneratedTypes, model => model.SourceIdentifier is "Model");
        Assert.DoesNotContain(result.AllGeneratedTypes, model => model.SourceIdentifier is "Unvisited");
    }

    private static CSharpParseOptions CreateSafetyParseOptions(bool enabled)
    {
        CSharpParseOptions options = CompilationHelpers.CreateParseOptions(LanguageVersion.Preview);
        return enabled ? options.WithFeatures([new("updated-memory-safety-rules", "true")]) : options;
    }

    private static string CreateSafetyFixture(string member) => $$"""
        using System;
        using PolyType;

        public class Model<T>
        {
            {{member}}
        }

        """;

    private static void AssertUnsafeTypeRejected(PolyTypeSourceGeneratorResult result, string sourceIdentifier)
    {
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id is "PT0024");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.All(result.Diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error),
            diagnostic => Assert.Equal("PT0024", diagnostic.Id));
        Assert.DoesNotContain(result.AllGeneratedTypes, model => model.SourceIdentifier == sourceIdentifier);
        result.NewCompilation.GetDiagnostics(TestContext.Current.CancellationToken).AssertMaxSeverity(DiagnosticSeverity.Info);
    }
}
