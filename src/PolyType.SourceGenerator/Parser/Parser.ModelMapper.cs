using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PolyType.Roslyn;
using PolyType.Roslyn.Helpers;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.SourceGenerator;

public sealed partial class Parser
{
    private TypeShapeModel MapModel(TypeDataModel model, TypeId typeId, string sourceIdentifier)
    {
        TypeShapeModel incrementalModel = MapModelCore(model, typeId, sourceIdentifier);
        return model.DerivedTypes is [] ? incrementalModel : MapUnionModel(model, incrementalModel);
    }

    private TypeShapeModel MapModelCore(TypeDataModel model, TypeId typeId, string sourceIdentifier, bool isFSharpUnionCase = false)
    {
        ImmutableEquatableSet<AssociatedTypeId> associatedTypes = CollectAssociatedTypes(model);

        return model switch
        {
            EnumDataModel enumModel => new EnumShapeModel
            {
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                UnderlyingType = CreateTypeId(enumModel.UnderlyingType),
                AssociatedTypes = associatedTypes,
                Methods = [],
                Events = [],
                Members = enumModel.Members.ToImmutableEquatableDictionary(m => m.Key, m => EnumValueToString(m.Value)),
                Attributes = CollectAttributes(model.Type),
                IsFlags = enumModel.IsFlags,
            },

            OptionalDataModel optionalModel => new OptionalShapeModel
            {
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                Kind = optionalModel.Type switch
                {
                    { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } => OptionalKind.NullableOfT,
                    { Name: "FSharpOption" } => OptionalKind.FSharpOption,
                    { Name: "FSharpValueOption" } => OptionalKind.FSharpValueOption,
                    _ => throw new InvalidOperationException(),
                },
                SourceIdentifier = sourceIdentifier,
                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),
                ElementType = CreateTypeId(optionalModel.ElementType),
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
            },

            SurrogateTypeDataModel surrogateModel => new SurrogateShapeModel
            {
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                SurrogateType = CreateTypeId(surrogateModel.SurrogateType),
                MarshalerType = CreateTypeId(surrogateModel.MarshalerType),
                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
            },

            EnumerableDataModel enumerableModel => new EnumerableShapeModel
            {
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                ElementType = CreateTypeId(enumerableModel.ElementType),
                ConstructionStrategy = enumerableModel switch
                {
                    { EnumerableKind: EnumerableKind.ArrayOfT or EnumerableKind.MemoryOfT or EnumerableKind.ReadOnlyMemoryOfT or EnumerableKind.InlineArrayOfT } =>
                        CollectionConstructionStrategy.Parameterized, // use ReadOnlySpan.ToArray() to create the collection
                    { FactoryMethod: not null } =>
                        IsParameterizedConstructor(enumerableModel.FactorySignature)
                        ? CollectionConstructionStrategy.Parameterized
                        : CollectionConstructionStrategy.Mutable,

                    _ => CollectionConstructionStrategy.None,
                },

                AppendMethod = enumerableModel.AppendMethod?.Name,
                ImplementationTypeFQN =
                    enumerableModel.FactoryMethod is { IsStatic: false, ContainingType: INamedTypeSymbol implType } &&
                    !SymbolEqualityComparer.Default.Equals(implType, enumerableModel.Type)
                    ? implType.GetFullyQualifiedName()
                    : null,

                StaticFactoryMethod = enumerableModel.FactoryMethod is { IsStatic: true } m ? m.GetFullyQualifiedName() : null,
                ConstructorParameters = enumerableModel.FactorySignature.ToImmutableEquatableArray(),
                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),

                Kind = enumerableModel.EnumerableKind,
                Rank = enumerableModel.Rank,
                IsSetType = enumerableModel.IsSetType,
                ElementTypeContainsNullableAnnotations = enumerableModel.ElementType.ContainsNullabilityAnnotations(),
                InsertionMode = enumerableModel.InsertionMode,
                AppendMethodReturnsBoolean = enumerableModel.AppendMethod?.ReturnType.SpecialType is SpecialType.System_Boolean,
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
                Length = enumerableModel.Length,
            },

            DictionaryDataModel dictionaryModel => new DictionaryShapeModel
            {
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                KeyType = CreateTypeId(dictionaryModel.KeyType),
                ValueType = CreateTypeId(dictionaryModel.ValueType),
                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),
                ConstructionStrategy = dictionaryModel switch
                {
                    { FactoryMethod: not null } =>
                        IsParameterizedConstructor(dictionaryModel.FactorySignature)
                        ? CollectionConstructionStrategy.Parameterized
                        : CollectionConstructionStrategy.Mutable,

                    _ => CollectionConstructionStrategy.None,
                },

                ImplementationTypeFQN =
                    dictionaryModel.FactoryMethod is { IsStatic: false, ContainingType: INamedTypeSymbol implType } &&
                    !SymbolEqualityComparer.Default.Equals(implType, dictionaryModel.Type)

                    ? implType.GetFullyQualifiedName()
                    : null,

                StaticFactoryMethod = dictionaryModel.FactoryMethod is { IsStatic: true } m ? m.GetFullyQualifiedName() : null,
                ConstructorParameters = dictionaryModel.FactorySignature.ToImmutableEquatableArray(),
                Kind = dictionaryModel.DictionaryKind,
                KeyValueTypesContainNullableAnnotations =
                    dictionaryModel.KeyType.ContainsNullabilityAnnotations() ||
                    dictionaryModel.ValueType.ContainsNullabilityAnnotations(),
                AvailableInsertionModes = dictionaryModel.AvailableInsertionModes,
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
            },

            ObjectDataModel objectModel => new ObjectShapeModel
            {
                Requirements = objectModel.Requirements,
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                Constructor = objectModel.Constructors
                    .Select(c => MapConstructor(objectModel, typeId, c, isFSharpUnionCase))
                    .FirstOrDefault(),

                Properties = objectModel.Properties
                    .Select(p => MapProperty(model.Type, typeId, p))
                    .OrderBy(p => p.Order)
                    .ToImmutableEquatableArray(),

                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),
                IsValueTupleType = false,
                IsTupleType = false,
                IsRecordType = model.Type.IsRecord,
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
            },

            TupleDataModel tupleModel => new ObjectShapeModel
            {
                Requirements = TypeShapeRequirements.Full,
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                Constructor = MapTupleConstructor(typeId, tupleModel),
                Methods = MapMethods(model, typeId),
                Events = [],
                Properties = tupleModel.Elements
                    .Select((e, i) => MapProperty(model.Type, typeId, e, tupleElementIndex: i, isClassTupleType: !tupleModel.IsValueTuple))
                    .ToImmutableEquatableArray(),

                IsValueTupleType = tupleModel.IsValueTuple,
                IsTupleType = true,
                IsRecordType = false,
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
            },

            FSharpUnionDataModel unionModel => new FSharpUnionShapeModel
            {
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),
                TagReader = unionModel.TagReader switch
                {
                    { MethodKind: MethodKind.PropertyGet } tagReader => tagReader.AssociatedSymbol!.Name,
                    var tagReader => tagReader.GetFullyQualifiedName(),
                },

                TagReaderIsMethod = unionModel.TagReader.MethodKind is not MethodKind.PropertyGet,
                UnderlyingModel = new ObjectShapeModel
                {
                    Requirements = TypeShapeRequirements.Full,
                    ReflectionName = model.Type.GetReflectionToStringName(),
                    Type = typeId,
                    Constructor = null,
                    Methods = MapMethods(model, typeId),
                    Events = MapEvents(model, typeId),
                    Properties = [],
                    SourceIdentifier = sourceIdentifier + "__Underlying",
                    IsValueTupleType = false,
                    IsTupleType = false,
                    IsRecordType = false,
                    AssociatedTypes = associatedTypes,
                    Attributes = [],
                },
                UnionCases = unionModel.UnionCases
                    .Select(unionCase => new FSharpUnionCaseShapeModel(
                        Name: unionCase.Name,
                        Tag: unionCase.Tag,
                        TypeModel: MapModelCore(unionCase.Type, CreateTypeId(unionCase.Type.Type), $"{sourceIdentifier}__Case_{unionCase.Name}", isFSharpUnionCase: true)))
                    .ToImmutableEquatableArray(),
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
            },

            DelegateDataModel delegateModel => new FunctionShapeModel
            {
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),
                ReturnType = CreateTypeId(delegateModel.ReturnedValueType ?? _knownSymbols.UnitType!),
                ReturnTypeKind = delegateModel.ReturnTypeKind,
                UnderlyingReturnType = CreateTypeId(delegateModel.InvokeMethod.ReturnType),
                ReturnsByRef = delegateModel.InvokeMethod.ReturnsByRef || delegateModel.InvokeMethod.ReturnsByRefReadonly,
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
                IsFsharpFunc = false,
                Parameters = delegateModel.Parameters
                    .Select(p => MapParameter(declaringObjectForConstructor: null, typeId, p, false))
                    .ToImmutableEquatableArray(),

                ArgumentStateType = delegateModel.Parameters.Length switch
                {
                    0 => ArgumentStateType.EmptyArgumentState,
                    <= 64 => ArgumentStateType.SmallArgumentState,
                    _ => ArgumentStateType.LargeArgumentState,
                },
            },

            FSharpFunctionDataModel fsharpFunc => new FunctionShapeModel
            {
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),
                ReturnType = CreateTypeId(fsharpFunc.ReturnedValueType ?? _knownSymbols.UnitType!),
                ReturnTypeKind = fsharpFunc.ReturnTypeKind,
                UnderlyingReturnType = CreateTypeId(fsharpFunc.ReturnType),
                ReturnsByRef = false,
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
                IsFsharpFunc = true,
                Parameters = MapFSharpFunctionParameters(fsharpFunc, typeId, out int effectiveParameterCount),
                ArgumentStateType = effectiveParameterCount switch
                {
                    0 => ArgumentStateType.EmptyArgumentState,
                    <= 64 => ArgumentStateType.SmallArgumentState,
                    _ => ArgumentStateType.LargeArgumentState,
                },
            },

            FSharpUnitDataModel => new ObjectShapeModel
            {
                Requirements = TypeShapeRequirements.Full,
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),
                Constructor = MapFSharpUnitConstructor(typeId),
                Properties = [],
                IsValueTupleType = false,
                IsTupleType = false,
                IsRecordType = false,
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
            },

            _ => new ObjectShapeModel
            {
                Requirements = TypeShapeRequirements.Full,
                Type = typeId,
                ReflectionName = model.Type.GetReflectionToStringName(),
                SourceIdentifier = sourceIdentifier,
                Methods = MapMethods(model, typeId),
                Events = MapEvents(model, typeId),
                Constructor = null,
                Properties = [],
                IsValueTupleType = false,
                IsTupleType = false,
                IsRecordType = false,
                AssociatedTypes = associatedTypes,
                Attributes = CollectAttributes(model.Type),
            }
        };
    }

    private TypeExtensionModel? GetExtensionModel(ITypeSymbol type)
    {
        _typeShapeExtensions.TryGetValue(type, out TypeExtensionModel? exactMatch);

        TypeExtensionModel? genericTypeMatch = null;
        if (type is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            _typeShapeExtensions.TryGetValue(namedType.ConstructUnboundGenericType(), out genericTypeMatch);
        }

        return TypeExtensionModel.Combine(primary: exactMatch, secondary: genericTypeMatch);
    }

    private ImmutableEquatableSet<AssociatedTypeId> CollectAssociatedTypes(TypeDataModel model)
    {
        var associatedTypeSymbols = model.AssociatedTypes;

        var namedType = model.Type as INamedTypeSymbol;
        if (GetExtensionModel(model.Type) is { } extensionModel)
        {
            associatedTypeSymbols = associatedTypeSymbols.AddRange(extensionModel.AssociatedTypes);
        }

        Dictionary<AssociatedTypeId, TypeShapeRequirements> associatedTypesBuilder = new();
        ITypeSymbol[] typeArgs = namedType?.GetRecursiveTypeArguments() ?? [];
        foreach ((INamedTypeSymbol openType, IAssemblySymbol associatedAssembly, Location? location, TypeShapeRequirements requirements) in associatedTypeSymbols)
        {
            if (!IsAccessibleSymbol(openType))
            {
                // Skip types that are not accessible in the current scope
                ReportDiagnostic(TypeNotAccessible, location, openType.ToDisplayString());
                continue;
            }

            if (!openType.IsUnboundGenericType)
            {
                var typeId = CreateAssociatedTypeId(openType, openType);
                AddOrMerge(typeId, requirements);
            }
            else
            {
                if (openType.OriginalDefinition.ConstructRecursive(typeArgs) is INamedTypeSymbol closedType)
                {
                    var typeId = CreateAssociatedTypeId(openType, closedType);
                    AddOrMerge(typeId, requirements);
                }
                else if (openType.Arity != typeArgs.Length)
                {
                    ReportDiagnostic(AssociatedTypeArityMismatch, location, openType.GetFullyQualifiedName(), openType.Arity, typeArgs.Length);
                    continue;
                }
            }

            void AddOrMerge(AssociatedTypeId typeId, TypeShapeRequirements newRequirements)
            {
                TypeShapeRequirements existingRequirements = associatedTypesBuilder.TryGetValue(typeId, out TypeShapeRequirements rs) ? rs : TypeShapeRequirements.None;
                associatedTypesBuilder[typeId] = existingRequirements | newRequirements;
            }
        }

        return associatedTypesBuilder.Keys.ToImmutableEquatableSet();
    }

    private UnionShapeModel MapUnionModel(TypeDataModel model, TypeShapeModel underlyingIncrementalModel)
    {
        Debug.Assert(model.DerivedTypes.Length > 0);

        return new UnionShapeModel
        {
            Type = underlyingIncrementalModel.Type,
            ReflectionName = model.Type.GetReflectionToStringName(),
            SourceIdentifier = underlyingIncrementalModel.SourceIdentifier,
            UnderlyingModel = underlyingIncrementalModel with
            {
                SourceIdentifier = underlyingIncrementalModel.SourceIdentifier + "__Underlying",
            },

            Attributes = CollectAttributes(model.Type),
            Methods = MapMethods(model, underlyingIncrementalModel.Type),
            Events = MapEvents(model, underlyingIncrementalModel.Type),
            UnionCases = model.DerivedTypes
                .Select(derived => new UnionCaseModel
                {
                    Type = CreateTypeId(derived.Type),
                    Name = derived.Name,
                    Tag = derived.Tag,
                    IsTagSpecified = derived.IsTagSpecified,
                    Index = derived.Index,
                    IsBaseType = derived.IsBaseType,
                })
                .ToImmutableEquatableArray(),
            AssociatedTypes = [],
        };
    }

    private PropertyShapeModel MapProperty(ITypeSymbol parentType, TypeId parentTypeId, PropertyDataModel property, bool isClassTupleType = false, int tupleElementIndex = -1)
    {
        bool emitGetter = property.IncludeGetter;
        bool emitSetter = property.IncludeSetter && !property.IsInitOnly;

        return new PropertyShapeModel
        {
            Name = isClassTupleType ? $"Item{tupleElementIndex + 1}" : property.LogicalName ?? property.Name,
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
            IsRequiredBySyntax = property.IsRequiredBySyntax,
            IsRequiredByPolicy = property.IsRequiredByPolicy,
            RequiresDisambiguation = property.IsAmbiguous,
            IsField = property.IsField,
            Order = property.Order,
            Attributes = CollectAttributes(property.PropertySymbol),
        };
    }

    private ConstructorShapeModel MapConstructor(ObjectDataModel objectModel, TypeId declaringTypeId, ConstructorDataModel constructor, bool isFSharpUnionCase)
    {
        int position = constructor.Parameters.Length;
        List<ParameterShapeModel>? requiredMembers = null;
        List<ParameterShapeModel>? optionalMembers = null;

        bool isAccessibleConstructor = IsAccessibleSymbol(constructor.Constructor);
        bool isParameterizedConstructor = position > 0 || constructor.MemberInitializers.Any(p => p.IsRequiredBySyntax || p.IsRequiredByPolicy is true || p.IsInitOnly);
        IEnumerable<PropertyDataModel> memberInitializers = isParameterizedConstructor
            // Include all settable members but process required members first.
            ? constructor.MemberInitializers.OrderByDescending(p => p.IsRequiredBySyntax ? 2 : p.IsRequiredByPolicy is true ? 1 : 0)
            // Do not include any member initializers in parameterless constructors.
            : [];

        foreach (PropertyDataModel propertyModel in memberInitializers)
        {
            var memberInitializer = new ParameterShapeModel
            {
                ParameterType = CreateTypeId(propertyModel.PropertyType),
                DeclaringType = SymbolEqualityComparer.Default.Equals(propertyModel.DeclaringType, objectModel.Type)
                    ? declaringTypeId
                    : CreateTypeId(propertyModel.DeclaringType),

                Name = propertyModel.LogicalName ?? propertyModel.Name,
                UnderlyingMemberName = propertyModel.Name,
                Position = position++,
                IsRequired = propertyModel.IsRequiredByPolicy ?? propertyModel.IsRequiredBySyntax,
                IsAccessible = propertyModel.IsSetterAccessible,
                CanUseUnsafeAccessors = _knownSymbols.TargetFramework switch
                {
                    // .NET 8 or later supports unsafe accessors for properties of non-generic types.
                    var target when target >= TargetFramework.Net80 => !propertyModel.DeclaringType.IsGenericType,
                    _ => false
                },
                IsInitOnlyProperty = propertyModel.IsInitOnly,
                Kind = propertyModel.IsRequiredBySyntax || propertyModel.IsRequiredByPolicy is true ? ParameterKind.RequiredMember : ParameterKind.OptionalMember,
                RefKind = RefKind.None,
                IsNonNullable = propertyModel.IsSetterNonNullable,
                NullableAnnotation = propertyModel.PropertyType.NullableAnnotation,
                ParameterTypeContainsNullabilityAnnotations = propertyModel.PropertyType.ContainsNullabilityAnnotations(),
                IsPublic = propertyModel.PropertySymbol.DeclaredAccessibility is Accessibility.Public,
                IsField = propertyModel.IsField,
                HasDefaultValue = false,
                DefaultValueExpr = null,
                Attributes = CollectAttributes(propertyModel.PropertySymbol),
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

            Parameters = constructor.Parameters.Select(p => MapParameter(objectModel, declaringTypeId, p, isFSharpUnionCase)).ToImmutableEquatableArray(),
            RequiredMembers = requiredMembers?.ToImmutableEquatableArray() ?? [],
            OptionalMembers = optionalMembers?.ToImmutableEquatableArray() ?? [],
            ArgumentStateType = (constructor.Parameters.Length + (requiredMembers?.Count ?? 0) + (optionalMembers?.Count ?? 0)) switch
            {
                0 => ArgumentStateType.EmptyArgumentState,
                <= 64 => ArgumentStateType.SmallArgumentState,
                _ => ArgumentStateType.LargeArgumentState,
            },

            StaticFactoryName = constructor.Constructor switch
            {
                { IsStatic: true, MethodKind: MethodKind.PropertyGet } ctor => ctor.AssociatedSymbol!.GetFullyQualifiedName(),
                { IsStatic: true } ctor => ctor.GetFullyQualifiedName(),
                _ => null,
            },
            StaticFactoryIsProperty = constructor.Constructor.MethodKind is MethodKind.PropertyGet,
            ResultRequiresCast = !objectModel.Type.IsAssignableFrom(constructor.Constructor.GetReturnType()),
            IsPublic = constructor.Constructor.DeclaredAccessibility is Accessibility.Public,
            CanUseUnsafeAccessors = _knownSymbols.TargetFramework switch
            {
                // .NET 8 or later supports unsafe accessors for properties of non-generic types.
                var tfm when tfm >= TargetFramework.Net80 => !constructor.DeclaringType.IsGenericType,
                _ => false,
            },
            IsAccessible = isAccessibleConstructor,
            IsFSharpUnitConstructor = false,
            Attributes = CollectAttributes(constructor.Constructor),
        };
    }

    private ImmutableEquatableArray<MethodShapeModel> MapMethods(TypeDataModel typeModel, TypeId typeId)
    {
        return typeModel.Methods
            .Select((m, i) => new MethodShapeModel
            {
                Name = m.Name,
                UnderlyingMethodName = m.Method.Name,
                Position = i,
                IsPublic = m.Method.DeclaredAccessibility is Accessibility.Public,
                IsStatic = m.Method.IsStatic,
                ReturnTypeKind = m.ReturnTypeKind,
                DeclaringType = !SymbolEqualityComparer.Default.Equals(m.Method.ContainingType, typeModel.Type)
                    ? CreateTypeId(m.Method.ContainingType)
                    : typeId,

                UnderlyingReturnType = CreateTypeId(m.Method.ReturnType),
                ReturnType = CreateTypeId(m.ReturnedValueType ?? _knownSymbols.UnitType!),
                ReturnsByRef = m.Method.ReturnsByRef || m.Method.ReturnsByRefReadonly,
                RequiresDisambiguation = m.IsAmbiguous,
                Parameters = m.Parameters
                    .Select(p => MapParameter(declaringObjectForConstructor: null, CreateTypeId(m.Method.ContainingType), p, false))
                    .ToImmutableEquatableArray(),

                ArgumentStateType = m.Parameters.Length switch
                {
                    0 => ArgumentStateType.EmptyArgumentState,
                    <= 64 => ArgumentStateType.SmallArgumentState,
                    _ => ArgumentStateType.LargeArgumentState,
                },

                IsAccessible = IsAccessibleSymbol(m.Method),
                CanUseUnsafeAccessors = _knownSymbols.TargetFramework switch
                {
                    // .NET 8 or later supports unsafe accessors for methods of non-generic types.
                    // .NET 10 or later supports unsafe accessors for static methods cf. https://github.com/eiriktsarpalis/PolyType/issues/220
                    var target when target >= TargetFramework.Net80 => !m.Method.ContainingType.IsGenericType && !m.Method.IsStatic,
                    _ => false
                },

                Attributes = CollectAttributes(m.Method),
            })
            .ToImmutableEquatableArray();
    }

    private ImmutableEquatableArray<EventShapeModel> MapEvents(TypeDataModel typeModel, TypeId typeId)
    {
        return typeModel.Events
            .Select((e, i) => new EventShapeModel
            {
                Name = e.Name,
                UnderlyingMemberName = e.Event.Name,
                IsPublic = e.Event.DeclaredAccessibility is Accessibility.Public,
                IsStatic = e.Event.IsStatic,
                DeclaringType = !SymbolEqualityComparer.Default.Equals(e.Event.ContainingType, typeModel.Type)
                    ? CreateTypeId(e.Event.ContainingType)
                    : typeId,
                HandlerType = CreateTypeId(e.Event.Type),
                IsAccessible = IsAccessibleSymbol(e.Event),
                RequiresDisambiguation = e.IsAmbiguous,
                CanUseUnsafeAccessors = _knownSymbols.TargetFramework switch
                {
                    // .NET 8 or later supports unsafe accessors for events of non-generic types.
                    var target when target >= TargetFramework.Net80 => !e.Event.ContainingType.IsGenericType && !e.Event.IsStatic,
                    _ => false
                },
                Attributes = CollectAttributes(e.Event),
            })
            .ToImmutableEquatableArray();
    }

    private ParameterShapeModel MapParameter(ObjectDataModel? declaringObjectForConstructor, TypeId declaringTypeId, ParameterDataModel parameter, bool isFSharpUnionCase)
    {
        string name = parameter.Parameter.Name;
        bool isRequired = !parameter.HasDefaultValue;

        AttributeData? parameterAttr = parameter.Parameter.GetAttribute(_knownSymbols.ParameterShapeAttribute);
        if (parameterAttr?.TryGetNamedArgument("IsRequired", out bool? isRequiredValue) is true && isRequiredValue is not null)
        {
            isRequired = isRequiredValue.Value;
        }

        if (parameterAttr != null &&
            parameterAttr.TryGetNamedArgument("Name", out string? value) && value != null)
        {
            // Resolve the [ParameterShape] attribute name override
            name = value;
        }
        else if (isFSharpUnionCase &&
                 name.StartsWith("_", StringComparison.Ordinal) &&
                 declaringObjectForConstructor?.Properties.All(p => p.Name != name) is true)
        {
            name = name[1..];
        }
        else if (declaringObjectForConstructor is not null)
        {
            foreach (PropertyDataModel property in declaringObjectForConstructor.Properties)
            {
                // Match property names to parameters up to Pascal/camel case conversion.
                if (SymbolEqualityComparer.Default.Equals(property.PropertyType, parameter.Parameter.Type) &&
                    CommonHelpers.CamelCaseInvariantComparer.Instance.Equals(parameter.Parameter.Name, property.Name))
                {
                    // We have a matching property, use its name in the parameter.
                    name = property.LogicalName ?? property.Name;
                    break;
                }
            }
        }

        return new ParameterShapeModel
        {
            Name = name,
            UnderlyingMemberName = parameter.Parameter.Name,
            Position = parameter.Parameter.Ordinal,
            DeclaringType = declaringTypeId,
            ParameterType = CreateTypeId(parameter.Parameter.Type),
            Kind = ParameterKind.MethodParameter,
            RefKind = parameter.Parameter.RefKind,
            IsRequired = isRequired,
            IsAccessible = true,
            CanUseUnsafeAccessors = false,
            IsInitOnlyProperty = false,
            IsNonNullable = parameter.IsNonNullable,
            NullableAnnotation = parameter.Parameter.NullableAnnotation,
            ParameterTypeContainsNullabilityAnnotations = parameter.Parameter.Type.ContainsNullabilityAnnotations(),
            IsPublic = true,
            IsField = false,
            HasDefaultValue = parameter.HasDefaultValue,
            DefaultValueExpr = parameter.DefaultValueExpr,
            Attributes = CollectAttributes(parameter.Parameter),
        };
    }

    private ImmutableEquatableArray<ParameterShapeModel> MapFSharpFunctionParameters(FSharpFunctionDataModel fsharpFunc, TypeId declaringTypeId, out int effectiveParameterCount)
    {
        Debug.Assert(fsharpFunc.Parameters.Length > 0);

        if (fsharpFunc.Parameters is [var singleParam] && SymbolEqualityComparer.Default.Equals(singleParam, _knownSymbols.FSharpUnitType))
        {
            effectiveParameterCount = 0;
            return [];
        }

        effectiveParameterCount = fsharpFunc.Parameters.Length;
        return fsharpFunc.Parameters
            .Select((ITypeSymbol argType, int index) =>
                new ParameterShapeModel
                {
                    Name = $"arg{index + 1}",
                    UnderlyingMemberName = $"arg{index + 1}",
                    Position = index,
                    DeclaringType = declaringTypeId,
                    ParameterType = CreateTypeId(argType),
                    Kind = ParameterKind.MethodParameter,
                    RefKind = RefKind.None,
                    IsRequired = true,
                    IsAccessible = true,
                    CanUseUnsafeAccessors = false,
                    IsInitOnlyProperty = false,
                    IsNonNullable = false,
                    NullableAnnotation = NullableAnnotation.NotAnnotated,
                    ParameterTypeContainsNullabilityAnnotations = false,
                    IsPublic = true,
                    IsField = false,
                    HasDefaultValue = false,
                    DefaultValueExpr = null,
                    Attributes = [], // F# func parameters don't have attributes
                })
            .ToImmutableEquatableArray();
    }

    private static string EnumValueToString(object underlyingValue)
        => underlyingValue switch
        {
            float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            _ => underlyingValue.ToString(),
        };

    private ConstructorShapeModel MapTupleConstructor(TypeId typeId, TupleDataModel tupleModel)
    {
        return new ConstructorShapeModel
        {
            DeclaringType = typeId,
            Parameters = tupleModel.Elements.Select((p, i) => MapTupleConstructorParameter(typeId, p, i)).ToImmutableEquatableArray(),
            RequiredMembers = [],
            OptionalMembers = [],
            ArgumentStateType = tupleModel.Elements.Length switch
            {
                0 => ArgumentStateType.EmptyArgumentState,
                <= 64 => ArgumentStateType.SmallArgumentState,
                _ => ArgumentStateType.LargeArgumentState,
            },
            StaticFactoryName = null,
            StaticFactoryIsProperty = false,
            ResultRequiresCast = false,
            IsAccessible = true,
            CanUseUnsafeAccessors = false,
            IsPublic = true,
            IsFSharpUnitConstructor = false,
            Attributes = [],
        };

        ParameterShapeModel MapTupleConstructorParameter(TypeId typeId, PropertyDataModel tupleElement, int position)
        {
            string name = $"Item{position + 1}";
            return new ParameterShapeModel
            {
                Name = name,
                UnderlyingMemberName = name,
                Position = position,
                ParameterType = CreateTypeId(tupleElement.PropertyType),
                DeclaringType = typeId,
                HasDefaultValue = false,
                Kind = ParameterKind.MethodParameter,
                RefKind = RefKind.None,
                IsRequired = true,
                IsAccessible = true,
                CanUseUnsafeAccessors = false,
                IsInitOnlyProperty = false,
                IsPublic = true,
                IsField = true,
                IsNonNullable = tupleElement.IsSetterNonNullable,
                NullableAnnotation = NullableAnnotation.NotAnnotated,
                ParameterTypeContainsNullabilityAnnotations = tupleElement.PropertyType.ContainsNullabilityAnnotations(),
                DefaultValueExpr = null,
                Attributes = [],
            };
        }
    }

    private static ConstructorShapeModel MapFSharpUnitConstructor(TypeId typeId)
    {
        return new ConstructorShapeModel
        {
            DeclaringType = typeId,
            Parameters = [],
            RequiredMembers = [],
            OptionalMembers = [],
            ArgumentStateType = ArgumentStateType.EmptyArgumentState,
            StaticFactoryName = null,
            StaticFactoryIsProperty = false,
            ResultRequiresCast = false,
            IsAccessible = true,
            CanUseUnsafeAccessors = false,
            IsPublic = true,
            IsFSharpUnitConstructor = true,
            Attributes = [],
        };
    }

    private record struct CustomAttributeAssociatedTypeProvider(ImmutableDictionary<string, TypeShapeRequirements> NamesAndRequirements);

    private readonly Dictionary<INamedTypeSymbol, CustomAttributeAssociatedTypeProvider> _customAttributes = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, (bool ShouldSkip, bool AllowMultiple, bool IsInherited)> _attributeMetadataCache = new(SymbolEqualityComparer.Default);

    protected override void ParseCustomAssociatedTypeAttributes(
        ISymbol symbol,
        out ImmutableArray<AssociatedTypeModel> associatedTypes)
    {
        associatedTypes = ImmutableArray<AssociatedTypeModel>.Empty;
        foreach (AttributeData att in symbol.GetAttributes())
        {
            if (att.AttributeClass is null)
            {
                continue;
            }

            // Is this attribute a custom one that provides associated types?
            if (!_customAttributes.TryGetValue(att.AttributeClass, out CustomAttributeAssociatedTypeProvider provider))
            {
                _customAttributes[att.AttributeClass] = provider = GetAttributeProviderData(att.AttributeClass);
            }

            if (provider.NamesAndRequirements.IsEmpty)
            {
                continue;
            }

            Location? location = att.GetLocation();
            foreach ((string name, TypeShapeRequirements requirements) in provider.NamesAndRequirements)
            {
                bool match = false;

                // First try to match the name to a parameter.
                for (int i = 0; i < att.AttributeConstructor?.Parameters.Length && !match; i++)
                {
                    if (att.AttributeConstructor.Parameters[i].Name == name)
                    {
                        match = true;
                        associatedTypes = associatedTypes.AddRange(ParseArgument(att.ConstructorArguments[i]));
                    }
                }

                // Now try to match the name to a named argument.
                if (!match)
                {
                    foreach (KeyValuePair<string, TypedConstant> namedArgument in att.NamedArguments)
                    {
                        if (namedArgument.Key == name)
                        {
                            associatedTypes = associatedTypes.AddRange(ParseArgument(namedArgument.Value));
                            break;
                        }
                    }
                }

                IEnumerable<AssociatedTypeModel> ParseArgument(TypedConstant arg)
                {
                    if (arg.Kind == TypedConstantKind.Array)
                    {
                        foreach (TypedConstant argValue in arg.Values)
                        {
                            if (argValue.Value is INamedTypeSymbol namedType)
                            {
                                yield return new AssociatedTypeModel(namedType, symbol.ContainingAssembly, location, requirements);
                            }
                        }
                    }
                    else if (arg.Value is INamedTypeSymbol namedType)
                    {
                        yield return new AssociatedTypeModel(namedType, symbol.ContainingAssembly, location, requirements);
                    }
                }
            }
        }

        CustomAttributeAssociatedTypeProvider GetAttributeProviderData(INamedTypeSymbol attributeClass)
        {
            var names = ImmutableDictionary.CreateBuilder<string, TypeShapeRequirements>();
            foreach (AttributeData att in attributeClass.GetAttributes())
            {
                if (att.AttributeClass is null)
                {
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(att.AttributeClass, _knownSymbols.AssociatedTypeAttributeAttribute))
                {
                    if (att.ConstructorArguments is [{ Value: string name }, { Value: int requirements }])
                    {
                        names.Add(name, (TypeShapeRequirements)requirements);
                    }
                }
            }

            return new CustomAttributeAssociatedTypeProvider(names.ToImmutable());
        }
    }

    protected override string GetEnumValueName(IFieldSymbol field)
    {
        if (field.GetAttribute(_knownSymbols.EnumMemberShapeAttribute, inherit: false) is AttributeData enumMemberShapeAttr)
        {
            if (enumMemberShapeAttr.TryGetNamedArgument(PolyTypeKnownSymbols.EnumMemberShapeAttributePropertyNames.Name, out string? name) && name is not null)
            {
                return name;
            }
        }

        if (field.GetAttribute(_knownSymbols.EnumMemberAttribute, inherit: false) is AttributeData enumMemberAttr)
        {
            if (enumMemberAttr.TryGetNamedArgument("Value", out string? name) && name is not null)
            {
                return name;
            }
        }

        return base.GetEnumValueName(field);
    }

    private void ParseTypeShapeAttribute(
        ITypeSymbol typeSymbol,
        out TypeShapeKind? kind,
        out ITypeSymbol? marshaler,
        out MethodShapeFlags? includeMethodFlags,
        out Location? location)
    {
        kind = null;
        marshaler = null;
        location = null;
        includeMethodFlags = null;

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
                    case "Marshaler":
                        marshaler = namedArgument.Value.Value as ITypeSymbol;
                        break;
                    case "IncludeMethods":
                        includeMethodFlags = (MethodShapeFlags)namedArgument.Value.Value!;
                        break;
                }
            }
        }
    }

    private ImmutableArray<AssociatedTypeModel> ParseAssociatedTypeShapeAttributes(ITypeSymbol typeSymbol)
    {
        ImmutableArray<AssociatedTypeModel>.Builder associatedTypes = ImmutableArray.CreateBuilder<AssociatedTypeModel>();
        foreach (AttributeData associatedTypeAttr in typeSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(associatedTypeAttr.AttributeClass, _knownSymbols.AssociatedTypeShapeAttribute))
            {
                Location? location = associatedTypeAttr.GetLocation();

                TypeShapeRequirements requirements = associatedTypeAttr.TryGetNamedArgument(PolyTypeKnownSymbols.AssociatedTypeShapeAttributePropertyNames.Requirements, out TypeShapeRequirements depthArg)
                    ? depthArg : TypeShapeRequirements.Full;
                if (associatedTypeAttr.ConstructorArguments is [{ Kind: TypedConstantKind.Array, Values: { } typeArgs }, ..])
                {
                    associatedTypes.AddRange(
                        from tc in typeArgs
                        where tc.Value is INamedTypeSymbol s
                        select new AssociatedTypeModel((INamedTypeSymbol)tc.Value!, typeSymbol.ContainingAssembly, location, requirements));
                }
            }
        }

        return associatedTypes.ToImmutable();
    }

    private static TypeDataKind? MapTypeShapeKindToDataKind(TypeShapeKind? kind)
    {
        Debug.Assert(kind is not TypeShapeKind.Surrogate);
        return kind switch
        {
            null => null,
            TypeShapeKind.Enum => TypeDataKind.Enum,
            TypeShapeKind.Optional => TypeDataKind.Optional,
            TypeShapeKind.Enumerable => TypeDataKind.Enumerable,
            TypeShapeKind.Dictionary => TypeDataKind.Dictionary,
            TypeShapeKind.Object => TypeDataKind.Object,
            _ => TypeDataKind.None,
        };
    }

    private static BindingFlags? MapMethodShapeFlagsToBindingFlags(MethodShapeFlags? flags)
    {
        if (flags is null)
        {
            return null;
        }

        BindingFlags bindingFlags = BindingFlags.Default;
        if ((flags.Value & MethodShapeFlags.PublicInstance) != 0)
        {
            bindingFlags |= BindingFlags.Public | BindingFlags.Instance;
        }

        if ((flags.Value & MethodShapeFlags.PublicStatic) != 0)
        {
            bindingFlags |= BindingFlags.Public | BindingFlags.Static;
        }

        return bindingFlags;
    }

    private static bool IsParameterizedConstructor(ImmutableArray<CollectionConstructorParameter> signature)
    {
        foreach (var param in signature)
        {
            switch (param)
            {
                case CollectionConstructorParameter.Span:
                case CollectionConstructorParameter.List:
                case CollectionConstructorParameter.HashSet:
                case CollectionConstructorParameter.Dictionary:
                case CollectionConstructorParameter.TupleEnumerable:
                    return true;
            }
        }

        return false;
    }

    private bool ParsePropertyShapeAttribute(ISymbol propertySymbol, out string propertyName, out int order, out bool ignore)
    {
        propertyName = propertySymbol.Name;
        order = 0;
        ignore = false;

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
                    case "Ignore":
                        ignore = (bool)namedArgument.Value.Value!;
                        break;
                }
            }

            return true;
        }

        if (propertySymbol.ContainingType.HasAttribute(_knownSymbols.DataContractAttribute))
        {
            if (propertySymbol.GetAttribute(_knownSymbols.DataMemberAttribute) is AttributeData dataMemberAttr)
            {
                foreach (KeyValuePair<string, TypedConstant> namedArgument in dataMemberAttr.NamedArguments)
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

                return true;
            }

            // In a [DataContract] type, ignore properties without [DataMember] attribute.
            ignore = true;
            return true;
        }

        if (propertySymbol.HasAttribute(_knownSymbols.IgnoreDataMemberAttribute))
        {
            // Ignore properties with [IgnoreDataMember] attribute.
            ignore = true;
            return true;
        }

        return false;
    }

    private ImmutableEquatableArray<AttributeDataModel> CollectAttributes(ISymbol symbol)
    {
        List<AttributeDataModel> attributes = [];
        HashSet<INamedTypeSymbol>? uniqueAttrs = null;
        string[] tokenBuffer = new string[8];

        foreach ((AttributeData attr, bool isInherited) in symbol.GetAllAttributes())
        {
            // Skip if attribute class is null or not accessible
            if (attr.AttributeClass is null || !IsAccessibleSymbol(attr.AttributeClass))
            {
                continue;
            }

            var attrMetadata = GetAttributeMetadata(attr.AttributeClass);
            if (attrMetadata.ShouldSkip)
            {
                continue; // filter skipped attributes
            }

            if (isInherited && !attrMetadata.IsInherited)
            {
                continue; // filter inherited attributes whose usage is not marked as inherited
            }

            if (!attrMetadata.AllowMultiple && !(uniqueAttrs ??= new(SymbolEqualityComparer.Default)).Add(attr.AttributeClass))
            {
                continue; // filter duplicate attributes when multiple usage is not allowed
            }

            // Format constructor arguments
            var ctorArgs = attr.ConstructorArguments
                .Select(arg => Helpers.RoslynHelpers.FormatAttributeConstant(_knownSymbols.Compilation, GenerationScope, arg))
                .ToImmutableEquatableArray();

            // Format named arguments
            var namedArgs = attr.NamedArguments
                .Select(kvp => (kvp.Key, Helpers.RoslynHelpers.FormatAttributeConstant(_knownSymbols.Compilation, GenerationScope, kvp.Value)))
                .ToImmutableEquatableArray();

            attributes.Add(new AttributeDataModel
            {
                AttributeType = CreateTypeId(attr.AttributeClass),
                ConstructorArguments = ctorArgs,
                NamedArguments = namedArgs,
                IsInherited = isInherited,
            });
        }

        return attributes.ToImmutableEquatableArray();

        (bool ShouldSkip, bool AllowMultiple, bool IsInherited) GetAttributeMetadata(INamedTypeSymbol attributeClass)
        {
            if (_attributeMetadataCache.TryGetValue(attributeClass, out var result))
            {
                return result;
            }

            GetAttributeUsage(attributeClass, out bool allowMultiple, out bool isInherited);
            result = (ShouldSkip: ShouldSkipAttribute(attributeClass), AllowMultiple: allowMultiple, IsInherited: isInherited);
            _attributeMetadataCache[attributeClass] = result;
            return result;

            void GetAttributeUsage(INamedTypeSymbol attributeClass, out bool allowMultiple, out bool isInherited)
            {
                // Set default settings
                allowMultiple = false;
                isInherited = true;

                var allAttributes = attributeClass.GetAllAttributes();
                for (int i = allAttributes.Count - 1; i >= 0; i--)
                {
                    // Use reverse traversal so that most derived attribute types are processed last
                    var (attr, _) = allAttributes[i];
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, _knownSymbols.AttributeUsageAttribute))
                    {
                        foreach (KeyValuePair<string, TypedConstant> namedArg in attr.NamedArguments)
                        {
                            switch (namedArg.Key)
                            {
                                case nameof(AttributeUsageAttribute.AllowMultiple):
                                    allowMultiple = (bool)namedArg.Value.Value!;
                                    break;
                                case nameof(AttributeUsageAttribute.Inherited):
                                    isInherited = (bool)namedArg.Value.Value!;
                                    break;
                            }
                        }
                    }
                }
            }

            bool ShouldSkipAttribute(INamedTypeSymbol attributeClass)
            {
                if (_knownSymbols.ConditionalAttribute is not null)
                {
                    foreach (AttributeData attr in attributeClass.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, _knownSymbols.ConditionalAttribute) &&
                            attr.ConstructorArguments is [{ Value: string condition }])
                        {
                            bool isConditionDefined = _knownSymbols.Compilation.SyntaxTrees.Any(tree => tree.Options is CSharpParseOptions { PreprocessorSymbolNames: var names } && names.Contains(condition));
                            return !isConditionDefined;
                        }
                    }
                }

                return GetQualifiedNameTokens() switch
                {
                    // Skip framework and compiler specific attributes
                    ["System", "CLSCompliantAttribute"] => true,
                    ["System", "Runtime", "CompilerServices", ..] => true,
                    ["System", "Runtime", "InteropServices", ..] => true,
                    ["System", "Diagnostics", ..] => true,
                    ["System", "Reflection", "DefaultMemberAttribute"] => true,
                    ["Microsoft", "FSharp", "Core", ..] => true,
                    _ => false,
                };
            }

            ReadOnlySpan<string> GetQualifiedNameTokens()
            {
                if (attributeClass.ContainingType is not null)
                {
                    // Ignore nested types since we're not filtering any here.
                    return [];
                }

                int index = 0;
                tokenBuffer[index++] = attributeClass.Name;

                for (INamespaceSymbol? nsToken = attributeClass.ContainingNamespace;
                    nsToken is { IsGlobalNamespace: false };
                    nsToken = nsToken.ContainingNamespace)
                {
                    if (index >= tokenBuffer.Length)
                    {
                        // We're definitely not filtering a namespace this deep.
                        return [];
                    }

                    tokenBuffer[index++] = nsToken.Name;
                }

                Span<string> tokens = tokenBuffer.AsSpan(0, index);
                tokens.Reverse();
                return tokens;
            }
        }
    }
}
