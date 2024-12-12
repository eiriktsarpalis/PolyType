﻿using Microsoft.CodeAnalysis;
using PolyType.Abstractions;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;
using System.Diagnostics;

namespace PolyType.SourceGenerator;

public sealed partial class Parser
{
    private TypeShapeModel MapModel(TypeDataModel model, TypeId typeId, string sourceIdentifier)
    {
        return model switch
        {
            EnumDataModel enumModel => new EnumShapeModel
            {
                Type = typeId,
                SourceIdentifier = sourceIdentifier,
                UnderlyingType = CreateTypeId(enumModel.UnderlyingType),
            },

            NullableDataModel nullableModel => new NullableShapeModel
            {
                Type = typeId,
                SourceIdentifier = sourceIdentifier,
                ElementType = CreateTypeId(nullableModel.ElementType),
            },

            EnumerableDataModel enumerableModel => new EnumerableShapeModel
            {
                Type = typeId,
                SourceIdentifier = sourceIdentifier,
                ElementType = CreateTypeId(enumerableModel.ElementType),
                ConstructionStrategy = enumerableModel.ConstructionStrategy switch
                {
                    _ when enumerableModel.EnumerableKind is EnumerableKind.ArrayOfT or EnumerableKind.MemoryOfT or EnumerableKind.ReadOnlyMemoryOfT
                        => CollectionConstructionStrategy.Span, // use ReadOnlySpan.ToArray() to create the collection

                    CollectionModelConstructionStrategy.Mutable => CollectionConstructionStrategy.Mutable,
                    CollectionModelConstructionStrategy.Span => CollectionConstructionStrategy.Span,
                    CollectionModelConstructionStrategy.List => 
                        IsFactoryAcceptingIEnumerable(enumerableModel.FactoryMethod) 
                        ? CollectionConstructionStrategy.Enumerable 
                        : CollectionConstructionStrategy.Span,

                    _ => CollectionConstructionStrategy.None,
                },

                AddElementMethod = enumerableModel.AddElementMethod?.Name,
                ImplementationTypeFQN = 
                    enumerableModel.ConstructionStrategy is CollectionModelConstructionStrategy.Mutable &&
                    enumerableModel.FactoryMethod is { IsStatic: false, ContainingType: INamedTypeSymbol implType } && 
                    !SymbolEqualityComparer.Default.Equals(implType, enumerableModel.Type)

                    ? implType.GetFullyQualifiedName()
                    : null,

                StaticFactoryMethod = enumerableModel.FactoryMethod is { IsStatic: true } m ? m.GetFullyQualifiedName() : null,
                CtorRequiresListConversion = 
                    enumerableModel.ConstructionStrategy is CollectionModelConstructionStrategy.List &&
                    !IsFactoryAcceptingIEnumerable(enumerableModel.FactoryMethod),

                Kind = enumerableModel.EnumerableKind,
                Rank = enumerableModel.Rank,
                ElementTypeContainsNullableAnnotations = enumerableModel.ElementType.ContainsNullabilityAnnotations(),
            },

            DictionaryDataModel dictionaryModel => new DictionaryShapeModel
            {
                Type = typeId,
                SourceIdentifier = sourceIdentifier,
                KeyType = CreateTypeId(dictionaryModel.KeyType),
                ValueType = CreateTypeId(dictionaryModel.ValueType),
                ConstructionStrategy = dictionaryModel.ConstructionStrategy switch
                {
                    CollectionModelConstructionStrategy.Mutable => CollectionConstructionStrategy.Mutable,
                    CollectionModelConstructionStrategy.Span => CollectionConstructionStrategy.Span,
                    CollectionModelConstructionStrategy.Dictionary =>
                        IsFactoryAcceptingIEnumerable(dictionaryModel.FactoryMethod)
                        ? CollectionConstructionStrategy.Enumerable
                        : CollectionConstructionStrategy.Span,

                    CollectionModelConstructionStrategy.TupleEnumerable => CollectionConstructionStrategy.Enumerable,
                    _ => CollectionConstructionStrategy.None,
                },

                ImplementationTypeFQN =
                    dictionaryModel.ConstructionStrategy is CollectionModelConstructionStrategy.Mutable &&
                    dictionaryModel.FactoryMethod is { IsStatic: false, ContainingType: INamedTypeSymbol implType } &&
                    !SymbolEqualityComparer.Default.Equals(implType, dictionaryModel.Type)

                    ? implType.GetFullyQualifiedName()
                    : null,

                StaticFactoryMethod = dictionaryModel.FactoryMethod is { IsStatic: true } m ? m.GetFullyQualifiedName() : null,
                IsTupleEnumerableFactory = dictionaryModel.ConstructionStrategy is CollectionModelConstructionStrategy.TupleEnumerable,
                Kind = dictionaryModel.DictionaryKind,
                CtorRequiresDictionaryConversion =
                    dictionaryModel.ConstructionStrategy is CollectionModelConstructionStrategy.Dictionary &&
                    !IsFactoryAcceptingIEnumerable(dictionaryModel.FactoryMethod),
                KeyValueTypesContainNullableAnnotations = 
                    dictionaryModel.KeyType.ContainsNullabilityAnnotations() ||
                    dictionaryModel.ValueType.ContainsNullabilityAnnotations(),
            },

            ObjectDataModel objectModel => new ObjectShapeModel
            {
                Type = typeId,
                SourceIdentifier = sourceIdentifier,
                Constructor = objectModel.Constructors
                    .Select(c => MapConstructor(objectModel, typeId, c))
                    .FirstOrDefault(),

                Properties = objectModel.Properties
                    .Select(p => MapProperty(model.Type, typeId, p))
                    .OrderBy(p => p.Order)
                    .ToImmutableEquatableArray(),

                IsValueTupleType = false,
                IsTupleType = false,
                IsRecordType = model.Type.IsRecord,
            },

            TupleDataModel tupleModel => new ObjectShapeModel
            {
                Type = typeId,
                SourceIdentifier = sourceIdentifier,
                Constructor = MapTupleConstructor(typeId, tupleModel),
                Properties = tupleModel.Elements
                    .Select((e, i) => MapProperty(model.Type, typeId, e, tupleElementIndex: i, isClassTupleType: !tupleModel.IsValueTuple))
                    .ToImmutableEquatableArray(),

                IsValueTupleType = tupleModel.IsValueTuple,
                IsTupleType = true,
                IsRecordType = false,
            },

            _ => new ObjectShapeModel
            { 
                Type = typeId,
                SourceIdentifier = sourceIdentifier,
                Constructor = null,
                Properties = [],
                IsValueTupleType = false,
                IsTupleType = false,
                IsRecordType = false,
            }
        };

        static bool IsFactoryAcceptingIEnumerable(IMethodSymbol? method)
        {
            return method?.Parameters is [{ Type: INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } }];
        }
    }

    protected override TypeDataModelGenerationStatus MapType(ITypeSymbol type, TypeDataKind? requestedKind, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model)
    {
        Debug.Assert(requestedKind is null);
        ParseTypeShapeAttribute(type, out TypeShapeKind? requestedTypeShapeKind, out Location? location);
        requestedKind = MapTypeShapeKindToDataKind(requestedTypeShapeKind);

        TypeDataModelGenerationStatus status = base.MapType(type, requestedKind, ref ctx, out model);

        if (requestedKind is not null && model is { Kind: TypeDataKind actualKind } && requestedKind != actualKind)
        {
            ReportDiagnostic(InvalidTypeShapeKind, location, requestedKind.Value, type.ToDisplayString());
        }

        return status;
    }

    private PropertyShapeModel MapProperty(ITypeSymbol parentType, TypeId parentTypeId, PropertyDataModel property, bool isClassTupleType = false, int tupleElementIndex = -1)
    {
        ParsePropertyShapeAttribute(property.PropertySymbol, out string propertyName, out int order);

        bool emitGetter = property.IncludeGetter;
        bool emitSetter = property.IncludeSetter && !property.IsInitOnly;
        
        return new PropertyShapeModel
        {
            Name = isClassTupleType ? $"Item{tupleElementIndex + 1}" : propertyName ?? property.Name,
            UnderlyingMemberName = isClassTupleType
                ? $"{string.Join("", Enumerable.Repeat("Rest.", tupleElementIndex / 7))}Item{(tupleElementIndex % 7) + 1}"
                : property.Name,

            DeclaringType = SymbolEqualityComparer.Default.Equals(parentType, property.DeclaringType) ? parentTypeId : CreateTypeId(property.DeclaringType),
            CanUseUnsafeAccessors = _knownSymbols.TargetFramework switch
            {
                // .NET 8 or later supports unsafe accessors for properties of non-generic types.
                var target when target >= TargetFramework.Net80 => !property.DeclaringType.IsGenericType,
                _ => false
            },

            PropertyType = CreateTypeId(property.PropertyType),
            IsGetterNonNullable = emitGetter && property.IsGetterNonNullable,
            IsSetterNonNullable = emitSetter && property.IsSetterNonNullable,
            PropertyTypeContainsNullabilityAnnotations = property.PropertyType.ContainsNullabilityAnnotations(),
            EmitGetter = emitGetter,
            EmitSetter = emitSetter,
            IsGetterAccessible = property.IsGetterAccessible,
            IsSetterAccessible = property.IsSetterAccessible,
            IsGetterPublic = emitGetter && property.BaseSymbol is IPropertySymbol { GetMethod.DeclaredAccessibility: Accessibility.Public } or IFieldSymbol { DeclaredAccessibility: Accessibility.Public },
            IsSetterPublic = emitSetter && property.BaseSymbol is IPropertySymbol { SetMethod.DeclaredAccessibility: Accessibility.Public } or IFieldSymbol { DeclaredAccessibility: Accessibility.Public },
            IsInitOnly = property.IsInitOnly,
            IsField = property.IsField,
            Order = order,
        };
    }

    private ConstructorShapeModel MapConstructor(ObjectDataModel objectModel, TypeId declaringTypeId, ConstructorDataModel constructor)
    {
        int position = constructor.Parameters.Length;
        List<ConstructorParameterShapeModel>? requiredMembers = null;
        List<ConstructorParameterShapeModel>? optionalMembers = null;
        
        bool isAccessibleConstructor = IsAccessibleSymbol(constructor.Constructor);
        bool isParameterizedConstructor = position > 0 || constructor.MemberInitializers.Any(p => p.IsRequired || p.IsInitOnly);
        IEnumerable<PropertyDataModel> memberInitializers = isParameterizedConstructor
            // Include all settable members but process required members first.
            ? constructor.MemberInitializers.OrderByDescending(p => p.IsRequired)
            // Do not include any member initializers in parameterless constructors.
            : [];

        foreach (PropertyDataModel propertyModel in memberInitializers)
        {
            ParsePropertyShapeAttribute(propertyModel.PropertySymbol, out string propertyName, out _);

            var memberInitializer = new ConstructorParameterShapeModel
            {
                ParameterType = CreateTypeId(propertyModel.PropertyType),
                DeclaringType = SymbolEqualityComparer.Default.Equals(propertyModel.DeclaringType, objectModel.Type)
                    ? declaringTypeId
                    : CreateTypeId(propertyModel.DeclaringType),

                Name = propertyName,
                UnderlyingMemberName = propertyModel.Name,
                Position = position++,
                IsRequired = propertyModel.IsRequired,
                IsAccessible = propertyModel.IsSetterAccessible,
                CanUseUnsafeAccessors = _knownSymbols.TargetFramework switch
                {
                    // .NET 8 or later supports unsafe accessors for properties of non-generic types.
                    var target when target >= TargetFramework.Net80 => !propertyModel.DeclaringType.IsGenericType,
                    _ => false
                },
                IsInitOnlyProperty = propertyModel.IsInitOnly,
                Kind = propertyModel.IsRequired ? ParameterKind.RequiredMember : ParameterKind.OptionalMember,
                RefKind = RefKind.None,
                IsNonNullable = propertyModel.IsSetterNonNullable,
                ParameterTypeContainsNullabilityAnnotations = propertyModel.PropertyType.ContainsNullabilityAnnotations(),
                IsPublic = propertyModel.PropertySymbol.DeclaredAccessibility is Accessibility.Public,
                IsField = propertyModel.IsField,
                HasDefaultValue = false,
                DefaultValueExpr = null,
            };

            if (memberInitializer.Kind is ParameterKind.RequiredMember)
            {
                // Member must be set using an object initializer expression
                (requiredMembers ??= []).Add(memberInitializer);
            }
            else
            {
                // Member can be set optionally post construction
                (optionalMembers ??= []).Add(memberInitializer);
            }
        }

        return new ConstructorShapeModel
        {
            DeclaringType = SymbolEqualityComparer.Default.Equals(constructor.DeclaringType, objectModel.Type)
                ? declaringTypeId
                : CreateTypeId(constructor.DeclaringType),

            Parameters = constructor.Parameters.Select(p => MapConstructorParameter(objectModel, declaringTypeId, p)).ToImmutableEquatableArray(),
            RequiredMembers = requiredMembers?.ToImmutableEquatableArray() ?? [],
            OptionalMembers = optionalMembers?.ToImmutableEquatableArray() ?? [],
            OptionalMemberFlagsType = (optionalMembers?.Count ?? 0) switch
            {
                0 => OptionalMemberFlagsType.None,
                <= 8 => OptionalMemberFlagsType.Byte,
                <= 16 => OptionalMemberFlagsType.UShort,
                <= 32 => OptionalMemberFlagsType.UInt32,
                <= 64 => OptionalMemberFlagsType.ULong,
                _ => OptionalMemberFlagsType.BitArray,
            },

            StaticFactoryName = constructor.Constructor.IsStatic ? constructor.Constructor.GetFullyQualifiedName() : null,
            IsPublic = constructor.Constructor.DeclaredAccessibility is Accessibility.Public,
            CanUseUnsafeAccessors = _knownSymbols.TargetFramework switch 
            {
                // .NET 8 or later supports unsafe accessors for properties of non-generic types.
                var tfm when tfm >= TargetFramework.Net80 => !constructor.DeclaringType.IsGenericType,
                _ => false,
            },
            IsAccessible = isAccessibleConstructor,
        };
    }

    private ConstructorParameterShapeModel MapConstructorParameter(ObjectDataModel objectModel, TypeId declaringTypeId, ConstructorParameterDataModel parameter)
    {
        string name = parameter.Parameter.Name;

        AttributeData? parameterAttr = parameter.Parameter.GetAttribute(_knownSymbols.ParameterShapeAttribute);
        if (parameterAttr != null &&
            parameterAttr.TryGetNamedArgument("Name", out string? value) && value != null)
        {
            // Resolve the [ParameterShape] attribute name override
            name = value;
        }
        else
        {
            foreach (PropertyDataModel property in objectModel.Properties)
            {
                if (SymbolEqualityComparer.Default.Equals(property.PropertyType, parameter.Parameter.Type) &&
                    // Match property names to parameters up to Pascal/camel case conversion.
                    CommonHelpers.CamelCaseInvariantComparer.Instance.Equals(parameter.Parameter.Name, property.Name) &&
                    property.PropertySymbol.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData attributeData &&
                    attributeData.TryGetNamedArgument("Name", out string? result) && result != null)
                {
                    // We have a matching property with a name override, use it in the parameter as well.
                    name = result;
                }
            }
        }

        return new ConstructorParameterShapeModel
        {
            Name = name,
            UnderlyingMemberName = parameter.Parameter.Name,
            Position = parameter.Parameter.Ordinal,
            DeclaringType = declaringTypeId,
            ParameterType = CreateTypeId(parameter.Parameter.Type),
            Kind = ParameterKind.ConstructorParameter,
            RefKind = parameter.Parameter.RefKind,
            IsRequired = !parameter.HasDefaultValue,
            IsAccessible = true,
            CanUseUnsafeAccessors = false,
            IsInitOnlyProperty = false,
            IsNonNullable = parameter.IsNonNullable,
            ParameterTypeContainsNullabilityAnnotations = parameter.Parameter.Type.ContainsNullabilityAnnotations(),
            IsPublic = true,
            IsField = false,
            HasDefaultValue = parameter.HasDefaultValue,
            DefaultValueExpr = parameter.DefaultValueExpr,
        };
    }

    private static ConstructorShapeModel MapTupleConstructor(TypeId typeId, TupleDataModel tupleModel)
    {
        if (tupleModel.IsValueTuple)
        {
            // Return the default constructor for value tuples
            return new ConstructorShapeModel
            {
                DeclaringType = typeId,
                Parameters = [],
                RequiredMembers = [],
                OptionalMembers = [],
                OptionalMemberFlagsType = OptionalMemberFlagsType.None,
                StaticFactoryName = null,
                IsAccessible = true,
                CanUseUnsafeAccessors = false,
                IsPublic = true,
            };
        }
        else
        {
            // Return the parameterized constructor for object tuples
            return new ConstructorShapeModel
            {
                DeclaringType = typeId,
                Parameters = tupleModel.Elements.Select((p, i) => MapTupleConstructorParameter(typeId, p, i)).ToImmutableEquatableArray(),
                RequiredMembers = [],
                OptionalMembers = [],
                OptionalMemberFlagsType = OptionalMemberFlagsType.None,
                StaticFactoryName = null,
                IsAccessible = true,
                CanUseUnsafeAccessors = false,
                IsPublic = true,
            };   
        }

        static ConstructorParameterShapeModel MapTupleConstructorParameter(TypeId typeId, PropertyDataModel tupleElement, int position)
        {
            string name = $"Item{position + 1}";
            return new ConstructorParameterShapeModel
            {
                Name = name,
                UnderlyingMemberName = name,
                Position = position,
                ParameterType = CreateTypeId(tupleElement.PropertyType),
                DeclaringType = typeId,
                HasDefaultValue = false,
                Kind = ParameterKind.ConstructorParameter,
                RefKind = RefKind.None,
                IsRequired = true,
                IsAccessible = true,
                CanUseUnsafeAccessors = false,
                IsInitOnlyProperty = false,
                IsPublic = true,
                IsField = true,
                IsNonNullable = tupleElement.IsSetterNonNullable,
                ParameterTypeContainsNullabilityAnnotations = tupleElement.PropertyType.ContainsNullabilityAnnotations(),
                DefaultValueExpr = null,
            };
        }
    }

    private void ParseTypeShapeAttribute(ITypeSymbol typeSymbol, out TypeShapeKind? kind, out Location? location)
    {
        kind = null;
        location = null;

        if (typeSymbol.GetAttribute(_knownSymbols.TypeShapeAttribute) is AttributeData propertyAttr)
        {
            location = propertyAttr.GetLocation();
            foreach (KeyValuePair<string, TypedConstant> namedArgument in propertyAttr.NamedArguments)
            {
                switch (namedArgument.Key)
                {
                    case "Kind":
                        kind = (TypeShapeKind)namedArgument.Value.Value!;
                        break;
                }
            }
        }
    }

    private static TypeDataKind? MapTypeShapeKindToDataKind(TypeShapeKind? kind)
    {
        return kind switch
        {
            null => null,
            TypeShapeKind.Enum => TypeDataKind.Enum,
            TypeShapeKind.Nullable => TypeDataKind.Nullable,
            TypeShapeKind.Enumerable => TypeDataKind.Enumerable,
            TypeShapeKind.Dictionary => TypeDataKind.Dictionary,
            TypeShapeKind.Object => TypeDataKind.Object,
            _ => TypeDataKind.None,
        };
    }

    private void ParsePropertyShapeAttribute(ISymbol propertySymbol, out string propertyName, out int order)
    {
        propertyName = propertySymbol.Name;
        order = 0;

        if (propertySymbol.GetAttribute(_knownSymbols.PropertyShapeAttribute) is AttributeData propertyAttr)
        {
            foreach (KeyValuePair<string, TypedConstant> namedArgument in propertyAttr.NamedArguments)
            {
                switch (namedArgument.Key)
                {
                    case "Name":
                        propertyName = (string?)namedArgument.Value.Value ?? propertyName;
                        break;
                    case "Order":
                        order = (int)namedArgument.Value.Value!;
                        break;
                }
            }
        }
    }
}