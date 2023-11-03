﻿using Microsoft.CodeAnalysis;
using System.Diagnostics;
using TypeShape.SourceGenerator.Helpers;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

public sealed partial class ModelGenerator
{
    private ImmutableEquatableArray<ConstructorModel> MapConstructors(TypeId typeId, ITypeSymbol type, ITypeSymbol[]? classTupleElements, ITypeSymbol? collectionInterface, bool disallowMemberResolution)
    {
        if (TryResolveFactoryMethod(type) is { } factoryMethod)
        {
            return [MapConstructor(type, typeId, factoryMethod)];
        }

        if (disallowMemberResolution || type.TypeKind is not (TypeKind.Struct or TypeKind.Class) || type.SpecialType is not SpecialType.None)
        {
            return [];
        }
        
        if (classTupleElements is not null)
        {
            return MapTupleConstructors(typeId, type, classTupleElements).ToImmutableEquatableArray();
        }

        if (type is INamedTypeSymbol namedType && namedType.IsTupleType)
        {
            return MapTupleConstructors(typeId, namedType, namedType.TupleElements.Select(e => e.Type)).ToImmutableEquatableArray();
        }

        ConstructorParameterModel[] requiredOrInitMembers = ResolvePropertyAndFieldSymbols(type)
            .Select(member => member is IPropertySymbol p ? MapPropertyInitializer(p) : MapFieldInitializer((IFieldSymbol)member))
            .Where(member => member != null)
            .ToArray()!;

        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(ctor =>
                ctor is { MethodKind: MethodKind.Constructor } &&
                ctor.Parameters.All(p => IsSupportedType(p.Type)) &&
                IsAccessibleFromGeneratedType(ctor))
            .Where(ctor => 
                // Skip the copy constructor for record types
                !(type.IsRecord && ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, type)))
            .Where(ctor =>
                // For collection types only emit the default & interface copy constructors
                collectionInterface is null ||
                ctor.Parameters.Length == 0 ||
                ctor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(ctor.Parameters[0].Type, collectionInterface))
            .Select(ctor => MapConstructor(type, typeId, ctor, requiredOrInitMembers))
            .ToImmutableEquatableArray();
    }

    private ConstructorModel MapConstructor(ITypeSymbol type, TypeId typeId, IMethodSymbol constructor, ConstructorParameterModel[]? requiredOrInitOnlyMembers = null)
    {
        Debug.Assert(constructor.MethodKind is MethodKind.Constructor || constructor.IsStatic);
        Debug.Assert(IsAccessibleFromGeneratedType(constructor));

        var parameters = new List<ConstructorParameterModel>();
        foreach (IParameterSymbol param in constructor.Parameters)
        {
            parameters.Add(MapConstructorParameter(param));
        }

        var memberInitializers = new List<ConstructorParameterModel>();
        bool setsRequiredMembers = constructor.HasSetsRequiredMembersAttribute();
        int position = parameters.Count;

        HashSet<(string FQN, string name)>? ctorParameterSet = constructor.ContainingType.IsRecord
            ? new(parameters.Select(paramModel => (paramModel.ParameterType.FullyQualifiedName, paramModel.Name)))
            : null;

        foreach (ConstructorParameterModel memberInitializer in (requiredOrInitOnlyMembers ?? []))
        {
            if (setsRequiredMembers && memberInitializer.IsRequired)
            {
                continue;
            }

            if (!memberInitializer.IsRequired && memberInitializer.IsAutoProperty && 
                ctorParameterSet?.Contains((memberInitializer.ParameterType.FullyQualifiedName, memberInitializer.Name)) == true)
            {
                // In records, deduplicate any init auto-properties whose signature matches the constructor parameters.
                continue;
            }

            memberInitializers.Add(memberInitializer with { Position = position++ });
        }

        return new ConstructorModel
        {
            DeclaringType = SymbolEqualityComparer.Default.Equals(constructor.ContainingType, type)
                ? typeId
                : CreateTypeId(constructor.ContainingType),

            Parameters = parameters.ToImmutableEquatableArray(),
            MemberInitializers = memberInitializers.ToImmutableEquatableArray(),
            StaticFactoryName = constructor.IsStatic 
            ? $"{constructor.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{constructor.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}" 
            : null,
        };
    }

    private ConstructorParameterModel MapConstructorParameter(IParameterSymbol parameter)
    {
        TypeId typeId = EnqueueForGeneration(parameter.Type);
        return new ConstructorParameterModel
        {
            Name = parameter.Name,
            Position = parameter.Ordinal,
            ParameterType = typeId,
            IsRequired = !parameter.HasExplicitDefaultValue,
            IsMemberInitializer = false,
            IsAutoProperty = false,
            IsNonNullable = parameter.IsNonNullableAnnotation(),
            HasDefaultValue = parameter.HasExplicitDefaultValue,
            DefaultValue = parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue : null,
            DefaultValueRequiresCast = parameter.Type 
                is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } 
                or { TypeKind: TypeKind.Enum },
        };
    }

    private ConstructorParameterModel? MapPropertyInitializer(IPropertySymbol propertySymbol)
    {
        if (!propertySymbol.IsRequired && propertySymbol.SetMethod?.IsInitOnly != true)
        {
            return null;
        }

        TypeId typeId = EnqueueForGeneration(propertySymbol.Type);
        propertySymbol.ResolveNullableAnnotation(out _, out bool isSetterNonNullable);
        return new ConstructorParameterModel
        {
            ParameterType = typeId,
            Name = propertySymbol.Name,
            Position = -1, // must be set relative to each constructor arity
            IsRequired = propertySymbol.IsRequired,
            IsMemberInitializer = true,
            IsAutoProperty = propertySymbol.IsAutoProperty(),
            IsNonNullable = isSetterNonNullable,
            HasDefaultValue = false,
            DefaultValue = null,
            DefaultValueRequiresCast = false,
        };
    }

    private ConstructorParameterModel? MapFieldInitializer(IFieldSymbol fieldSymbol)
    {
        if (!fieldSymbol.IsRequired)
        {
            return null;
        }

        TypeId typeId = EnqueueForGeneration(fieldSymbol.Type);
        fieldSymbol.ResolveNullableAnnotation(out _, out bool isSetterNonNullable);
        return new ConstructorParameterModel
        {
            ParameterType = typeId,
            Name = fieldSymbol.Name,
            Position = -1, // must be set relative to each constructor arity
            IsRequired = fieldSymbol.IsRequired,
            IsMemberInitializer = true,
            IsAutoProperty = false,
            IsNonNullable = isSetterNonNullable,
            HasDefaultValue = false,
            DefaultValue = null,
            DefaultValueRequiresCast = false,
        };
    }

    private IEnumerable<ConstructorModel> MapTupleConstructors(TypeId typeId, ITypeSymbol tupleType, IEnumerable<ITypeSymbol> tupleElements)
    {
        if (tupleType.IsValueType)
        {
            // Return the default constructor for value tuples
            yield return new ConstructorModel
            {
                DeclaringType = typeId,
                Parameters = [],
                MemberInitializers = [],
                StaticFactoryName = null,
            };
        }

        yield return new ConstructorModel
        {
            DeclaringType = typeId,
            Parameters = tupleElements.Select(MapTupleConstructorParameter).ToImmutableEquatableArray(),
            MemberInitializers = [],
            StaticFactoryName = null,
        };

        ConstructorParameterModel MapTupleConstructorParameter(ITypeSymbol tupleElement, int position)
        {
            TypeId typeId = EnqueueForGeneration(tupleElement);
            return new ConstructorParameterModel
            { 
                Name = $"Item{position + 1}",
                Position = position,
                ParameterType = typeId,
                HasDefaultValue = false,
                IsRequired = true,
                IsMemberInitializer = false,
                IsNonNullable = tupleElement.IsNonNullableAnnotation(),
                IsAutoProperty = false,
                DefaultValue = null,
                DefaultValueRequiresCast = false,
            };
        }
    }

    private IMethodSymbol? TryResolveFactoryMethod(ITypeSymbol type)
    {
        // TODO add support for CollectionBuilderAttribute resolution
        // cf. https://github.com/dotnet/runtime/issues/87569

        if (type is IArrayTypeSymbol arrayType && arrayType.Rank == 1)
        {
            return semanticModel.Compilation.GetTypeByMetadataName("System.Linq.Enumerable")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "ToArray" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "IEnumerable")
                .MakeGenericMethod(arrayType.ElementType);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return null;
        }

        SymbolEqualityComparer cmp = SymbolEqualityComparer.Default;

        if (!namedType.IsGenericType)
        {
            if (namedType.IsAssignableFrom(knownSymbols.IList))
            {
                // Handle IList, ICollection and IEnumerable interfaces using object[]
                return semanticModel.Compilation.GetTypeByMetadataName("System.Linq.Enumerable")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "ToArray" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "IEnumerable")
                    .MakeGenericMethod(knownSymbols.ObjectType);
            }

            if (cmp.Equals(namedType, knownSymbols.IDictionary))
            {
                // Handle IDictionary using Dictionary<object, object>
                return knownSymbols.DictionaryOfTKeyTValue?.Construct(knownSymbols.ObjectType, knownSymbols.ObjectType).Constructors
                    .FirstOrDefault(ctor => ctor.Parameters.Length == 1 && ctor.Parameters[0].Type.Name == "IEnumerable");
            }

            return null;
        }

        if (namedType.TypeKind is TypeKind.Interface)
        {
            if (namedType.TypeArguments.Length == 1 && knownSymbols.ListOfT?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) != null)
            {
                // Handle IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> types using List<T>
                return semanticModel.Compilation.GetTypeByMetadataName("System.Linq.Enumerable")
                    .GetMethodSymbol(method =>
                        method.IsStatic && method.IsGenericMethod && method.Name is "ToList" &&
                        method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "IEnumerable")
                    .MakeGenericMethod(namedType.TypeArguments[0]);
            }

            if (namedType.TypeArguments.Length == 1 && knownSymbols.HashSetOfT?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) != null)
            {
                // Handle ISet<T> and IReadOnlySet<T> types using HashSet<T>
                return knownSymbols.HashSetOfT?.Construct(namedType.TypeArguments[0]).Constructors
                    .FirstOrDefault(ctor => ctor.Parameters.Length == 1 && ctor.Parameters[0].Type.Name == "IEnumerable");
            }

            if (namedType.TypeArguments.Length == 2 && knownSymbols.DictionaryOfTKeyTValue?.GetCompatibleGenericBaseType(namedType.ConstructedFrom) != null)
            {
                // Handle IDictionary<TKey, TValue> and IReadOnlyDictionary<TKey, TValue> using Dictionary<TKey, TValue>
                return knownSymbols.DictionaryOfTKeyTValue?.Construct(namedType.TypeArguments[0], namedType.TypeArguments[1]).Constructors
                    .FirstOrDefault(ctor => ctor.Parameters.Length == 1 && ctor.Parameters[0].Type.Name == "IEnumerable");
            }

            return null;
        }

        if (cmp.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableArray))
        { 
            return semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0]);
        }

        if (cmp.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableList))
        {
            return semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0]);
        }

        if (cmp.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableQueue))
        {
            return semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableQueue")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0]);
        }

        if (cmp.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableStack))
        {
            return semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableStack")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0]);
        }

        if (cmp.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableHashSet))
        {
            return semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableHashSet")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0]);
        }

        if (cmp.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableSortedSet))
        {
            return semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedSet")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0]);
        }

        if (cmp.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableDictionary))
        {
            return semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
        }

        if (cmp.Equals(namedType.ConstructedFrom, knownSymbols.ImmutableSortedDictionary))
        {
            return semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedDictionary")
                .GetMethodSymbol(method =>
                    method.IsStatic && method.IsGenericMethod && method.Name is "CreateRange" &&
                    method.Parameters.Length == 1 && method.Parameters[0].Type.Name is "IEnumerable")
                .MakeGenericMethod(namedType.TypeArguments[0], namedType.TypeArguments[1]);
        }

        return null;
    }
}
