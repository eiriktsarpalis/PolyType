using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;

namespace PolyType.FastPathJson.SourceGenerator;

/// <summary>Generates statically dispatched JSON converters from PolyType.Roslyn data models.</summary>
[Generator]
public sealed class JsonSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var parsed = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                static (context, _) => (TypeDeclarationSyntax)context.Node)
            .Collect()
            .Combine(context.CompilationProvider)
            .Select(static (input, token) => Parser.Parse(input.Right, input.Left, token));

        var models = parsed.Select(static (result, _) => result.Model)
            .WithTrackingName("FastPathJsonModel");
        context.RegisterSourceOutput(models, static (context, model) => SourceFormatter.Generate(context, model));
        context.RegisterSourceOutput(parsed, static (context, result) =>
        {
            foreach (Diagnostic diagnostic in result.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }
        });
    }
}
