using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using Xunit;
using static PolyType.FastPathJson.Tests.CompilationHelpers;

namespace PolyType.FastPathJson.Tests;

public static class GeneratorTests
{
    [Theory]
    [InlineData("[GenerateJson] public class Payload { public int Value { get; set; } }", "FJ0001")]
    [InlineData("[GenerateJson] public partial class Payload<T> { public T? Value { get; set; } }", "FJ0001")]
    [InlineData("[GenerateJsonFor(typeof(object))] public partial class Witness;", "FJ0002")]
    [InlineData("[GenerateJsonFor(typeof(System.Collections.Generic.List<>))] public partial class Witness;", "FJ0002")]
    [InlineData("[GenerateJson] public partial class Payload { public System.Action? Callback { get; set; } }", "FJ0002")]
    [InlineData("[GenerateJson] public partial class Payload { [System.Text.Json.Serialization.JsonInclude] private int Value { get; set; } }", "FJ0002")]
    [InlineData("[GenerateJson] public partial class Payload { public int Value { get; init; } = 42; }", "FJ0002")]
    [InlineData("[GenerateJson] public partial class Payload { [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Value { get; set; } }", "FJ0002")]
    [InlineData("[GenerateJson, System.Text.Json.Serialization.JsonDerivedType(typeof(Payload), 1)] public partial class Payload;", "FJ0002")]
    public static void UnsupportedRootsProduceDiagnostics(string source, string expectedDiagnostic)
    {
        var result = Run(source);
        Assert.Contains(result.Diagnostics, d => d.Id == expectedDiagnostic && d.Severity is DiagnosticSeverity.Error);
        Assert.Empty(result.Driver.GetRunResult().Results.Single().GeneratedSources);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CS8785");
    }

    [Theory]
    [InlineData("""
        [GenerateJson]
        public partial record Payload(int Number, string Text);
        """)]
    [InlineData("""
        [GenerateJsonFor(typeof(System.Collections.Generic.List<Node>))]
        public sealed partial class Witness;
        public class Node
        {
            public int Number { get; set; }
            public System.Collections.Generic.List<Node>? Children { get; set; }
        }
        """)]
    [InlineData("""
        public partial class Outer
        {
            [GenerateJson]
            public readonly partial record struct Payload(int Number, string Text);
        }
        """)]
    [InlineData("""
        [GenerateJsonFor<int>]
        [GenerateJsonFor(typeof(System.Collections.Generic.Dictionary<int, string>))]
        public sealed partial class Witness;
        """)]
    [InlineData("""
        [GenerateJsonFor<System.Collections.Generic.List<System.Collections.Generic.List<System.Collections.Generic.List<int>>>>]
        [GenerateJsonFor<System.Collections.Generic.Dictionary<string, (Person, double[])>>]
        public sealed partial class Witness;
        public record Person(string Name, int Age);
        """)]
    [InlineData("""
        [GenerateJson]
        public partial class Payload
        {
            public System.Collections.Generic.List<string?>? NullableElements { get; set; }
            public System.Collections.Generic.List<string>? Elements { get; set; }
            public string?[]? Array { get; set; }
            public System.Collections.Generic.Dictionary<string, string?>? Dictionary { get; set; }
        }
        """)]
    [InlineData("""
        [GenerateJsonFor(typeof(System.Collections.Generic.List<string?>))]
        public sealed partial class Witness;
        public static class Consumer
        {
            public static System.Collections.Generic.List<string?>? Read() =>
                FastPathJsonSerializer.Deserialize<System.Collections.Generic.List<string?>, Witness>("[]");
        }
        """)]
    [InlineData("""
        [GenerateJsonFor(typeof(int[,][]))]
        [GenerateJsonFor(typeof(int[][,]))]
        public sealed partial class Witness;
        """)]
    [InlineData("""
        public record Node(int Value, System.Collections.Generic.List<Node>? Children);
        public partial class Outer
        {
            [GenerateJsonFor<Node>]
            [GenerateJsonFor<System.Collections.Generic.List<Node>>]
            private partial class Witness;
        }
        """)]
    [InlineData("""
        public record Identifier<T>(T Value);
        [GenerateJsonFor<Identifier<string>>]
        [GenerateJsonFor<System.Collections.Generic.List<Identifier<string>>>]
        public partial class Witness;
        """)]
    public static void GeneratedCodeCompilesWithoutPolyTypeRuntime(string source)
    {
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity >= DiagnosticSeverity.Warning));
        using MemoryStream assembly = new();
        var emit = result.Compilation.Emit(assembly, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        string generated = string.Join(Environment.NewLine, result.Driver.GetRunResult().Results.Single().GeneratedSources.Select(s => s.SourceText));
        Assert.DoesNotContain("ITypeShape", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Reflection", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("object")]
    [InlineData("System.Action")]
    public static void UnsupportedRootsDoNotSuppressIndependentRoots(string unsupported)
    {
        var result = Run($$"""
            [GenerateJsonFor(typeof({{unsupported}}))]
            [GenerateJsonFor<int>]
            public sealed partial class Witness;
            """);
        Assert.Single(result.Diagnostics, d => d.Id == "FJ0002");
        Assert.Contains(result.Compilation.GetTypeByMetadataName("Witness")!.AllInterfaces,
            i => i.OriginalDefinition.MetadataName == "IJsonSerializable`1" && i.TypeArguments[0].SpecialType is SpecialType.System_Int32);
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity >= DiagnosticSeverity.Warning));
    }

    [Theory]
    [InlineData("TupleType", 0, 0)]
    [InlineData("SimplePoco", 0, 0)]
    [InlineData("SimpleRecord", 0, 0)]
    [InlineData("StructType", 0, 0)]
    [InlineData("RecursiveObject", 0, 0)]
    [InlineData("DictionaryType", 0, 0)]
    [InlineData("FoldedWitness", 0, 0)]
    [InlineData("TypeHierarchy", 2, 1)]
    public static void ObjectHelpersAreLimitedToHierarchies(string testCaseName, int expectedPropertyHelpers, int expectedReaderHelpers)
    {
        string source = (string)SnapshotTests.GetTestCases().Single(row => (string)row[0] == testCaseName)[1];
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));

        MethodDeclarationSyntax[] methods = result.Driver.GetRunResult().Results.Single().GeneratedSources
            .SelectMany(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>())
            .ToArray();
        MethodDeclarationSyntax[] propertyHelpers = methods
            .Where(m => m.Identifier.ValueText.StartsWith("WriteProperties_", StringComparison.Ordinal)).ToArray();
        Assert.Equal(expectedPropertyHelpers, propertyHelpers.Length);
        Assert.All(propertyHelpers, helper => Assert.Contains(helper.Modifiers, modifier => modifier.IsKind(SyntaxKind.InternalKeyword)));

        MethodDeclarationSyntax[] readerHelpers = methods
            .Where(m => m.Identifier.ValueText.StartsWith("ReadObject_", StringComparison.Ordinal)).ToArray();
        Assert.Equal(expectedReaderHelpers, readerHelpers.Length);
        Assert.All(readerHelpers, helper => Assert.Contains(helper.Modifiers, modifier => modifier.IsKind(SyntaxKind.PrivateKeyword)));
        if (expectedReaderHelpers == 0)
        {
            INamedTypeSymbol? listDefinition = result.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
            INamedTypeSymbol? dictionaryDefinition = result.Compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
            MethodDeclarationSyntax[] readers = methods
                .Where(m => m.Identifier.ValueText == "Read" && m.ExplicitInterfaceSpecifier is not null && m.Body is not null)
                .Where(m =>
                {
                    ITypeSymbol returnType = result.Compilation.GetSemanticModel(m.SyntaxTree)
                        .GetDeclaredSymbol(m, TestContext.Current.CancellationToken)!.ReturnType;
                    return returnType is not IArrayTypeSymbol &&
                        !SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, listDefinition) &&
                        !SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, dictionaryDefinition);
                })
                .ToArray();
            Assert.NotEmpty(readers);
            Assert.All(readers, reader => Assert.Contains(reader.Body!.Statements, statement => statement is WhileStatementSyntax));
        }
    }

    [Theory]
    [InlineData("JsonMetadata")]
    [InlineData("SimplePoco")]
    [InlineData("SimpleRecord")]
    [InlineData("RecursiveObject")]
    public static void ClassMemberAccessDoesNotEmitIdentityCasts(string testCaseName)
    {
        string source = (string)SnapshotTests.GetTestCases().Single(row => (string)row[0] == testCaseName)[1];
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));

        foreach (GeneratedSourceResult generated in result.Driver.GetRunResult().Results.Single().GeneratedSources)
        {
            SemanticModel semanticModel = result.Compilation.GetSemanticModel(generated.SyntaxTree);
            foreach (CastExpressionSyntax cast in generated.SyntaxTree.GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes().OfType<CastExpressionSyntax>())
            {
                ITypeSymbol? targetType = semanticModel.GetTypeInfo(cast.Type, TestContext.Current.CancellationToken).Type;
                ITypeSymbol? sourceType = semanticModel.GetTypeInfo(cast.Expression, TestContext.Current.CancellationToken).Type;
                Assert.False(targetType?.IsReferenceType is true && SymbolEqualityComparer.Default.Equals(targetType, sourceType),
                    $"Redundant identity cast: {cast}");
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void InheritedMembersRetainDeclaringTypeCasts(bool field)
    {
        string source = $$"""
            namespace TestNamespace;
            public class Base<T>
            {
                public T? Inherited {{(field ? ";" : "{ get; set; }")}}
            }

            [GenerateJson]
            public partial class Payload : Base<string>
            {
                public int Own { get; set; }
            }
            """;
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        string generated = string.Join("\n", result.Driver.GetRunResult().Results.Single().GeneratedSources.Select(s => s.SourceText));
        Assert.Contains("((global::TestNamespace.Base<string>)value).Inherited", generated, StringComparison.Ordinal);
        Assert.Contains("((global::TestNamespace.Base<string>)result).Inherited", generated, StringComparison.Ordinal);
        Assert.Contains("value.Own", generated, StringComparison.Ordinal);
        Assert.Contains("result.Own =", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ordered", "property", false)]
    [InlineData("Value", "field", false)]
    [InlineData("Id", "init", false)]
    [InlineData("class", "property", true)]
    [InlineData("event", "field", true)]
    [InlineData("required", "init", true)]
    [InlineData("yield", "property", true)]
    [InlineData("__makeref", "property", true)]
    public static void GeneratedMembersOnlyEscapeKeywords(string name, string kind, bool requiresEscape)
    {
        string member = kind switch
        {
            "field" => $"public int @{name};",
            "init" => $"public required int @{name} {{ get; init; }}",
            _ => $"public int @{name} {{ get; set; }}",
        };
        string source = $$"""
            namespace TestNamespace;

            [GenerateJson]
            public partial class Payload
            {
                {{member}}
            }
            """;
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        SyntaxNode[] roots = result.Driver.GetRunResult().Results.Single().GeneratedSources
            .Select(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken)).ToArray();
        SyntaxToken[] identifiers = roots
            .SelectMany(root => root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Select(access => access.Name.Identifier)
                .Concat(root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                    .Select(assignment => assignment.Left).OfType<IdentifierNameSyntax>().Select(identifier => identifier.Identifier)))
            .Where(identifier => identifier.ValueText == name)
            .ToArray();
        Assert.Equal(2, identifiers.Length);
        Assert.All(identifiers, identifier => Assert.Equal(requiresEscape ? "@" + name : name, identifier.Text));
        Assert.Contains(roots, root => root.ToString().Contains(
            $"WritePropertyName({SymbolDisplay.FormatLiteral(name, quote: true)})", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Create", false)]
    [InlineData("new", true)]
    public static void CollectionFactoriesOnlyEscapeKeywords(string name, bool requiresEscape)
    {
        string source = $$"""
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            namespace TestNamespace;

            [CollectionBuilder(typeof(CollectionBuilder), "{{name}}")]
            public class Collection : IEnumerable<int>
            {
                private readonly int[] _items;
                internal Collection(int[] items) => _items = items;
                public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)_items).GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public static class CollectionBuilder
            {
                public static Collection @{{name}}(ReadOnlySpan<int> values) => new(values.ToArray());
            }

            [GenerateJsonFor<Collection>]
            public partial class Witness;
            """;
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        MemberAccessExpressionSyntax factory = Assert.Single(result.Driver.GetRunResult().Results.Single().GeneratedSources
            .SelectMany(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Select(call => call.Expression).OfType<MemberAccessExpressionSyntax>(),
            access => access.Name.Identifier.ValueText == name);
        Assert.Equal(requiresEscape ? "@" + name : name, factory.Name.Identifier.Text);
    }

    [Theory]
    [InlineData("ScalarWitness")]
    [InlineData("CustomCollection")]
    [InlineData("RecursiveObject")]
    [InlineData("MultiDimensionalArray")]
    [InlineData("FoldedWitness")]
    [InlineData("TypeHierarchy")]
    public static void ValueDispatchUsesRuntimeMethods(string testCaseName)
    {
        string source = (string)SnapshotTests.GetTestCases().Single(row => (string)row[0] == testCaseName)[1];
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));

        INamedTypeSymbol? converters = result.Compilation.GetTypeByMetadataName("PolyType.FastPathJson.Generated.GeneratedJsonConverters");
        if (converters is not null)
        {
            Assert.Empty(converters.GetMembers().OfType<IMethodSymbol>());
        }

        Assert.DoesNotContain(result.Driver.GetRunResult().Results.Single().GeneratedSources,
            generated => generated.HintName == "FastPathJson.Helpers.g.cs");

        int dispatches = 0;
        foreach (GeneratedSourceResult generated in result.Driver.GetRunResult().Results.Single().GeneratedSources)
        {
            SemanticModel semanticModel = result.Compilation.GetSemanticModel(generated.SyntaxTree);
            foreach (InvocationExpressionSyntax call in generated.SyntaxTree.GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is not MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax
                    {
                        Identifier.ValueText: "Write" or "Read",
                        TypeArgumentList.Arguments.Count: 2,
                    },
                })
                {
                    continue;
                }

                IMethodSymbol method = Assert.IsAssignableFrom<IMethodSymbol>(
                    semanticModel.GetSymbolInfo(call, TestContext.Current.CancellationToken).Symbol);
                Assert.Equal("PolyType.FastPathJson.SerializationHelpers", method.ContainingType.ToDisplayString());
                dispatches++;
            }
        }

        Assert.True(dispatches > 0);
    }

    [Theory]
    [InlineData("MultiDimensionalArray", "ThrowNonRectangularArray", "Read", 2)]
    [InlineData("TypeHierarchy", "ThrowUnrecognizedTypeDiscriminator", "Read", 1)]
    [InlineData("TypeHierarchy", "ThrowMissingTypeDiscriminator", "ReadObject_", 1)]
    [InlineData("RequiredProperties64", "ThrowMissingRequiredParameters", "Read", 1)]
    [InlineData("RequiredProperties65", "ThrowMissingRequiredParameters", "Read", 1)]
    [InlineData("MixedRequiredMembers", "ThrowMissingRequiredParameters", "Read", 1)]
    public static void SingleCallerThrowsAreStaticLocalFunctions(string testCaseName, string helperName, string ownerName, int expectedCount)
    {
        string source = (string)SnapshotTests.GetTestCases().Single(row => (string)row[0] == testCaseName)[1];
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));

        LocalFunctionStatementSyntax[] helpers = result.Driver.GetRunResult().Results.Single().GeneratedSources
            .SelectMany(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            .Where(local => local.Identifier.ValueText == helperName)
            .ToArray();
        Assert.Equal(expectedCount, helpers.Length);
        Assert.All(helpers, helper =>
        {
            Assert.Contains(helper.Modifiers, modifier => modifier.IsKind(SyntaxKind.StaticKeyword));
            Assert.Contains("MethodImplOptions.NoInlining", helper.AttributeLists.ToString(), StringComparison.Ordinal);
            Assert.Contains("DoesNotReturn", helper.AttributeLists.ToString(), StringComparison.Ordinal);
            MethodDeclarationSyntax owner = Assert.Single(helper.Ancestors().OfType<MethodDeclarationSyntax>());
            Assert.StartsWith(ownerName, owner.Identifier.ValueText, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("TupleType")]
    [InlineData("DictionaryType")]
    [InlineData("TypeHierarchy")]
    [InlineData("RecursiveObject")]
    [InlineData("CustomCollection")]
    public static void PropertyLoopsAvoidRedundantTokenChecks(string testCaseName)
    {
        string source = (string)SnapshotTests.GetTestCases().Single(row => (string)row[0] == testCaseName)[1];
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        InvocationExpressionSyntax[] calls = result.Driver.GetRunResult().Results.Single().GeneratedSources
            .SelectMany(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>())
            .ToArray();
        Assert.DoesNotContain(calls, call => call.Expression.ToString() == "global::System.Diagnostics.Debug.Assert");
        InvocationExpressionSyntax[] propertyChecks = calls.Where(call =>
            call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "EnsureToken" } &&
            call.ArgumentList.Arguments.Last().Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "PropertyName" }).ToArray();
        Assert.All(propertyChecks, call =>
        {
            IfStatementSyntax? guard = call.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
            Assert.NotNull(guard);
            string reader = call.ArgumentList.Arguments[0].Expression.ToString();
            Assert.Equal($"{reader}.TokenType is global::System.Text.Json.JsonTokenType.Comment", guard.Condition.ToString());
        });
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("display-name")]
    [InlineData("name with \"quotes\" and \nnewline")]
    public static void NullPropertyGuardsPassOnlyJsonMemberNames(string propertyName)
    {
        string source = $$"""
            using System.Text.Json.Serialization;
            namespace TestNamespace;

            [GenerateJson]
            public partial class Payload
            {
                [JsonPropertyName({{SymbolDisplay.FormatLiteral(propertyName, quote: true)}})]
                public string Value { get; set; } = "";
            }
            """;
        var result = Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        InvocationExpressionSyntax[] guards = result.Driver.GetRunResult().Results.Single().GeneratedSources
            .SelectMany(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Where(call => call.Expression is MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "ThrowNullPropertyOnRead" or "ThrowNullPropertyOnWrite",
            })
            .ToArray();
        Assert.Equal(2, guards.Length);
        Assert.Equal(["ThrowNullPropertyOnRead", "ThrowNullPropertyOnWrite"],
            guards.Select(call => ((MemberAccessExpressionSyntax)call.Expression).Name.Identifier.ValueText).Order(StringComparer.Ordinal));
        Assert.All(guards, guard =>
        {
            ArgumentSyntax argument = Assert.Single(guard.ArgumentList.Arguments);
            LiteralExpressionSyntax name = Assert.IsType<LiteralExpressionSyntax>(argument.Expression);
            Assert.Equal(propertyName, name.Token.ValueText);
        });
    }

    [Theory]
    [InlineData("// unrelated comment")]
    [InlineData("public class Unrelated { public int Number => 42; }")]
    public static void UnrelatedChangesPreserveIncrementalModel(string addition)
    {
        var first = Run("[GenerateJson] public partial record Payload(int Number);");
        SyntaxTree unrelated = CSharpSyntaxTree.ParseText(addition, CompilationHelpers.ParseOptions, cancellationToken: TestContext.Current.CancellationToken);
        GeneratorDriver second = first.Driver.RunGeneratorsAndUpdateCompilation(
            first.Input.AddSyntaxTrees(unrelated), out _, out ImmutableArray<Diagnostic> diagnostics,
            TestContext.Current.CancellationToken);
        Assert.Empty(diagnostics);
        var outputs = second.GetRunResult().Results.Single().TrackedSteps["FastPathJsonModel"]
            .SelectMany(step => step.Outputs);
        Assert.All(outputs, output => Assert.True(output.Reason is IncrementalStepRunReason.Unchanged or IncrementalStepRunReason.Cached));
    }
}
