using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TypeShapeExtensionAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Parser.ConflictingMarshallers);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(
            context =>
            {
                PolyTypeKnownSymbols knownSymbols = new(context.Compilation);
                if (knownSymbols.TypeShapeExtensionAttribute is null)
                {
                    return;
                }

                Dictionary<INamedTypeSymbol, (INamedTypeSymbol Marshaller, Location AttributeLocation)> extensionMarshaler = new(SymbolEqualityComparer.Default);
                context.RegisterOperationAction(
                    context =>
                    {
                        IAttributeOperation op = (IAttributeOperation)context.Operation;
                        if (op.Operation is not IObjectCreationOperation attCtor)
                        {
                            return;
                        }

                        if (!SymbolEqualityComparer.Default.Equals(attCtor.Constructor?.ContainingType, knownSymbols.TypeShapeExtensionAttribute))
                        {
                            return;
                        }

                        if (attCtor.Arguments is not [{ Value: ITypeOfOperation { TypeOperand: INamedTypeSymbol target } }, ..])
                        {
                            return;
                        }

                        foreach (IObjectOrCollectionInitializerOperation initializerOp in attCtor.ChildOperations.OfType<IObjectOrCollectionInitializerOperation>())
                        {
                            foreach (ISimpleAssignmentOperation assignmentOp in initializerOp.Initializers.OfType<ISimpleAssignmentOperation>())
                            {
                                if (assignmentOp is { Target: IPropertyReferenceOperation { Property.Name: PolyTypeKnownSymbols.TypeShapeExtensionAttributePropertyNames.Marshaller }, Value: ITypeOfOperation { TypeOperand: INamedTypeSymbol marshaller, Syntax: { } marshallerSyntax } })
                                {
                                    Location thisLocation = marshallerSyntax.GetLocation();
                                    if (extensionMarshaler.TryGetValue(target, out var existingMarshaller) && !SymbolEqualityComparer.Default.Equals(existingMarshaller.Marshaller, marshaller))
                                    {
                                        // We have a conflicting attribute.
                                        Location[] addlLocations = [existingMarshaller.AttributeLocation];
                                        context.ReportDiagnostic(Diagnostic.Create(Parser.ConflictingMarshallers, thisLocation, additionalLocations: addlLocations, target.MetadataName));
                                    }
                                    else
                                    {
                                        extensionMarshaler.Add(target, (marshaller, thisLocation));
                                    }
                                }
                            }
                        }
                    },
                    OperationKind.Attribute);
            });
    }
}
