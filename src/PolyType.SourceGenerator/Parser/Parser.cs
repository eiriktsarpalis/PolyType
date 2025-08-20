﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
using PolyType.Roslyn.Helpers;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace PolyType.SourceGenerator;

public sealed partial class Parser : TypeDataModelGenerator
{
    private const LanguageVersion MinimumSupportedLanguageVersion = LanguageVersion.CSharp9;

    private static readonly IEqualityComparer<(ITypeSymbol Type, string Name)> s_ctorParamComparer =
        CommonHelpers.CreateTupleComparer<ITypeSymbol, string>(
            SymbolEqualityComparer.Default,
            CommonHelpers.CamelCaseInvariantComparer.Instance);

    private readonly PolyTypeKnownSymbols _knownSymbols;
    private readonly IReadOnlyDictionary<ITypeSymbol, TypeExtensionModel> _typeShapeExtensions;

    private Parser(ISymbol generationScope, IReadOnlyDictionary<ITypeSymbol, TypeExtensionModel> typeShapeExtensions, PolyTypeKnownSymbols knownSymbols, CancellationToken cancellationToken)
        : base(generationScope, knownSymbols, cancellationToken)
    {
        _knownSymbols = knownSymbols;
        _typeShapeExtensions = typeShapeExtensions;
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

        Dictionary<ITypeSymbol, TypeExtensionModel> typeShapeExtensions = DiscoverTypeShapeExtensions(generateShapeDeclarations, knownSymbols, cancellationToken);
        Parser parser = new(knownSymbols.Compilation.Assembly, typeShapeExtensions, knownSymbols, cancellationToken);
        TypeDeclarationModel shapeProviderDeclaration = CreateShapeProviderDeclaration(knownSymbols.Compilation);
        ImmutableEquatableArray<TypeDeclarationModel> generateShapeTypes = parser.IncludeTypesUsingGenerateShapeAttributes(generateShapeDeclarations);
        return parser.ExportTypeShapeProviderModel(shapeProviderDeclaration, generateShapeTypes);
    }

    private static Dictionary<ITypeSymbol, TypeExtensionModel> DiscoverTypeShapeExtensions(
        ImmutableArray<TypeWithAttributeDeclarationContext> generateShapeDeclarations,
        PolyTypeKnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        Dictionary<ITypeSymbol, TypeExtensionModel> typeShapeExtensions = new(SymbolEqualityComparer.Default);

        // The compilation itself.
        DiscoverTypeShapeExtensionsInAssembly(knownSymbols.Compilation.Assembly, isCurrentCompilation: true);

        // Full referenced assemblies.
        foreach (MetadataReference metadataReference in knownSymbols.Compilation.References)
        {
            if (knownSymbols.Compilation.GetAssemblyOrModuleSymbol(metadataReference) is IAssemblySymbol referencedAssembly)
            {
                DiscoverTypeShapeExtensionsInAssembly(referencedAssembly, isCurrentCompilation: false);
            }
        }

        // In combining extensions from multiple sources, type extensions may be merged.
        // Non-mergeable details (e.g. the marshaler to use) will be resolved by preferring
        // the first definition found. We always scan the compilation itself first, so the
        // project always has the ability to resolve a conflict by defining the attribute directly.
        return typeShapeExtensions;

        void DiscoverTypeShapeExtensionsInAssembly(IAssemblySymbol assembly, bool isCurrentCompilation)
        {
            if (isCurrentCompilation)
            {
                // If processing the current compilation, also incorporate all configuration from
                // the [GenerateShape(For)] attributes applied to types in the compilation.
                foreach (TypeWithAttributeDeclarationContext typeWithAttr in generateShapeDeclarations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (AttributeData attributeData in typeWithAttr.TypeSymbol.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, knownSymbols.GenerateShapeAttribute))
                        {
                            ProcessExtensionAttribute(typeWithAttr.TypeSymbol, attributeData, assembly);
                        }
                        else if (
                            SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, knownSymbols.GenerateShapeForAttribute) &&
                            attributeData.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: ITypeSymbol nonGenericTypeArgument }])
                        {
                            ProcessExtensionAttribute(nonGenericTypeArgument, attributeData, assembly);
                        }
                        else if (
                            attributeData.AttributeClass is { TypeArguments: [ITypeSymbol typeArgument] } &&
                            SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass.ConstructedFrom, knownSymbols.GenerateShapeForAttributeOfT))
                        {
                            ProcessExtensionAttribute(typeArgument, attributeData, assembly);
                        }
                    }
                }
            }

            foreach (AttributeData attribute in assembly.GetAttributes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.TypeShapeExtensionAttribute))
                {
                    continue;
                }

                if (attribute.ConstructorArguments is not [{ Value: ITypeSymbol targetType }])
                {
                    continue;
                }

                ProcessExtensionAttribute(targetType, attribute, assembly);
            }
        }

        void ProcessExtensionAttribute(ITypeSymbol targetType, AttributeData attribute, IAssemblySymbol assembly)
        {
            TypeShapeKind? kind = null;
            MethodShapeFlags? includeMethodFlags = null;
            TypeShapeRequirements requirements = TypeShapeRequirements.Full;
            ImmutableArray<TypedConstant>? associatedTypesExpr = null;
            INamedTypeSymbol? marshaler = null;
            ImmutableArray<Location> locations = attribute.GetLocation() is Location loc
                ? ImmutableArray.Create(loc)
                : ImmutableArray<Location>.Empty;

            foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
            {
                switch (namedArgument.Key)
                {
                    case "Kind":
                        kind = (TypeShapeKind)namedArgument.Value.Value!;
                        break;
                    case "Marshaler":
                        if (namedArgument.Value.Value is INamedTypeSymbol m)
                        {
                            marshaler = m;
                        }
                        break;
                    case "IncludeMethods":
                        includeMethodFlags = (MethodShapeFlags)namedArgument.Value.Value!;
                        break;
                    case "AssociatedTypes":
                        associatedTypesExpr = namedArgument.Value.Values!;
                        break;
                    case "Requirements":
                        requirements = (TypeShapeRequirements)namedArgument.Value.Value!;
                        break;
                }
            }

            ImmutableArray<AssociatedTypeModel> associatedTypeModels;
            if (associatedTypesExpr is not null)
            {
                ImmutableArray<AssociatedTypeModel>.Builder builder = ImmutableArray.CreateBuilder<AssociatedTypeModel>(associatedTypesExpr?.Length ?? 0);
                foreach (TypedConstant tc in associatedTypesExpr ?? ImmutableArray<TypedConstant>.Empty)
                {
                    if (tc.Value is INamedTypeSymbol associatedType)
                    {
                        builder.Add(new AssociatedTypeModel(associatedType, assembly, attribute.GetLocation(), requirements));
                    }
                }

                associatedTypeModels = builder.ToImmutable();
            }
            else
            {
                associatedTypeModels = ImmutableArray<AssociatedTypeModel>.Empty;
            }

            // Merge with any pre-existing models for the same type.
            typeShapeExtensions.TryGetValue(targetType, out TypeExtensionModel? existing);
            TypeExtensionModel extensionModel = new()
            {
                Kind = existing?.Kind ?? kind,
                IncludeMethods = includeMethodFlags,
                Marshaler = existing?.Marshaler ?? marshaler,
                AssociatedTypes = existing?.AssociatedTypes.AddRange(associatedTypeModels) ?? associatedTypeModels,
                Locations = existing?.Locations.AddRange(locations) ?? locations,
            };

            typeShapeExtensions[targetType] = extensionModel;
        }
    }

    // We want to flatten System.Tuple types for consistency with
    // the reflection-based provider (which caters to F# model types).
    protected override bool FlattenSystemTupleTypes => true;

    // Full types used as generic parameters so we must exclude ref structs.
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

    protected override bool? IsRequiredByPolicy(IPropertySymbol member)
    {
        if (member.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData fieldAttribute && fieldAttribute.TryGetNamedArgument("IsRequired", out bool isRequiredValue))
        {
            return isRequiredValue;
        }

        return base.IsRequiredByPolicy(member);
    }

    protected override bool? IsRequiredByPolicy(IFieldSymbol member)
    {
        if (member.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData fieldAttribute && fieldAttribute.TryGetNamedArgument("IsRequired", out bool isRequiredValue))
        {
            return isRequiredValue;
        }

        return base.IsRequiredByPolicy(member);
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

    protected override IEnumerable<ResolvedMethodSymbol> ResolveMethods(ITypeSymbol type, BindingFlags bindingFlags)
    {
        foreach ((IMethodSymbol method, bool isAmbiguous) in type.GetAllMethods())
        {
            if (IncludeMethod(method, out string? customName))
            {
                yield return new() { CustomName = customName, MethodSymbol = method, IsDiamondAmbiguous = isAmbiguous };
            }
        }

        bool IncludeMethod(IMethodSymbol method, out string? customName)
        {
            customName = null;

            if (method.MethodKind is not MethodKind.Ordinary ||
                !SyntaxFacts.IsValidIdentifier(method.Name) ||
                method.IsImplicitlyDeclared ||
                method.HasAttribute(KnownSymbols.CompilerGeneratedAttribute))
            {
                return false; // Skip methods that are special names (getters, setters, events) or compiler-generated.
            }

            if (ParseMethodShapeAttribute(method, out customName, out bool? ignore))
            {
                return ignore is null or false; // Skip methods explicitly marked as ignored.
            }

            if (method.DeclaredAccessibility is not Accessibility.Public || method.MethodKind is not MethodKind.Ordinary)
            {
                return false; // Skip methods that are not public when not annotated.
            }

            if (SymbolEqualityComparer.Default.Equals(method.ContainingType, KnownSymbols.Compilation.ObjectType) ||
                SymbolEqualityComparer.Default.Equals(method.ContainingType, KnownSymbols.SystemValueType))
            {
                return false; // Skip GetHashCode, ToString, Equals, and other object methods.
            }

            BindingFlags requiredFlags = method.IsStatic ? BindingFlags.Public | BindingFlags.Static : BindingFlags.Public | BindingFlags.Instance;
            if ((bindingFlags & requiredFlags) == 0)
            {
                return false; // Skip methods that are not included in the shape by default.
            }

            return true;
        }

        bool ParseMethodShapeAttribute(IMethodSymbol method, out string? name, out bool? ignore)
        {
            name = null;
            ignore = null;
            if (method.GetAttribute(_knownSymbols.MethodShapeAttribute) is not AttributeData methodShapeAttr)
            {
                return false;
            }

            foreach (KeyValuePair<string, TypedConstant> namedArgument in methodShapeAttr.NamedArguments)
            {
                switch (namedArgument.Key)
                {
                    case "Name":
                        name = (string)namedArgument.Value.Value!;
                        break;
                    case "Ignore":
                        ignore = (bool)namedArgument.Value.Value!;
                        break;
                }
            }

            return true;
        }
    }

    protected override TypeDataModelGenerationStatus MapMethod(ResolvedMethodSymbol resolvedMethod, ref TypeDataModelGenerationContext ctx, out MethodDataModel result)
    {
        if (resolvedMethod.MethodSymbol is { IsGenericMethod: true, IsDefinition: true } method)
        {
            ReportDiagnostic(GenericMethodShapesNotSupported, method.Locations.FirstOrDefault(), [method.ToDisplayString()]);
            result = default;
            return TypeDataModelGenerationStatus.UnsupportedType;
        }

        TypeDataModelGenerationStatus status = base.MapMethod(resolvedMethod, ref ctx, out result);
        if (status is not TypeDataModelGenerationStatus.Success)
        {
            ReportDiagnostic(MethodParametersNotSupported, resolvedMethod.MethodSymbol.Locations.FirstOrDefault(), [resolvedMethod.MethodSymbol.ToDisplayString()]);
            return status;
        }

        if (result.ReturnedValueType is null)
        {
            // Include the unit type if the method does not return a value.
            status = IncludeNestedType(_knownSymbols.UnitType!, ref ctx);
        }

        return status;
    }

    protected override IEnumerable<DerivedTypeModel> ResolveDerivedTypes(ITypeSymbol type)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Interface))
        {
            yield break;
        }

        int i = 0;
        ITypeSymbol[]? baseTypeArgs = null;
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

            if (derivedType is INamedTypeSymbol { IsUnboundGenericType: true } namedDerivedType)
            {
                baseTypeArgs ??= ((INamedTypeSymbol)type).GetRecursiveTypeArguments();
                INamedTypeSymbol? specializedDerivedType = namedDerivedType.OriginalDefinition.ConstructRecursive(baseTypeArgs);
                if (specializedDerivedType is null || !type.IsAssignableFrom(specializedDerivedType))
                {
                    ReportDiagnostic(DerivedTypeUnsupportedGenerics, attribute.GetLocation(), derivedType.ToDisplayString(), type.ToDisplayString());
                    continue;
                }

                derivedType = specializedDerivedType;
            }
            else if (!type.IsAssignableFrom(derivedType))
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

    protected override TypeDataModelGenerationStatus MapType(ITypeSymbol type, TypeDataKind? requestedKind, BindingFlags? methodBindingFlags, ImmutableArray<AssociatedTypeModel> associatedTypes, ref TypeDataModelGenerationContext ctx, TypeShapeRequirements requirements, out TypeDataModel? model)
    {
        Debug.Assert(requestedKind is null);

        ParseTypeShapeAttribute(type,
            out TypeShapeKind? attrDeclaredKind,
            out ITypeSymbol? marshaler,
            out MethodShapeFlags? attrMethodBindingFlags,
            out Location? typeShapeLocation);

        TypeExtensionModel? typeExtensionModel = GetExtensionModel(type);
        if (typeExtensionModel is not null)
        {
            // Merge shape configuration with any extension model that targets the type.
            attrDeclaredKind ??= typeExtensionModel.Kind;
            marshaler ??= typeExtensionModel.Marshaler;
            attrMethodBindingFlags ??= typeExtensionModel.IncludeMethods;
        }

        ParseCustomAssociatedTypeAttributes(type, out ImmutableArray<AssociatedTypeModel> customAssociatedTypes);
        ImmutableArray<AssociatedTypeModel> associatedTypeShapes = ParseAssociatedTypeShapeAttributes(type);
        ImmutableArray<AssociatedTypeModel> typeExtensionAssociatedTypes = typeExtensionModel?.AssociatedTypes ?? ImmutableArray<AssociatedTypeModel>.Empty;

        // Aggregate the associated types from all sources, and aggregate flags specified for the same type.
        associatedTypes =
            associatedTypes.Concat(associatedTypeShapes).Concat(customAssociatedTypes).Concat(typeExtensionAssociatedTypes)
            .GroupBy(at => at.AssociatedType, SymbolEqualityComparer.Default)
            .Select(g => g.Aggregate((left, right) => new AssociatedTypeModel(left.AssociatedType, left.AssociatingAssembly, left.Location, left.Requirements | right.Requirements)))
            .ToImmutableArray();

        methodBindingFlags ??= MapMethodShapeFlagsToBindingFlags(attrMethodBindingFlags);
        if (marshaler is not null || attrDeclaredKind is TypeShapeKind.Surrogate)
        {
            return MapSurrogateType(type, marshaler, associatedTypes, ref ctx, requirements, methodBindingFlags, out model);
        }

        if (_knownSymbols.ResolveFSharpUnionMetadata(type) is FSharpUnionInfo unionInfo)
        {
            return unionInfo switch
            {
                FSharpOptionInfo optionInfo => MapFSharpOptionDataModel(optionInfo, ref ctx, requirements, methodBindingFlags, out model),
                _ => MapFSharpUnionDataModel((GenericFSharpUnionInfo)unionInfo, ref ctx, methodBindingFlags, out model),
            };
        }

        requestedKind = MapTypeShapeKindToDataKind(attrDeclaredKind);
        TypeDataModelGenerationStatus status = base.MapType(type, requestedKind, methodBindingFlags, associatedTypes, ref ctx, requirements, out model);

        if (requestedKind is not null && model is { Kind: TypeDataKind actualKind } && requestedKind != actualKind)
        {
            ReportDiagnostic(InvalidTypeShapeKind, typeShapeLocation, requestedKind.Value, type.ToDisplayString());
        }

        return status;
    }

    private TypeDataModelGenerationStatus MapSurrogateType(ITypeSymbol type, ITypeSymbol? marshaler, ImmutableArray<AssociatedTypeModel> associatedTypes, ref TypeDataModelGenerationContext ctx, TypeShapeRequirements requirements, BindingFlags? methodFlags, out TypeDataModel? model)
    {
        model = null;

        if (marshaler is not INamedTypeSymbol namedMarshaler)
        {
            return ReportInvalidMarshalerAndExit();
        }

        if (namedMarshaler.IsUnboundGenericType)
        {
            // If the marshaler type is an unbound generic,
            // apply type arguments from the declaring type.
            ITypeSymbol[] typeArgs = ((INamedTypeSymbol)type).GetRecursiveTypeArguments();
            INamedTypeSymbol? specializedMarshaler = namedMarshaler.OriginalDefinition.ConstructRecursive(typeArgs);
            if (specializedMarshaler is null)
            {
                return ReportInvalidMarshalerAndExit();
            }

            namedMarshaler = specializedMarshaler;
        }

        IMethodSymbol? defaultCtor = namedMarshaler.GetMembers()
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => method is { MethodKind: MethodKind.Constructor, IsStatic: false, Parameters: [] });

        if (defaultCtor is null || !IsAccessibleSymbol(defaultCtor))
        {
            return ReportInvalidMarshalerAndExit();
        }

        // Check that the surrogate marshaler implements exactly one IMarshaler<,> for the source type.
        ITypeSymbol? surrogateType = null;
        foreach (INamedTypeSymbol interfaceType in namedMarshaler.AllInterfaces)
        {
            if (interfaceType.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(interfaceType.OriginalDefinition, _knownSymbols.MarshalerType))
            {
                var typeArgs = interfaceType.TypeArguments;
                if (SymbolEqualityComparer.Default.Equals(typeArgs[0], type))
                {
                    if (surrogateType is not null)
                    {
                        // We have conflicting implementations.
                        return ReportInvalidMarshalerAndExit();
                    }

                    surrogateType = typeArgs[1];
                }
            }
        }

        if (surrogateType is null)
        {
            return ReportInvalidMarshalerAndExit();
        }

        // Generate the shape for the surrogate type.
        TypeDataModelGenerationStatus status = IncludeNestedType(surrogateType, ref ctx, requirements);
        if (status is TypeDataModelGenerationStatus.Success)
        {
            model = new SurrogateTypeDataModel
            {
                Type = type,
                Requirements = TypeShapeRequirements.Full,
                SurrogateType = surrogateType,
                MarshalerType = namedMarshaler,
                Methods = MapMethods(type, ref ctx, methodFlags),
            };
        }

        return status;

        TypeDataModelGenerationStatus ReportInvalidMarshalerAndExit()
        {
            ReportDiagnostic(InvalidMarshaler, type.Locations.FirstOrDefault(), type.ToDisplayString());
            return TypeDataModelGenerationStatus.UnsupportedType;
        }
    }

    private TypeDataModelGenerationStatus MapFSharpOptionDataModel(FSharpOptionInfo optionInfo, ref TypeDataModelGenerationContext ctx, TypeShapeRequirements requirements, BindingFlags? methodFlags, out TypeDataModel? model)
    {
        TypeDataModelGenerationStatus status = IncludeNestedType(optionInfo.ElementType, ref ctx, requirements);
        if (status is not TypeDataModelGenerationStatus.Success)
        {
            model = null;
            return status;
        }

        model = new OptionalDataModel
        {
            Type = optionInfo.Type,
            Requirements = TypeShapeRequirements.Full,
            ElementType = optionInfo.ElementType,
            Methods = MapMethods(optionInfo.Type, ref ctx, methodFlags),
        };

        return status;
    }

    private TypeDataModelGenerationStatus MapFSharpUnionDataModel(GenericFSharpUnionInfo unionInfo, ref TypeDataModelGenerationContext ctx, BindingFlags? methodFlags, out TypeDataModel? model)
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
            Requirements = TypeShapeRequirements.Full,
            UnionCases = unionCaseModels.ToImmutableArray(),
            Methods = MapMethods(unionInfo.Type, ref ctx, methodFlags),
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
                .Select(p => new ParameterDataModel { Parameter = p })
                .ToImmutableArray(),
            MemberInitializers = ImmutableArray<PropertyDataModel>.Empty,
        };

        model = new ObjectDataModel
        {
            Type = unionCaseInfo.DeclaringType,
            Requirements = TypeShapeRequirements.Full,
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
                SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, _knownSymbols.GenerateShapeForAttribute) &&
                attributeData.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: ITypeSymbol nonGenericTypeArgument }])
            {
                typeToInclude = nonGenericTypeArgument;
                typeIdToInclude = CreateTypeId(nonGenericTypeArgument);
                isWitnessTypeDeclaration = true;
            }
            else if (
                attributeData.AttributeClass is { TypeArguments: [ITypeSymbol typeArgument] } &&
                SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass.ConstructedFrom, _knownSymbols.GenerateShapeForAttributeOfT))
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
            SourceFilenamePrefix = context.TypeSymbol.ToDisplayString(Helpers.RoslynHelpers.QualifiedNameOnlyFormat),
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
            entry.Value.AssociatedTypes = AssociatedTypes.GetValueOrDefault(entry.Key, ImmutableArray<AssociatedTypeModel>.Empty);

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

    private static AssociatedTypeId CreateAssociatedTypeId(INamedTypeSymbol open, INamedTypeSymbol closed)
    {
        StringBuilder builder = new();

        ConstructStableName(open, builder);
        string openName = builder.ToString();

        string closedName;
        if (SymbolEqualityComparer.Default.Equals(open, closed))
        {
            closedName = openName;
        }
        else
        {
            builder.Clear();
            ConstructStableName(closed, builder);
            closedName = builder.ToString();
        }

        return new AssociatedTypeId(openName, closedName, closed.GetFullyQualifiedName(), CreateTypeId(closed));

        static void ConstructStableName(ITypeSymbol symbol, StringBuilder builder)
        {
            // The algorithm here must be kept in sync with SourceGenObjectTypeShape<T>.GetAssociatedTypeFactory.
            if (symbol.ContainingType is not null)
            {
                ConstructStableName(symbol.ContainingType, builder);
                builder.Append('+');
            }
            else if (symbol.ContainingNamespace is { IsGlobalNamespace: false })
            {
                ConstructStableNamespaceName(symbol.ContainingNamespace, builder);
                builder.Append('.');
            }

            if (symbol is INamedTypeSymbol { IsGenericType: true } genericSymbol)
            {
                builder.Append(symbol.OriginalDefinition.Name);
                builder.Append('<');
                if (genericSymbol.IsUnboundGenericType)
                {
                    builder.Append(',', genericSymbol.TypeParameters.Length - 1);
                }
                else
                {
                    for (int i = 0; i < genericSymbol.TypeArguments.Length; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(',');
                        }

                        ConstructStableName(genericSymbol.TypeArguments[i], builder);
                    }
                }

                builder.Append('>');
            }
            else
            {
                builder.Append(symbol.Name);
            }
        }

        static void ConstructStableNamespaceName(INamespaceSymbol ns, StringBuilder builder)
        {
            if (ns.ContainingNamespace is { IsGlobalNamespace: false })
            {
                ConstructStableNamespaceName(ns.ContainingNamespace, builder);
                builder.Append('.');
            }

            builder.Append(ns.Name);
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
            return ns.ToDisplayString(Helpers.RoslynHelpers.QualifiedNameOnlyFormat);
        }

        return null;
    }

    private static TypeDeclarationModel CreateShapeProviderDeclaration(Compilation compilation)
    {
        string typeName = !string.IsNullOrWhiteSpace(compilation.AssemblyName)
            ? "ShapeProvider_" + s_escapeAssemblyName.Replace(compilation.AssemblyName!, "_")
            : "ShapeProvider_AnonAssembly";

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

    private static readonly Regex s_escapeAssemblyName = new(@"\W+", RegexOptions.Compiled);
}
