using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PolyType.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TypeShapeExtensionAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Parser.ConflictingMarshalers);

    public override void Initialize(AnalysisContext context)
    {
        if (!Debugger.IsAttached)
        {
            context.EnableConcurrentExecution();
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(
            context =>
            {
                PolyTypeKnownSymbols knownSymbols = new(context.Compilation);
                if (knownSymbols.TypeShapeExtensionAttribute is null)
                {
                    return;
                }

                Dictionary<INamedTypeSymbol, (INamedTypeSymbol Marshaler, Location AttributeLocation)> extensionMarshaler = new(SymbolEqualityComparer.Default);
                context.RegisterSyntaxNodeAction(
                    context =>
                    {
                        AttributeSyntax att = (AttributeSyntax)context.Node;
                        if (att.Parent is not AttributeListSyntax { Target: { Identifier: { RawKind: (int)SyntaxKind.AssemblyKeyword } } })
                        {
                            return;
                        }

                        if (context.SemanticModel.GetSymbolInfo(att, context.CancellationToken).Symbol is not { } attSymbol)
                        {
                            return;
                        }

                        if (!SymbolEqualityComparer.Default.Equals(attSymbol.ContainingType, knownSymbols.TypeShapeExtensionAttribute))
                        {
                            return;
                        }

                        if (att.ArgumentList?.Arguments is not [{ Expression: TypeOfExpressionSyntax typeOfExpr }, ..])
                        {
                            // We expect the first argument to be a typeof expression.
                            return;
                        }

                        if (context.SemanticModel.GetTypeInfo(typeOfExpr.Type, context.CancellationToken).Type is not INamedTypeSymbol target)
                        {
                            // The type of the first argument must be a named type.
                            return;
                        }

                        // Find the marshaler argument, if present, and check for conflicts.
                        INamedTypeSymbol? marshaler = null;
                        Location? marshalerLocation = null;
                        if (att.ArgumentList?.Arguments.Count > 1)
                        {
                            // Look for a named argument: marshaler = typeof(...)
                            foreach (var arg in att.ArgumentList.Arguments)
                            {
                                if (arg.NameEquals?.Name.Identifier.Text == PolyTypeKnownSymbols.TypeShapeExtensionAttributePropertyNames.Marshaler &&
                                    arg.Expression is TypeOfExpressionSyntax marshalerTypeOf)
                                {
                                    var marshalerType = context.SemanticModel.GetTypeInfo(marshalerTypeOf.Type, context.CancellationToken).Type as INamedTypeSymbol;
                                    if (marshalerType is not null)
                                    {
                                        marshaler = marshalerType;
                                        marshalerLocation = marshalerTypeOf.GetLocation();
                                        break;
                                    }
                                }
                            }
                        }

                        if (marshaler is not null && marshalerLocation is not null)
                        {
                            if (extensionMarshaler.TryGetValue(target, out var existingMarshaler) &&
                                !SymbolEqualityComparer.Default.Equals(existingMarshaler.Marshaler, marshaler))
                            {
                                // We have a conflicting attribute.
                                Location[] addlLocations = [existingMarshaler.AttributeLocation];
                                context.ReportDiagnostic(Diagnostic.Create(Parser.ConflictingMarshalers, marshalerLocation, additionalLocations: addlLocations, target.MetadataName));
                            }
                            else
                            {
                                extensionMarshaler[target] = (marshaler, marshalerLocation);
                            }
                        }
                    },
                    SyntaxKind.Attribute);
            });
    }
}
