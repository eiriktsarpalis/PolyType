using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PolyType.SourceGenerator;

public sealed partial class Parser : TypeDataModelGenerator
{
    // C# 12 is the minimum LTS version that supports static abstracts and generic attributes.
    private const LanguageVersion MinimumSupportedLanguageVersion = LanguageVersion.CSharp12;

    private static readonly IEqualityComparer<(ITypeSymbol Type, string Name)> s_ctorParamComparer =
        CommonHelpers.CreateTupleComparer<ITypeSymbol, string>(
            SymbolEqualityComparer.Default,
            CommonHelpers.CamelCaseInvariantComparer.Instance);
    
    private readonly PolyTypeKnownSymbols _knownSymbols;

    private Parser(ISymbol generationScope, PolyTypeKnownSymbols knownSymbols, CancellationToken cancellationToken)
        : base(generationScope, knownSymbols, cancellationToken)
    {
        _knownSymbols = knownSymbols;
    }

    public static TypeShapeProviderModel? ParseFromGenerateShapeAttributes(
        ImmutableArray<TypeWithAttributeDeclarationContext> generateShapeDeclarations,
        PolyTypeKnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (generateShapeDeclarations.IsEmpty)
        {
            return null;
        }

        Parser parser = new(knownSymbols.Compilation.Assembly, knownSymbols, cancellationToken);
        TypeDeclarationModel shapeProviderDeclaration = CreateShapeProviderDeclaration(knownSymbols.Compilation);
        ImmutableEquatableArray<TypeDeclarationModel> generateShapeTypes = parser.IncludeTypesUsingGenerateShapeAttributes(generateShapeDeclarations);
        return parser.ExportTypeShapeProviderModel(shapeProviderDeclaration, generateShapeTypes);
    }

    // We want to flatten System.Tuple types for consistency with
    // the reflection-based provider (which caters to F# model types).
    protected override bool FlattenSystemTupleTypes => true;

    // All types used as generic parameters so we must exclude ref structs.
    protected override bool IsSupportedType(ITypeSymbol type) =>
        base.IsSupportedType(type) && !type.IsRefLikeType && !type.IsStatic;

    // Erase nullable annotations and tuple labels from generated types.
    protected override ITypeSymbol NormalizeType(ITypeSymbol type) =>
        KnownSymbols.Compilation.EraseCompilerMetadata(type);

    // Ignore properties with the [PropertyShape] attribute set to Ignore = true.
    protected override bool IncludeProperty(IPropertySymbol property, out bool includeGetter, out bool includeSetter)
    {
        if (property.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData propertyAttribute)
        {
            bool includeProperty = !propertyAttribute.TryGetNamedArgument("Ignore", out bool ignoreValue) || !ignoreValue;
            if (includeProperty)
            {
                // Use the signature of the base property to determine shape.
                property = property.GetBaseProperty();
                includeGetter = property.GetMethod is not null;
                includeSetter = property.SetMethod is not null;
                return true;
            }

            includeGetter = includeSetter = false;
            return false;
        }

        return base.IncludeProperty(property, out includeGetter, out includeSetter);
    }

    // Ignore fields with the [PropertyShape] attribute set to Ignore = true.
    protected override bool IncludeField(IFieldSymbol field)
    {
        if (field.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData fieldAttribute)
        {
            return !fieldAttribute.TryGetNamedArgument("Ignore", out bool ignoreValue) || !ignoreValue;
        }

        return base.IncludeField(field);
    }

    // Resolve constructors with the [ConstructorShape] attribute.
    protected override IEnumerable<IMethodSymbol> ResolveConstructors(ITypeSymbol type, ImmutableArray<PropertyDataModel> properties)
    {
        // Search for constructors that have the [ConstructorShape] attribute. Ignore accessibility modifiers in this step.
        IMethodSymbol[] constructors = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ctor => ctor is { IsStatic: false, MethodKind: MethodKind.Constructor })
            .Where(ctor => ctor.HasAttribute(_knownSymbols.ConstructorShapeAttribute))
            .ToArray();

        if (constructors.Length == 1)
        {
            return constructors; // Found a unique match, return that.
        }

        if (constructors.Length > 1)
        {
            // We have a conflict, report a diagnostic and pick one using the default heuristic.
            ReportDiagnostic(DuplicateConstructorShape, constructors[^1].Locations.FirstOrDefault(), type.ToDisplayString());
        }
        else
        {
            // Otherwise, just resolve the public constructors on the type.
            constructors = base.ResolveConstructors(type, properties)
                .Where(ctor => ctor.DeclaredAccessibility is Accessibility.Public)
                .ToArray();
        }

        // In case of ambiguity, return the constructor that maximizes
        // the number of parameters corresponding to read-only properties.
        HashSet<(ITypeSymbol, string)> readOnlyProperties = new(
            properties
                .Where(p => !p.IncludeSetter)
                .Select(p => (p.PropertyType, p.Name)), 
            s_ctorParamComparer);
            
        return constructors
            .OrderByDescending(ctor =>
            {
                int paramsMatchingReadOnlyMembers = ctor.Parameters.Count(p => readOnlyProperties.Contains((p.Type, p.Name)));
                // In case of a tie, pick the ctor with the smallest arity.
                return (paramsMatchingReadOnlyMembers, -ctor.Parameters.Length);
            })
            .Take(1);
    }

    protected override IEnumerable<DerivedTypeModel> ResolveDerivedTypes(ITypeSymbol type)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Interface))
        {
            yield break;
        }

        int i = 0;
        HashSet<ITypeSymbol> types = new(SymbolEqualityComparer.Default);
        HashSet<int> tags = new();
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _knownSymbols.DerivedTypeShapeAttribute))
            {
                continue;
            }

            ParseDerivedTypeShapeAttribute(attribute, out ITypeSymbol derivedType, out string? name, out int tag);

            if (!type.IsAssignableFrom(derivedType))
            {
                ReportDiagnostic(DerivedTypeNotAssignableToBase, attribute.GetLocation(), derivedType.ToDisplayString(), type.ToDisplayString());
                continue;
            }

            bool isTagSpecified = tag >= 0;
            tag = isTagSpecified ? tag : i;
            name ??= derivedType.Name;

            if (!types.Add(derivedType))
            {
                ReportDiagnostic(DerivedTypeDuplicateMetadata, attribute.GetLocation(), type.ToDisplayString(), "type", derivedType.ToDisplayString());
                continue;
            }

            if (!tags.Add(tag))
            {
                ReportDiagnostic(DerivedTypeDuplicateMetadata, attribute.GetLocation(), type.ToDisplayString(), "tag", tag);
                continue;
            }

            if (!names.Add(name))
            {
                ReportDiagnostic(DerivedTypeDuplicateMetadata, attribute.GetLocation(), type.ToDisplayString(), "name", name);
                continue;
            }

            yield return new DerivedTypeModel
            {
                Type = derivedType,
                Name = name,
                Tag = tag,
                IsTagSpecified = isTagSpecified,
                Index = i,
                IsBaseType = SymbolEqualityComparer.Default.Equals(derivedType, type),
            };

            i++;
        }
    }

    protected override TypeDataModelGenerationStatus MapType(ITypeSymbol type, TypeDataKind? requestedKind, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model)
    {
        Debug.Assert(requestedKind is null);
        ParseTypeShapeAttribute(type, out TypeShapeKind? requestedTypeShapeKind, out ITypeSymbol? marshaller, out Location? location);

        if (marshaller is not null || requestedTypeShapeKind is TypeShapeKind.Surrogate)
        {
            return MapSurrogateType(type, marshaller, ref ctx, out model);
        }

        if (_knownSymbols.ResolveFSharpUnionMetadata(type) is FSharpUnionInfo unionInfo)
        {
            return unionInfo switch
            {
                FSharpOptionInfo optionInfo => MapFSharpOptionDataModel(optionInfo, ref ctx, out model),
                _ => MapFSharpUnionDataModel((GenericFSharpUnionInfo)unionInfo, ref ctx, out model),
            };
        }

        requestedKind = MapTypeShapeKindToDataKind(requestedTypeShapeKind);
        TypeDataModelGenerationStatus status = base.MapType(type, requestedKind, ref ctx, out model);

        if (requestedKind is not null && model is { Kind: TypeDataKind actualKind } && requestedKind != actualKind)
        {
            ReportDiagnostic(InvalidTypeShapeKind, location, requestedKind.Value, type.ToDisplayString());
        }

        return status;
    }

    private TypeDataModelGenerationStatus MapSurrogateType(ITypeSymbol type, ITypeSymbol? marshaller, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model)
    {
        model = null;

        if (marshaller is not INamedTypeSymbol namedMarshaller)
        {
            return ReportInvalidMarshallerAndExit();
        }

        if (namedMarshaller.IsUnboundGenericType)
        {
            // If the marshaller type is an unbound generic,
            // apply type arguments from the declaring type.
            ITypeSymbol[] typeArgs = ((INamedTypeSymbol)type).GetRecursiveTypeArguments();
            INamedTypeSymbol? specializedMarshaller = namedMarshaller.OriginalDefinition.ConstructRecursive(typeArgs);
            if (specializedMarshaller is null)
            {
                return ReportInvalidMarshallerAndExit();
            }

            namedMarshaller = specializedMarshaller;
        }

        IMethodSymbol? defaultCtor = namedMarshaller.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => method is { MethodKind: MethodKind.Constructor, IsStatic: false, Parameters: [] });

        if (defaultCtor is null || !IsAccessibleSymbol(defaultCtor))
        {
            return ReportInvalidMarshallerAndExit();
        }

        // Check that the surrogate marshaller implements exactly one IMarshaller<,> for the source type.
        ITypeSymbol? surrogateType = null;
        foreach (INamedTypeSymbol interfaceType in namedMarshaller.AllInterfaces)
        {
            if (interfaceType.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(interfaceType.OriginalDefinition, _knownSymbols.MarshallerType))
            {
                var typeArgs = interfaceType.TypeArguments;
                if (SymbolEqualityComparer.Default.Equals(typeArgs[0], type))
                {
                    if (surrogateType is not null)
                    {
                        // We have conflicting implementations.
                        return ReportInvalidMarshallerAndExit();
                    }

                    surrogateType = typeArgs[1];
                }
            }
        }

        if (surrogateType is null)
        {
            return ReportInvalidMarshallerAndExit();
        }

        // Generate the shape for the surrogate type.
        TypeDataModelGenerationStatus status = IncludeNestedType(surrogateType, ref ctx);
        if (status is TypeDataModelGenerationStatus.Success)
        {
            model = new SurrogateTypeDataModel
            {
                Type = type,
                SurrogateType = surrogateType,
                MarshallerType = namedMarshaller,
            };
        }

        return status;

        TypeDataModelGenerationStatus ReportInvalidMarshallerAndExit()
        {
            ReportDiagnostic(InvalidMarshaller, type.Locations.FirstOrDefault(), type.ToDisplayString());
            return TypeDataModelGenerationStatus.UnsupportedType;
        }
    }

    private TypeDataModelGenerationStatus MapFSharpOptionDataModel(FSharpOptionInfo optionInfo, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model)
    {
        TypeDataModelGenerationStatus status = IncludeNestedType(optionInfo.ElementType, ref ctx);
        if (status is not TypeDataModelGenerationStatus.Success)
        {
            model = null;
            return status;
        }

        model = new OptionalDataModel
        {
            Type = optionInfo.Type,
            ElementType = optionInfo.ElementType,
        };

        return status;
    }

    private TypeDataModelGenerationStatus MapFSharpUnionDataModel(GenericFSharpUnionInfo unionInfo, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model)
    {
        List<FSharpUnionCaseDataModel> unionCaseModels = new(unionInfo.UnionCases.Length);
        foreach (FSharpUnionCaseInfo unionCaseInfo in unionInfo.UnionCases)
        {
            TypeDataModelGenerationStatus status = MapUnionCaseModel(unionCaseInfo, ref ctx, out ObjectDataModel? unionCaseModel);
            if (status is not TypeDataModelGenerationStatus.Success)
            {
                model = null;
                return status;
            }

            Debug.Assert(unionCaseInfo.Tag == unionCaseModels.Count);
            unionCaseModels.Add(
                new FSharpUnionCaseDataModel
                {
                    Name = unionCaseInfo.Name,
                    Tag = unionCaseInfo.Tag,
                    Type = unionCaseModel!,
                });
        }

        model = new FSharpUnionDataModel
        {
            Type = unionInfo.Type,
            UnionCases = unionCaseModels.ToImmutableArray(),
            TagReader = unionInfo.TagReader,
        };

        return TypeDataModelGenerationStatus.Success;
    }

    private TypeDataModelGenerationStatus MapUnionCaseModel(
        FSharpUnionCaseInfo unionCaseInfo,
        ref TypeDataModelGenerationContext ctx,
        out ObjectDataModel? model)
    {
        List<PropertyDataModel> properties = new(unionCaseInfo.Properties.Length);
        foreach (IPropertySymbol property in unionCaseInfo.Properties)
        {
            TypeDataModelGenerationStatus status = IncludeNestedType(property.Type, ref ctx);
            if (status is not TypeDataModelGenerationStatus.Success)
            {
                model = null;
                return status;
            }

            PolyType.Roslyn.Helpers.RoslynHelpers.ResolveNullableAnnotation(property, out bool isGetterNonNullable, out bool _);
            properties.Add(new PropertyDataModel(property)
            {
                IncludeGetter = true,
                IsGetterNonNullable = isGetterNonNullable,
                IsGetterAccessible = true,
                IncludeSetter = false,
                IsSetterAccessible = false,
                IsSetterNonNullable = false,
            });
        }

        Debug.Assert(unionCaseInfo.Constructor is IPropertySymbol { IsStatic: true } or IMethodSymbol { IsStatic: true });
        ImmutableArray<IParameterSymbol> parameters = unionCaseInfo.Constructor switch
        {
            IMethodSymbol constructor => constructor.Parameters,
            _ => ImmutableArray<IParameterSymbol>.Empty,
        };

        ConstructorDataModel constructorDataModel = new()
        {
            Constructor = unionCaseInfo.Constructor,
            Parameters = parameters
                .Select(p => new ConstructorParameterDataModel { Parameter = p })
                .ToImmutableArray(),
            MemberInitializers = ImmutableArray<PropertyDataModel>.Empty,
        };

        model = new ObjectDataModel
        {
            Type = unionCaseInfo.DeclaringType,
            Properties = properties.ToImmutableArray(),
            Constructors = ImmutableArray.Create(constructorDataModel),
        };

        return TypeDataModelGenerationStatus.Success;
    }

    private TypeShapeProviderModel ExportTypeShapeProviderModel(TypeDeclarationModel providerDeclaration, ImmutableEquatableArray<TypeDeclarationModel> generateShapeTypes)
    {
        return new TypeShapeProviderModel
        {
            ProviderDeclaration = providerDeclaration,
            ProvidedTypes = GetGeneratedTypesAndIdentifiers()
                .ToImmutableEquatableDictionary(
                    keySelector: kvp => kvp.Key,
                    valueSelector: kvp => MapModel(kvp.Value.Model, kvp.Value.TypeId, kvp.Value.SourceIdentifier)),

            AnnotatedTypes = generateShapeTypes,
            Diagnostics = Diagnostics.ToImmutableEquatableSet(),
        };
    }

    private ImmutableEquatableArray<TypeDeclarationModel> IncludeTypesUsingGenerateShapeAttributes(ImmutableArray<TypeWithAttributeDeclarationContext> generateShapeDeclarations)
    {
        if (_knownSymbols.Compilation.GetLanguageVersion() is null or < MinimumSupportedLanguageVersion)
        {
            ReportDiagnostic(UnsupportedLanguageVersion, location: null);
            return [];
        }

        List<TypeDeclarationModel>? typeDeclarations = null;
        foreach (TypeWithAttributeDeclarationContext ctx in generateShapeDeclarations)
        {
            if (IncludeTypeUsingGenerateShapeAttributes(ctx) is { } typeDeclaration)
            {
                (typeDeclarations ??= []).Add(typeDeclaration);
            }
        }

        return typeDeclarations?.ToImmutableEquatableArray() ?? [];
    }

    private TypeDeclarationModel? IncludeTypeUsingGenerateShapeAttributes(TypeWithAttributeDeclarationContext context)
    {
        if (context.TypeSymbol.IsGenericTypeDefinition())
        {
            ReportDiagnostic(GenericTypeDefinitionsNotSupported, context.Declarations.First().Syntax.GetLocation(), context.TypeSymbol.ToDisplayString());
            return null;
        }

        (BaseTypeDeclarationSyntax? declarationSyntax, SemanticModel? semanticModel) = context.Declarations.First();
        string typeDeclarationHeader = FormatTypeDeclarationHeader(declarationSyntax, context.TypeSymbol, out bool isPartialHierarchy);

        Stack<string>? parentStack = null;
        for (SyntaxNode? parentNode = declarationSyntax.Parent; parentNode is BaseTypeDeclarationSyntax parentType; parentNode = parentNode.Parent)
        {
            ITypeSymbol parentSymbol = semanticModel.GetDeclaredSymbol(parentType, CancellationToken)!;
            string parentHeader = FormatTypeDeclarationHeader(parentType, parentSymbol, out bool isPartialType);
            (parentStack ??= new()).Push(parentHeader);
            isPartialHierarchy &= isPartialType;
        }

        if (!isPartialHierarchy)
        {
            ReportDiagnostic(GeneratedTypeNotPartial, declarationSyntax.GetLocation(), context.TypeSymbol.ToDisplayString());
            return null;
        }

        if (context.TypeSymbol.IsStatic)
        {
            ReportDiagnostic(GeneratedTypeIsStatic, declarationSyntax.GetLocation(), context.TypeSymbol.ToDisplayString());
            return null;
        }

        TypeId typeId = CreateTypeId(context.TypeSymbol);
        HashSet<TypeId>? shapeableOfTImplementations = null;
        bool isWitnessTypeDeclaration = false;

        foreach (AttributeData attributeData in context.TypeSymbol.GetAttributes())
        {
            ITypeSymbol typeToInclude;
            TypeId typeIdToInclude;

            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, _knownSymbols.GenerateShapeAttribute))
            {
                typeToInclude = context.TypeSymbol;
                typeIdToInclude = typeId;
            }
            else if (
                attributeData.AttributeClass is { TypeArguments: [ITypeSymbol typeArgument] } &&
                SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass.ConstructedFrom, _knownSymbols.GenerateShapeAttributeOfT))
            {
                typeToInclude = typeArgument;
                typeIdToInclude = CreateTypeId(typeArgument);
                isWitnessTypeDeclaration = true;
            }
            else
            {
                continue;
            }

            switch (IncludeType(typeToInclude))
            {
                case TypeDataModelGenerationStatus.UnsupportedType:
                    ReportDiagnostic(TypeNotSupported, attributeData.GetLocation(), typeToInclude.ToDisplayString());
                    continue;

                case TypeDataModelGenerationStatus.InaccessibleType:
                    ReportDiagnostic(TypeNotAccessible, attributeData.GetLocation(), typeToInclude.ToDisplayString());
                    continue;
            }

            if (_knownSymbols.TargetFramework >= TargetFramework.Net80)
            {
                // IShapeable<T> has static abstracts and is only defined in newer TFMs.
                (shapeableOfTImplementations ??= new()).Add(typeIdToInclude);
            }
        }

        return new TypeDeclarationModel
        {
            Id = typeId,
            Name = context.TypeSymbol.Name,
            TypeDeclarationHeader = typeDeclarationHeader,
            ContainingTypes = parentStack?.ToImmutableEquatableArray() ?? [],
            Namespace = FormatNamespace(context.TypeSymbol),
            SourceFilenamePrefix = context.TypeSymbol.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat),
            IsWitnessTypeDeclaration = isWitnessTypeDeclaration,
            ShapeableOfTImplementations = shapeableOfTImplementations?.ToImmutableEquatableSet() ?? [],
        };

        static string FormatTypeDeclarationHeader(BaseTypeDeclarationSyntax typeDeclaration, ITypeSymbol typeSymbol, out bool isPartialType)
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

    private Dictionary<TypeId, (TypeDataModel Model, TypeId TypeId, string SourceIdentifier)> GetGeneratedTypesAndIdentifiers()
    {
        Dictionary<TypeId, (TypeDataModel Model, TypeId TypeId, string SourceIdentifier)> results = new(GeneratedModels.Count);
        Dictionary<string, TypeId?> shortIdentifiers = new(GeneratedModels.Count);
        ReadOnlySpan<string> reservedIdentifiers = SourceFormatter.ReservedIdentifiers;

        foreach (KeyValuePair<ITypeSymbol, TypeDataModel> entry in GeneratedModels)
        {
            TypeId typeId = CreateTypeId(entry.Value.Type);
            if (results.ContainsKey(typeId))
            {
                // We can't have duplicate types with the same fully qualified name.
                ReportDiagnostic(TypeNameConflict, location: null, typeId.FullyQualifiedName);
                continue;
            }

            // Generate a property name for the type. Start with a short-form name that
            // doesn't include namespaces or containing types. If there is a conflict,
            // we will update the identifiers to incorporate fully qualified names.
            // Fully qualified names should not have conflicts since we've already checked

            string sourceIdentifier = entry.Value.Type.CreateTypeIdentifier(reservedIdentifiers, includeNamespaces: false);
            if (!shortIdentifiers.TryGetValue(sourceIdentifier, out TypeId? conflictingIdentifier))
            {
                // This is the first occurrence of the short-form identifier.
                // Add to the index including the typeId in case of a later conflict.
                shortIdentifiers.Add(sourceIdentifier, typeId);
            }
            else
            {
                // We have a conflict, update the identifiers of both types to long-form.
                if (conflictingIdentifier is { } cId)
                {
                    // Update the identifier of the conflicting type since it hasn't been already.
                    var conflictingResults = results[cId];
                    conflictingResults.SourceIdentifier = conflictingResults.Model.Type.CreateTypeIdentifier(reservedIdentifiers, includeNamespaces: true);
                    results[cId] = conflictingResults;

                    // Mark the short-form identifier as updated.
                    shortIdentifiers[sourceIdentifier] = null;
                }

                // Update the identifier of the current type and store the new key.
                sourceIdentifier = entry.Value.Type.CreateTypeIdentifier(reservedIdentifiers, includeNamespaces: true);
            }

            results.Add(typeId, (entry.Value, typeId, sourceIdentifier));
        }

        return results;
    }

    private static void ParseDerivedTypeShapeAttribute(
        AttributeData attributeData,
        out ITypeSymbol derivedType,
        out string? name,
        out int tag)
    {
        derivedType = (ITypeSymbol)attributeData.ConstructorArguments.First().Value!;
        name = null;
        tag = -1;

        foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
        {
            switch (namedArgument.Key)
            {
                case "Name":
                    name = (string)namedArgument.Value.Value!;
                    break;
                case "Tag":
                    tag = (int)namedArgument.Value.Value!;
                    break;
            }
        }
    }

    private static TypeId CreateTypeId(ITypeSymbol type)
    {
        return new TypeId
        {
            FullyQualifiedName = type.GetFullyQualifiedName(),
            IsValueType = type.IsValueType,
            SpecialType = type.OriginalDefinition.SpecialType,
        };
    }

    private static string? FormatNamespace(ITypeSymbol type)
    {
        if (type.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            return ns.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat);
        }

        return null;
    }

    private static TypeDeclarationModel CreateShapeProviderDeclaration(Compilation compilation)
    {
        string typeName = !string.IsNullOrWhiteSpace(compilation.AssemblyName)
            ? "ShapeProvider_" + s_escapeAssemblyName.Replace(compilation.AssemblyName, "_")
            : "ShapeProvider";

        return new()
        {
            Id = new()
            {
                FullyQualifiedName = $"global::PolyType.SourceGenerator.{typeName}",
                IsValueType = false,
                SpecialType = SpecialType.None,
            },
            Name = typeName,
            Namespace = "PolyType.SourceGenerator",
            SourceFilenamePrefix = "PolyType.SourceGenerator.ShapeProvider",
            TypeDeclarationHeader = $"internal sealed partial class {typeName}",
            IsWitnessTypeDeclaration = false,
            ContainingTypes = [],
            ShapeableOfTImplementations = [],
        };
    }

    private static readonly Regex s_escapeAssemblyName = new(@"[^\w]", RegexOptions.Compiled);
}
