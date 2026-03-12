using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    /// <summary>
    /// Whether property or field resolution should be skipped for the given type.
    /// </summary>
    /// <remarks>
    /// Currently skipped for simple types, nullable types, and <see cref="MemberInfo"/>.
    /// </remarks>
    protected virtual bool SkipObjectMemberResolution(INamedTypeSymbol type)
    {
        return
            type.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface) ||
            KnownSymbols.IsSimpleType(type) ||
            type.SpecialType is SpecialType.System_Object or SpecialType.System_Nullable_T or SpecialType.System_Delegate or SpecialType.System_MulticastDelegate ||
            KnownSymbols.MemberInfoType.IsAssignableFrom(type) ||
            KnownSymbols.ExceptionType.IsAssignableFrom(type) ||
            KnownSymbols.TaskType.IsAssignableFrom(type) ||
            SymbolEqualityComparer.Default.Equals(type, KnownSymbols.ValueTaskType) ||
            (type.IsGenericType && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, KnownSymbols.ValueTaskType));
    }

    /// <summary>
    /// Resolves the constructor symbols that should be included for the given type.
    /// </summary>
    protected virtual IEnumerable<IMethodSymbol> ResolveConstructors(ITypeSymbol type, ImmutableArray<PropertyDataModel> properties)
    {
        if (type.IsAbstract || type.TypeKind is TypeKind.Interface)
        {
            return [];
        }

        IMethodSymbol[] foundConstructors = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ctor =>
                ctor is { IsStatic: false, MethodKind: MethodKind.Constructor } &&
                ctor.Parameters.All(p => IsSupportedType(p.Type)) &&
                IsAccessibleSymbol(ctor))
            // Skip the copy constructor for record types
            .Where(ctor => !(type.IsRecord && ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, type)))
            .ToArray();

        return foundConstructors
            // Only include the implicit constructor in structs if there are no other constructors
            .Where(ctor => !ctor.IsImplicitlyDeclared || foundConstructors.Length == 1);
    }

    /// <summary>
    /// Resolves the properties and fields that should be included for the given type.
    /// </summary>
    protected virtual IEnumerable<ResolvedPropertySymbol> ResolveProperties(ITypeSymbol type)
    {
        foreach ((ISymbol member, bool isAmbiguous) in type.ResolveVisibleMembers())
        {
            if (member is IPropertySymbol { IsStatic: false, Parameters: [] } property &&
                IncludeProperty(property, out string? customName, out int order, out bool includeGetter, out bool includeSetter))
            {
                yield return new()
                {
                    Symbol = property,
                    CustomName = customName,
                    IncludeGetter = includeGetter,
                    IncludeSetter = includeSetter,
                    Order = order,
                    IsAmbiguous = isAmbiguous,
                };
            }
            else if (
                member is IFieldSymbol { IsStatic: false, IsConst: false } field &&
                IncludeField(field, out customName, out order, out includeGetter, out includeSetter))
            {
                yield return new()
                {
                    Symbol = field,
                    CustomName = customName,
                    IncludeGetter = includeGetter,
                    IncludeSetter = includeSetter,
                    Order = order,
                    IsAmbiguous = isAmbiguous,
                };
            }
        }
    }

    /// <summary>
    /// Determines whether the given field should be included in the object data model.
    /// </summary>
    /// <remarks>Defaults to including public fields only.</remarks>
    protected virtual bool IncludeField(IFieldSymbol field, out string? customName, out int order, out bool includeGetter, out bool includeSetter)
    {
        customName = null;
        order = 0;

        if (field.DeclaredAccessibility is not Accessibility.Public)
        { 
            includeGetter = includeSetter = false;
            return false;
        }

        includeGetter = true;
        includeSetter = !field.IsReadOnly;
        return true;
    }

    /// <summary>
    /// Determines whether the given property should be included in the object data model.
    /// </summary>
    /// <remarks>Defaults to including public getters and setters only.</remarks>
    protected virtual bool IncludeProperty(IPropertySymbol property, out string? customName, out int order, out bool includeGetter, out bool includeSetter)
    {
        customName = null;
        order = 0;

        if (property.DeclaredAccessibility is not Accessibility.Public)
        {
            includeGetter = includeSetter = false;
            return false;
        }

        // Use the signature of the base property to determine shape and accessibility.
        property = property.GetBaseProperty();
        includeGetter = property.GetMethod is { } getter && IsAccessibleSymbol(getter);
        includeSetter = property.SetMethod is { } setter && IsAccessibleSymbol(setter);
        return true;
    }

    /// <summary>
    /// Determines whether a member should be considered required.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>A value indicating whether the member is required.</returns>
    protected virtual bool? IsRequiredByPolicy(IPropertySymbol member) => null;

    /// <inheritdoc cref="IsRequiredByPolicy(IPropertySymbol)"/>
    protected virtual bool? IsRequiredByPolicy(IFieldSymbol member) => null;

    private bool TryMapObject(
        ITypeSymbol type,
        ref TypeDataModelGenerationContext ctx,
        ImmutableArray<MethodDataModel> methodModels,
        ImmutableArray<EventDataModel> eventModels,
        TypeShapeRequirements requirements,
        out TypeDataModel? model,
        out TypeDataModelGenerationStatus status)
    {
        status = default;
        model = null;

        if (type is not INamedTypeSymbol namedType ||
            type.TypeKind is not (TypeKind.Struct or TypeKind.Class or TypeKind.Interface) ||
            SkipObjectMemberResolution(namedType))
        {
            // Objects must be named classes, structs, or interfaces.
            return false;
        }

        ImmutableArray<PropertyDataModel> properties = requirements.HasFlag(TypeShapeRequirements.Properties) ? MapProperties(namedType, ref ctx) : ImmutableArray<PropertyDataModel>.Empty;
        ImmutableArray<ConstructorDataModel> constructors = requirements.HasFlag(TypeShapeRequirements.Constructor) ? MapConstructors(namedType, properties, ref ctx) : ImmutableArray<ConstructorDataModel>.Empty;
        ImmutableArray<DerivedTypeModel> derivedTypes = IncludeDerivedTypes(type, ref ctx, requirements);

        model = new ObjectDataModel
        {
            Type = type,
            Requirements = requirements,
            Constructors = constructors,
            Properties = properties,
            Methods = methodModels,
            Events = eventModels,
            DerivedTypes = derivedTypes,
        };

        return true;
    }

    /// <summary>
    /// Includes the specified associated types to the type graph traversal.
    /// </summary>
    /// <param name="type">The type from which the associated types originate.</param>
    /// <param name="associatedTypes">The associated types to include.</param>
    /// <param name="ctx">The type graph traversal context.</param>
    protected void IncludeAssociatedShapes(ITypeSymbol type, ImmutableArray<AssociatedTypeModel> associatedTypes, ref TypeDataModelGenerationContext ctx)
    {
        _associatedTypes = _associatedTypes.SetItem(
            type,
            ImmutableArray.CreateRange(_associatedTypes.GetValueOrDefault(type, ImmutableArray<AssociatedTypeModel>.Empty).Concat(associatedTypes)));

        foreach (AssociatedTypeModel associatedType in associatedTypes)
        {
            INamedTypeSymbol? closedAssociatedType = associatedType.AssociatedType.IsUnboundGenericType
                ? associatedType.AssociatedType.OriginalDefinition.ConstructRecursive((type as INamedTypeSymbol)?.GetRecursiveTypeArguments() ?? [])
                : associatedType.AssociatedType;

            if (closedAssociatedType is null)
            {
                continue;
            }

            IncludeNestedType(closedAssociatedType, ref ctx, associatedType.Requirements);
        }
    }

    private ImmutableArray<PropertyDataModel> MapProperties(INamedTypeSymbol type, ref TypeDataModelGenerationContext ctx)
    {
        var properties = ImmutableArray.CreateBuilder<PropertyDataModel>();

        foreach (var resolvedProperty in ResolveProperties(type))
        {
            if (resolvedProperty.Symbol is IPropertySymbol property &&
                IncludeNestedType(property.Type, ref ctx) is TypeDataModelGenerationStatus.Success)
            {
                PropertyDataModel propertyModel = MapProperty(
                    property,
                    resolvedProperty.CustomName,
                    resolvedProperty.Order,
                    resolvedProperty.IncludeGetter,
                    resolvedProperty.IncludeSetter,
                    resolvedProperty.IsAmbiguous);

                properties.Add(propertyModel);

                ParseCustomAssociatedTypeAttributes(property, out ImmutableArray<AssociatedTypeModel> customAssociatedTypes);
                IncludeAssociatedShapes(property.Type, customAssociatedTypes, ref ctx);
            }
            else if (
                resolvedProperty.Symbol is IFieldSymbol field &&
                IncludeNestedType(field.Type, ref ctx) is TypeDataModelGenerationStatus.Success)
            {
                PropertyDataModel fieldModel = MapField(
                    field,
                    resolvedProperty.CustomName,
                    resolvedProperty.Order,
                    resolvedProperty.IncludeGetter,
                    resolvedProperty.IncludeSetter,
                    resolvedProperty.IsAmbiguous);

                properties.Add(fieldModel);

                ParseCustomAssociatedTypeAttributes(field, out ImmutableArray<AssociatedTypeModel> customAssociatedTypes);
                IncludeAssociatedShapes(field.Type, customAssociatedTypes, ref ctx);
            }
        }

        return properties.ToImmutableArray();
    }

    private PropertyDataModel MapProperty(IPropertySymbol property, string? customName, int order, bool includeGetter, bool includeSetter, bool isAmbiguous)
    {
        Debug.Assert(property is { IsStatic: false, IsIndexer: false });
        Debug.Assert(!includeGetter || property.GetBaseProperty().GetMethod is not null);
        Debug.Assert(!includeSetter || property.GetBaseProperty().SetMethod is not null);

        // Property symbol represents the most derived declaration in the current hierarchy.
        // Need to use the base symbol to determine the actual signature, but use the derived
        // symbol for attribute and nullability metadata resolution.
        IPropertySymbol baseProperty = property.GetBaseProperty();
        property.ResolveNullableAnnotation(out bool isGetterNonNullable, out bool isSetterNonNullable);

        return new PropertyDataModel(property)
        {
            LogicalName = customName,
            IncludeGetter = includeGetter,
            IncludeSetter = includeSetter,
            IsGetterAccessible = baseProperty.GetMethod is { } getter && IsAccessibleSymbol(getter),
            IsSetterAccessible = baseProperty.SetMethod is { } setter && IsAccessibleSymbol(setter),
            IsGetterNonNullable = isGetterNonNullable,
            IsSetterNonNullable = isSetterNonNullable,
            IsRequiredBySyntax = property.IsRequired(),
            IsRequiredByPolicy = IsRequiredByPolicy(property),
            IsAmbiguous = isAmbiguous,
            Order = order,
        };
    }

    private PropertyDataModel MapField(IFieldSymbol field, string? customName, int order, bool includeGetter, bool includeSetter, bool isAmbiguous)
    {
        Debug.Assert(!field.IsStatic);
        field.ResolveNullableAnnotation(out bool isGetterNonNullable, out bool isSetterNonNullable);
        bool isAccessible = IsAccessibleSymbol(field);
        return new PropertyDataModel(field)
        {
            LogicalName = customName,
            IncludeGetter = includeGetter,
            IncludeSetter = includeSetter,
            IsGetterAccessible = isAccessible && includeGetter,
            IsSetterAccessible = isAccessible && includeSetter,
            IsGetterNonNullable = isGetterNonNullable,
            IsSetterNonNullable = isSetterNonNullable,
            IsRequiredBySyntax = field.IsRequired(),
            IsRequiredByPolicy = IsRequiredByPolicy(field),
            IsAmbiguous = isAmbiguous,
            Order = order,
        };
    }

    private ImmutableArray<ConstructorDataModel> MapConstructors(INamedTypeSymbol type, ImmutableArray<PropertyDataModel> properties, ref TypeDataModelGenerationContext ctx)
    {
        List<ConstructorDataModel> results = [];
        foreach (IMethodSymbol constructor in ResolveConstructors(type, properties))
        {
            ConstructorDataModel? constructorModel = MapConstructor(constructor, properties, ref ctx);
            if (constructorModel is not null)
            {
                results.Add(constructorModel.Value);
            }
        }

        return results.ToImmutableArray();
    }

    private ConstructorDataModel? MapConstructor(IMethodSymbol constructor, ImmutableArray<PropertyDataModel> properties, ref TypeDataModelGenerationContext ctx)
    {
        Debug.Assert(constructor.MethodKind is MethodKind.Constructor || constructor.IsStatic);

        // Skip constructors with out parameters - they cannot be meaningfully invoked through the shape system
        if (constructor.Parameters.Any(p => p.RefKind is RefKind.Out))
        {
            return null;
        }

        var parameters = new List<ParameterDataModel>();
        TypeDataModelGenerationContext scopedCtx = ctx;
        foreach (IParameterSymbol parameter in constructor.Parameters)
        {
            if (IncludeNestedType(parameter.Type, ref scopedCtx) != TypeDataModelGenerationStatus.Success)
            {
                // Skip constructors with unsupported parameter types
                return null;
            }

            ParameterDataModel parameterModel = MapParameter(parameter);
            parameters.Add(parameterModel);
        }

        ctx = scopedCtx; // Commit constructor parameter resolution to parent context
        bool setsRequiredMembers = constructor.HasSetsRequiredMembersAttribute();
        List<PropertyDataModel>? memberInitializers = null;

        for (int i = 0; i < properties.Length; i++)
        {
            PropertyDataModel property = properties[i];

            if (!property.IncludeSetter && !property.IsInitOnly)
            {
                // We're only interested in settable properties.
                continue;
            }

            if (setsRequiredMembers && property.IsRequiredBySyntax)
            {
                // Disable 'IsRequired' flag for constructors setting required members.
                property = property with { IsRequiredBySyntax = false };
            }

            if (!property.IsRequiredBySyntax && MatchesConstructorParameter(property))
            {
                // Deduplicate any optional properties whose signature matches a constructor parameter.
                continue;
            }

            (memberInitializers ??= []).Add(property);

            bool MatchesConstructorParameter(PropertyDataModel settableProperty)
            {
                foreach (IParameterSymbol p in constructor.Parameters)
                {
                    if (SymbolEqualityComparer.Default.Equals(p.Type, settableProperty.PropertyType) &&
                        CommonHelpers.CamelCaseInvariantComparer.Instance.Equals(p.Name, settableProperty.Name))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        return new ConstructorDataModel
        {
            Constructor = constructor,
            Parameters = parameters.ToImmutableArray(),
            MemberInitializers = memberInitializers?.ToImmutableArray() ?? ImmutableArray<PropertyDataModel>.Empty,
        };
    }

    private static ParameterDataModel MapParameter(IParameterSymbol parameter)
    {
        return new ParameterDataModel
        {
            Parameter = parameter
        };
    }
}
