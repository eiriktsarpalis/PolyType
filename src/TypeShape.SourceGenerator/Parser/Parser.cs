﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class Parser : TypeDataModelGenerator
{
    // We want to flatten System.Tuple types for consistency with
    // the reflection-based provider (which caters to F# model types).
    protected override bool FlattenSystemTupleTypes => true;

    // All types used as generic parameters so we must exclude ref structs.
    protected override bool IsSupportedType(ITypeSymbol type)
        => base.IsSupportedType(type) && !type.IsRefLikeType;

    // Erase nullable annotations and tuple labels from generated types.
    protected override ITypeSymbol NormalizeType(ITypeSymbol type)
        => KnownSymbols.Compilation.EraseCompilerMetadata(type);

    private Parser(ISymbol generationScope, TypeShapeKnownSymbols knownSymbols, CancellationToken cancellationToken) 
        : base(generationScope, knownSymbols, cancellationToken)
    { 
    }

    public static TypeShapeProviderModel ParseFromGenerateShapeOfTAttributes(
        TypeWithAttributeDeclarationContext context,
        TypeShapeKnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        Parser parser = new(context.TypeSymbol, knownSymbols, cancellationToken);
        TypeId declaringTypeId = CreateTypeId(context.TypeSymbol);

        TypeDeclarationModel providerDeclaration = parser.CreateTypeDeclaration(context, declaringTypeId);
        parser.IncludeTypesFromGenerateShapeOfTAttributes(context.TypeSymbol);
        return parser.ExportTypeShapeProviderModel(providerDeclaration, [], isGeneratedViaWitnessType: true);
    }

    public static TypeShapeProviderModel? ParseFromGenerateShapeAttributes(
        ImmutableArray<TypeWithAttributeDeclarationContext> generateShapeDeclarations,
        TypeShapeKnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (generateShapeDeclarations.IsEmpty)
        {
            return null;
        }

        Parser parser = new(knownSymbols.Compilation.Assembly, knownSymbols, cancellationToken);
        ImmutableEquatableArray<TypeDeclarationModel> generateShapeTypes = parser.IncludeTypesFromGenerateShapeAttributes(generateShapeDeclarations);
        return parser.ExportTypeShapeProviderModel(s_globalImplicitProviderDeclaration, generateShapeTypes, isGeneratedViaWitnessType: false);
    }

    private TypeShapeProviderModel ExportTypeShapeProviderModel(TypeDeclarationModel providerDeclaration, ImmutableEquatableArray<TypeDeclarationModel> generateShapeTypes, bool isGeneratedViaWitnessType)
    {
        Dictionary<TypeId, TypeShapeModel> generatedModels = new(GeneratedModels.Count);
        foreach (KeyValuePair<ITypeSymbol, TypeDataModel> entry in GeneratedModels)
        {
            TypeId typeId = CreateTypeId(entry.Value.Type);
            if (generatedModels.ContainsKey(typeId))
            {
                ReportDiagnostic(TypeNameConflict, location: null, typeId.FullyQualifiedName);
            }

            generatedModels[typeId] = MapModel(typeId, entry.Value, isGeneratedViaWitnessType);
        }

        return new TypeShapeProviderModel
        {
            Declaration = providerDeclaration,
            ProvidedTypes = generatedModels.ToImmutableEquatableDictionary(),
            GenerateShapeTypes = generateShapeTypes,
            Diagnostics = Diagnostics.ToImmutableEquatableSet(),
        };
    }

    private void IncludeTypesFromGenerateShapeOfTAttributes(ITypeSymbol declaringTypeSymbol)
    {
        Debug.Assert(declaringTypeSymbol.TypeKind is TypeKind.Class);

        foreach (AttributeData attributeData in declaringTypeSymbol.GetAttributes())
        {
            INamedTypeSymbol? attributeType = attributeData.AttributeClass;

            if (attributeType is { TypeArguments: [ITypeSymbol typeArgument] } &&
                SymbolEqualityComparer.Default.Equals(attributeType.ConstructedFrom, ((TypeShapeKnownSymbols)KnownSymbols).GenerateShapeAttributeOfT))
            {
                TypeDataModelGenerationStatus generationStatus = IncludeType(typeArgument);

                if (generationStatus is TypeDataModelGenerationStatus.UnsupportedType)
                {
                    ReportDiagnostic(TypeNotSupported, attributeData.GetLocation(), typeArgument.ToDisplayString());
                    continue;
                }

                if (generationStatus is TypeDataModelGenerationStatus.InaccessibleType)
                {
                    ReportDiagnostic(TypeNotAccessible, attributeData.GetLocation(), typeArgument.ToDisplayString());
                    continue;
                }
            }
        }
    }

    private ImmutableEquatableArray<TypeDeclarationModel> IncludeTypesFromGenerateShapeAttributes(ImmutableArray<TypeWithAttributeDeclarationContext> generateShapeDeclarations)
    {
        var typeDeclarations = new List<TypeDeclarationModel>();
        foreach (TypeWithAttributeDeclarationContext ctx in generateShapeDeclarations)
        {
            if (ctx.TypeSymbol.IsGenericTypeDefinition())
            {
                ReportDiagnostic(GenericTypeDefinitionsNotSupported, ctx.DeclarationSyntax.GetLocation(), ctx.TypeSymbol.ToDisplayString());
                continue;
            }

            TypeDataModelGenerationStatus generationStatus = IncludeType(ctx.TypeSymbol);

            if (generationStatus is TypeDataModelGenerationStatus.UnsupportedType)
            {
                ReportDiagnostic(TypeNotSupported, ctx.DeclarationSyntax.GetLocation(), ctx.TypeSymbol.ToDisplayString());
                continue;
            }

            if (generationStatus is TypeDataModelGenerationStatus.InaccessibleType)
            {
                ReportDiagnostic(TypeNotAccessible, ctx.DeclarationSyntax.GetLocation(), ctx.TypeSymbol.ToDisplayString());
                continue;
            }

            TypeDeclarationModel typeDeclaration = CreateTypeDeclaration(ctx, CreateTypeId(ctx.TypeSymbol));
            typeDeclarations.Add(typeDeclaration);
        }

        return typeDeclarations.ToImmutableEquatableArray();
    }

    private static TypeId CreateTypeId(ITypeSymbol type)
    {
        return new TypeId
        {
            FullyQualifiedName = type.GetFullyQualifiedName(),
            GeneratedPropertyName = type.GetGeneratedPropertyName(),
            IsValueType = type.IsValueType,
            SpecialType = type.OriginalDefinition.SpecialType,
        };
    }

    private TypeDeclarationModel CreateTypeDeclaration(TypeWithAttributeDeclarationContext context, TypeId typeId)
    {
        string typeDeclarationHeader = FormatTypeDeclarationHeader(context.DeclarationSyntax, context.TypeSymbol, CancellationToken, out bool isPartialHierarchy);

        Stack<string>? parentStack = null;
        for (SyntaxNode? parentNode = context.DeclarationSyntax.Parent; parentNode is BaseTypeDeclarationSyntax parentType; parentNode = parentNode.Parent)
        {
            ITypeSymbol parentSymbol = context.SemanticModel.GetDeclaredSymbol(parentType, CancellationToken)!;
            string parentHeader = FormatTypeDeclarationHeader(parentType, parentSymbol, CancellationToken, out bool isPartialType);
            (parentStack ??= new()).Push(parentHeader);
            isPartialHierarchy &= isPartialType;
        }

        if (!isPartialHierarchy)
        {
            ReportDiagnostic(GeneratedTypeNotPartial, context.DeclarationSyntax.GetLocation(), context.TypeSymbol.ToDisplayString());
        }

        return new TypeDeclarationModel
        {
            Id = typeId,
            Name = context.TypeSymbol.Name,
            TypeDeclarationHeader = typeDeclarationHeader,
            ContainingTypes = parentStack?.ToImmutableEquatableArray() ?? [],
            Namespace = FormatNamespace(context.TypeSymbol),
            SourceFilenamePrefix = context.TypeSymbol.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat),
        };

        static string FormatTypeDeclarationHeader(BaseTypeDeclarationSyntax typeDeclaration, ITypeSymbol typeSymbol, CancellationToken cancellationToken, out bool isPartialType)
        {
            StringBuilder stringBuilder = new();
            isPartialType = false;

            foreach (SyntaxToken modifier in typeDeclaration.Modifiers)
            {
                stringBuilder.Append(modifier.Text);
                stringBuilder.Append(' ');
                isPartialType |= modifier.IsKind(SyntaxKind.PartialKeyword);
            }

            stringBuilder.Append(typeDeclaration.GetTypeKindKeyword());
            stringBuilder.Append(' ');

            string typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            stringBuilder.Append(typeName);

            return stringBuilder.ToString();
        }
    }

    private static string? FormatNamespace(ITypeSymbol type)
    {
        if (type.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            return ns.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat);
        }

        return null;
    }

    private readonly static TypeDeclarationModel s_globalImplicitProviderDeclaration = new()
    {
        Id = new()
        {
            FullyQualifiedName = "global::TypeShape.SourceGenerator.GenerateShapeProvider",
            GeneratedPropertyName = "GenerateShapeProvider",
            IsValueType = false,
            SpecialType = SpecialType.None,
        },
        Name = "GenerateShapeProvider",
        Namespace = "TypeShape.SourceGenerator",
        SourceFilenamePrefix = "GenerateShapeProvider",
        TypeDeclarationHeader = "internal partial class GenerateShapeProvider",
        ContainingTypes = [],
    };
}
