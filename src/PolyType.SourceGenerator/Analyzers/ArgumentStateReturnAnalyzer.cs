using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArgumentStateReturnAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor ArgumentStateNotReturned = new(
        id: "PT0033",
        title: "Argument state is not returned",
        messageFormat: "Argument state '{0}' passed to a constructor delegate should be returned after use",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ArgumentStateNotReturned);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetOperation(context.Node, context.CancellationToken) is not IInvocationOperation invocation)
        {
            return;
        }

        INamedTypeSymbol delegateType = invocation.TargetMethod.ContainingType.OriginalDefinition;
        if (delegateType.MetadataName != "Constructor`2" ||
            delegateType.ContainingNamespace.ToDisplayString() != "PolyType.Abstractions" ||
            invocation.Arguments is not [{ Parameter.RefKind: RefKind.Ref } argument] ||
            GetReferencedSymbol(argument.Value, context.SemanticModel, context.CancellationToken) is not { } stateSymbol ||
            !ImplementsArgumentState(invocation.TargetMethod.ContainingType.TypeArguments[0]))
        {
            return;
        }

        bool returnedInFinally = invocation.Syntax.Ancestors()
            .OfType<TryStatementSyntax>()
            .Where(tryStatement => tryStatement.Block.Span.Contains(invocation.Syntax.Span))
            .Any(tryStatement =>
                tryStatement.Finally is { Block: { } finallyBlock } &&
                IsStateReturnedAndUnusedAfter(finallyBlock.Statements, stateSymbol, context.SemanticModel, context.CancellationToken) &&
                IsStateUnusedAfterStatement(tryStatement, stateSymbol, context.SemanticModel, context.CancellationToken));

        bool returnedAfterInvocation = invocation.Syntax.Ancestors()
            .OfType<StatementSyntax>()
            .Where(statement => statement.Parent is BlockSyntax)
            .Any(statement =>
            {
                BlockSyntax block = (BlockSyntax)statement.Parent!;
                int statementIndex = block.Statements.IndexOf(statement);
                return statementIndex >= 0 &&
                    IsStateReturnedAndUnusedAfter(
                        block.Statements.Skip(statementIndex + 1),
                        stateSymbol,
                        context.SemanticModel,
                        context.CancellationToken);
            });

        if (returnedInFinally || returnedAfterInvocation)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ArgumentStateNotReturned, argument.Value.Syntax.GetLocation(), stateSymbol.Name));
    }

    private static ISymbol? GetReferencedSymbol(IOperation operation, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        while (operation is IConversionOperation or IParenthesizedOperation)
        {
            operation = operation switch
            {
                IConversionOperation conversion => conversion.Operand,
                IParenthesizedOperation parenthesized => parenthesized.Operand,
                _ => operation,
            };
        }

        return operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            _ => semanticModel.GetSymbolInfo(operation.Syntax, cancellationToken).Symbol,
        };
    }

    private static bool ImplementsArgumentState(ITypeSymbol type)
    {
        if (IsArgumentStateInterface(type))
        {
            return true;
        }

        return type switch
        {
            INamedTypeSymbol namedType => namedType.AllInterfaces.Any(IsArgumentStateInterface),
            ITypeParameterSymbol typeParameter =>
                typeParameter.AllInterfaces.Any(IsArgumentStateInterface) ||
                typeParameter.ConstraintTypes.Any(ImplementsArgumentState),
            _ => false,
        };
    }

    private static bool IsArgumentStateInterface(ITypeSymbol type) => type.ToDisplayString() == "PolyType.Abstractions.IArgumentState";

    private static bool IsStateReturned(StatementSyntax statement, ISymbol stateSymbol, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        return statement is ExpressionStatementSyntax
        {
            Expression: InvocationExpressionSyntax invocation,
        } &&
        semanticModel.GetOperation(invocation, cancellationToken) is IInvocationOperation
        {
            TargetMethod.Name: "Return",
            Instance: { } instance,
        } &&
        SymbolEqualityComparer.Default.Equals(GetReferencedSymbol(instance, semanticModel, cancellationToken), stateSymbol);
    }

    private static bool IsStateReturnedAndUnusedAfter(
        IEnumerable<StatementSyntax> statements,
        ISymbol stateSymbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        bool returned = false;
        foreach (StatementSyntax statement in statements)
        {
            if (returned && ContainsStateReference(statement, stateSymbol, semanticModel, cancellationToken))
            {
                return false;
            }

            returned |= IsStateReturned(statement, stateSymbol, semanticModel, cancellationToken);
        }

        return returned;
    }

    private static bool ContainsStateReference(
        StatementSyntax statement,
        ISymbol stateSymbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken) =>
        statement.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                stateSymbol));

    private static bool IsStateUnusedAfterStatement(
        StatementSyntax statement,
        ISymbol stateSymbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (statement.Parent is not BlockSyntax block)
        {
            return true;
        }

        int statementIndex = block.Statements.IndexOf(statement);
        return statementIndex < 0 ||
            !block.Statements.Skip(statementIndex + 1)
                .Any(nextStatement => ContainsStateReference(nextStatement, stateSymbol, semanticModel, cancellationToken));
    }
}
