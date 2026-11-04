using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.Loader;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class CollectionEnumerationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ConcretePatternsPreserveStructEnumerators(bool dictionary)
    {
        string elementType = dictionary ? "KeyValuePair<int, int>" : "int";
        string baseType = dictionary ? "Dictionary<int, int>" : "List<int>";
        string declaration = $$"""
            public class Collection : {{baseType}}, IEnumerable<{{elementType}}>
            {
                IEnumerator<{{elementType}}> IEnumerable<{{elementType}}>.GetEnumerator() =>
                    throw new InvalidOperationException("The interface enumerator must not be used.");
            }
            """;
        VerifyEnumeration(declaration, dictionary, requiresInterface: false);
    }

    [Theory]
    [InlineData(false, "explicit")]
    [InlineData(true, "explicit")]
    [InlineData(false, "mismatched")]
    [InlineData(true, "mismatched")]
    [InlineData(true, "ambiguous")]
    public static void InterfaceFallbackPreservesModeledElementTypes(bool dictionary, string mode)
    {
        string pattern = mode == "mismatched"
            ? """public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("The incompatible pattern must not be used.");"""
            : "";
        string declaration = dictionary
            ? $$"""
                public class Collection : IReadOnlyDictionary<int, int>{{(mode == "ambiguous" ? ", IEnumerable<string>" : "")}}
                {
                    private readonly Dictionary<int, int> _items = new();
                    public int this[int key] { get => _items[key]; set => _items[key] = value; }
                    public IEnumerable<int> Keys => _items.Keys;
                    public IEnumerable<int> Values => _items.Values;
                    public int Count => _items.Count;
                    public bool ContainsKey(int key) => _items.ContainsKey(key);
                    public bool TryGetValue(int key, out int value) => _items.TryGetValue(key, out value);
                    IEnumerator<KeyValuePair<int, int>> IEnumerable<KeyValuePair<int, int>>.GetEnumerator() => _items.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                    {{pattern}}
                    {{(mode == "ambiguous" ? """IEnumerator<string> IEnumerable<string>.GetEnumerator() => throw new InvalidOperationException("The unrelated interface must not be used.");""" : "")}}
                }
                """
            : $$"""
                public class Collection : IEnumerable<int>
                {
                    private readonly List<int> _items = new();
                    public void Add(int value) => _items.Add(value);
                    IEnumerator<int> IEnumerable<int>.GetEnumerator() => _items.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                    {{pattern}}
                }
                """;
        VerifyEnumeration(declaration, dictionary, requiresInterface: true);
    }

    private static void VerifyEnumeration(string declaration, bool dictionary, bool requiresInterface)
    {
        string initializer = dictionary ? "{ [1] = 4, [2] = 5 }" : "{ 1, 2, 3 }";
        string expected = dictionary ? """{"1":4,"2":5}""" : "[1,2,3]";
        string source = $$"""
            using System;
            using System.Collections;
            using System.Collections.Generic;
            namespace TestNamespace;

            {{declaration}}

            [GenerateJsonFor<Collection>]
            public partial class Witness;

            public static class Consumer
            {
                public static string Roundtrip()
                {
                    Collection value = new() {{initializer}};
                    string json = FastPathJsonSerializer.Serialize<Collection, Witness>(value);
                    return FastPathJsonSerializer.Serialize<Collection, Witness>(
                        FastPathJsonSerializer.Deserialize<Collection, Witness>(json));
                }
            }
            """;
        var result = CompilationHelpers.Run(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Empty(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning));
        ForEachStatementSyntax loop = Assert.Single(result.Driver.GetRunResult().Results.Single().GeneratedSources
            .SelectMany(s => s.SyntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<ForEachStatementSyntax>()));
        ForEachStatementInfo enumeration = result.Compilation.GetSemanticModel(loop.SyntaxTree).GetForEachStatementInfo(loop);
        Assert.NotNull(enumeration.GetEnumeratorMethod);
        Assert.NotNull(enumeration.MoveNextMethod);
        Assert.NotNull(enumeration.CurrentProperty);
        if (requiresInterface)
        {
            Assert.IsType<CastExpressionSyntax>(loop.Expression);
            Assert.Equal(TypeKind.Interface, enumeration.GetEnumeratorMethod.ContainingType.TypeKind);
        }
        else
        {
            Assert.IsType<IdentifierNameSyntax>(loop.Expression);
            Assert.True(enumeration.GetEnumeratorMethod.ReturnType.IsValueType);
            Assert.True(enumeration.MoveNextMethod.ContainingType.IsValueType);
            Assert.True(enumeration.CurrentProperty.ContainingType.IsValueType);
        }

        using MemoryStream stream = new();
        var emitted = result.Compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        stream.Position = 0;
        AssemblyLoadContext context = new(nameof(CollectionEnumerationTests), isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(stream);
            var consumer = assembly.GetType("TestNamespace.Consumer");
            Assert.NotNull(consumer);
            var method = consumer.GetMethod("Roundtrip");
            Assert.NotNull(method);
            Assert.Equal(expected, method.CreateDelegate<Func<string>>()());
        }
        finally
        {
            context.Unload();
        }
    }
}
