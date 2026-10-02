using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PolyType.Roslyn;
using Xunit;

namespace PolyType.Roslyn.Tests;

public static class KnownSymbolsTests
{
    [Theory]
    [InlineData(false, false, TargetFramework.Legacy)]
    [InlineData(true, false, TargetFramework.Net80)]
    [InlineData(false, true, TargetFramework.Net90)]
    [InlineData(true, true, TargetFramework.Net90)]
    public static void TargetFramework_DetectsCoreLibraryCapabilities(bool hasSearchValues, bool hasAlternateEqualityComparer, TargetFramework expected)
    {
        string coreLibrarySource = $$"""
            namespace System
            {
                public class Object { }
                public class ValueType : Object { }
                public struct Void { }
                public struct Int32 { }
            }

            {{(hasSearchValues ? "namespace System.Buffers { public class SearchValues { } }" : "")}}
            {{(hasAlternateEqualityComparer ? "namespace System.Collections.Generic { public interface IAlternateEqualityComparer<TAlternate, T> { } }" : "")}}
            """;

        var coreLibrary = CSharpCompilation.Create(
            "CoreLibrary",
            [CSharpSyntaxTree.ParseText(coreLibrarySource, cancellationToken: TestContext.Current.CancellationToken)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.DoesNotContain(coreLibrary.GetDiagnostics(TestContext.Current.CancellationToken),
            diagnostic => diagnostic.Severity is DiagnosticSeverity.Error);
        var compilation = CSharpCompilation.Create(
            "Consumer",
            references: [coreLibrary.ToMetadataReference()],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var symbols = new KnownSymbols(compilation);
        Assert.Equal(expected, symbols.TargetFramework);

        const string decoySource = """
            namespace System.Buffers { public class SearchValues { } }
            namespace System.Collections.Generic { public interface IAlternateEqualityComparer<TAlternate, T> { } }
            """;
        var decoy = CSharpCompilation.Create(
            "Decoy",
            [CSharpSyntaxTree.ParseText(decoySource, cancellationToken: TestContext.Current.CancellationToken)],
            [coreLibrary.ToMetadataReference()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        symbols = new KnownSymbols(compilation.AddReferences(decoy.ToMetadataReference()));
        Assert.Equal(expected, symbols.TargetFramework);
    }
}
