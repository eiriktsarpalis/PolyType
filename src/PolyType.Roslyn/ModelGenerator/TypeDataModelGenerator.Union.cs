using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    /// <summary>
    /// Attempts to map a C# union and its creation parameter types.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="ctx">The current graph traversal context.</param>
    /// <param name="methodModels">The type's method models.</param>
    /// <param name="eventModels">The type's event models.</param>
    /// <param name="requirements">The requirements for the case shapes.</param>
    /// <param name="model">The resulting model, or <see langword="null"/> on failure.</param>
    /// <param name="status">The mapping status.</param>
    /// <returns>Whether the type opted into union discovery, including invalid union patterns.</returns>
    private bool TryMapUnion(
        ITypeSymbol type,
        ref TypeDataModelGenerationContext ctx,
        ImmutableArray<MethodDataModel> methodModels,
        ImmutableArray<EventDataModel> eventModels,
        TypeShapeRequirements requirements,
        out TypeDataModel? model,
        out TypeDataModelGenerationStatus status)
    {
        model = null;
        status = TypeDataModelGenerationStatus.UnsupportedType;
        if (type is not INamedTypeSymbol unionType || !type.IsCSharpUnion(CancellationToken))
        {
            return false;
        }

        INamedTypeSymbol? provider = GetUnionMemberProvider(unionType);
        if (provider is null && unionType.IsAbstract)
        {
            return true;
        }

        List<(INamedTypeSymbol Declared, INamedTypeSymbol Constructed)> hierarchy = provider is null
            ? GetUnionClassHierarchy(unionType)
            : GetUnionProviderHierarchy(provider);

        IPropertySymbol? valueProperty = GetUnionValueProperty(hierarchy, provider is not null);
        if (valueProperty is null)
        {
            return true;
        }

        List<(IMethodSymbol Declared, IMethodSymbol Constructed)> creators = provider is null
            ? unionType.InstanceConstructors
                .Where(IsSuitableUnionConstructor)
                .Select(ctor => (ctor.OriginalDefinition, ctor))
                .ToList()
            : GetUnionFactories(unionType, hierarchy);

        if (creators.Count == 0)
        {
            return true;
        }

        if (!IsAccessibleSymbol(valueProperty.ContainingType) ||
            creators.Any(creator => !IsAccessibleSymbol(creator.Constructed.ContainingType)))
        {
            status = TypeDataModelGenerationStatus.InaccessibleType;
            return true;
        }

        Dictionary<ITypeSymbol, IMethodSymbol> tryGetValueMethods = new(SymbolEqualityComparer.Default);
        foreach ((_, INamedTypeSymbol constructed) in GetUnionClassHierarchy(unionType))
        {
            foreach (IMethodSymbol method in constructed.GetMembers("TryGetValue").OfType<IMethodSymbol>())
            {
                if (method is
                    {
                        IsStatic: false,
                        Arity: 0,
                        MethodKind: MethodKind.Ordinary,
                        DeclaredAccessibility: Accessibility.Public,
                        RefKind: RefKind.None,
                        ReturnType.SpecialType: SpecialType.System_Boolean,
                        Parameters: [{ RefKind: RefKind.Out } parameter],
                    } && !tryGetValueMethods.ContainsKey(parameter.Type))
                {
                    tryGetValueMethods.Add(parameter.Type, method);
                }
            }
        }

        var cases = ImmutableArray.CreateBuilder<UnionCaseDataModel>(creators.Count);
        foreach ((IMethodSymbol declared, IMethodSymbol creator) in creators)
        {
            IParameterSymbol parameter = creator.Parameters[0];
            status = IncludeNestedType(parameter.Type, ref ctx, requirements);
            if (status is not TypeDataModelGenerationStatus.Success)
            {
                return true;
            }

            OnMemberAccessed(creator);
            tryGetValueMethods.TryGetValue(parameter.Type, out IMethodSymbol? tryGetValue);
            if (tryGetValue is not null)
            {
                OnMemberAccessed(tryGetValue);
            }

            cases.Add(new UnionCaseDataModel
            {
                Type = parameter.Type,
                DeclaredType = declared.Parameters[0].Type,
                CreationMember = creator,
                TryGetValueMethod = tryGetValue,
                IsNullable = !parameter.IsNonNullableAnnotation(),
            });
        }

        OnMemberAccessed(valueProperty);
        model = new CSharpUnionDataModel
        {
            Type = type,
            Requirements = TypeShapeRequirements.Full,
            Methods = methodModels,
            Events = eventModels,
            UnionCases = cases.ToImmutable(),
            ValueProperty = valueProperty,
            UnionMemberProvider = provider,
        };

        status = TypeDataModelGenerationStatus.Success;
        return true;
    }

    private static bool IsSuitableUnionConstructor(IMethodSymbol method) =>
        method is { DeclaredAccessibility: Accessibility.Public, Parameters: [{ RefKind: RefKind.None or RefKind.In }] };

    private static INamedTypeSymbol? GetUnionMemberProvider(INamedTypeSymbol unionType)
    {
        foreach (INamedTypeSymbol candidate in unionType.GetTypeMembers("IUnionMembers"))
        {
            // A non-generic nested interface can still inherit type arguments from its containing union.
            if (candidate.Arity != 0)
            {
                continue;
            }

            return candidate is { TypeKind: TypeKind.Interface, DeclaredAccessibility: Accessibility.Public } &&
                unionType.AllInterfaces.Contains(candidate, SymbolEqualityComparer.Default)
                ? candidate : null;
        }

        return null;
    }

    private static List<(INamedTypeSymbol Declared, INamedTypeSymbol Constructed)> GetUnionClassHierarchy(INamedTypeSymbol type)
    {
        List<(INamedTypeSymbol, INamedTypeSymbol)> result = [];
        for (INamedTypeSymbol? declared = type.OriginalDefinition, constructed = type;
             declared is not null && constructed is not null;
             declared = declared.BaseType, constructed = constructed.BaseType)
        {
            result.Add((declared, constructed));
        }

        return result;
    }

    private static List<(INamedTypeSymbol Declared, INamedTypeSymbol Constructed)> GetUnionProviderHierarchy(INamedTypeSymbol provider)
    {
        List<(INamedTypeSymbol, INamedTypeSymbol)> result = [];
        HashSet<INamedTypeSymbol> visited = new(SymbolEqualityComparer.Default);
        Visit(provider.OriginalDefinition, provider);
        result.Reverse();
        return result;

        void Visit(INamedTypeSymbol declared, INamedTypeSymbol constructed)
        {
            if (!visited.Add(declared))
            {
                return;
            }

            // Reverse postorder retains declaration order among unrelated interfaces.
            for (int i = declared.Interfaces.Length - 1; i >= 0; i--)
            {
                Visit(declared.Interfaces[i], constructed.Interfaces[i]);
            }

            result.Add((declared, constructed));
        }
    }

    private static IPropertySymbol? GetUnionValueProperty(
        List<(INamedTypeSymbol Declared, INamedTypeSymbol Constructed)> hierarchy,
        bool isProvider)
    {
        IPropertySymbol? match = null;
        foreach ((INamedTypeSymbol declared, INamedTypeSymbol constructed) in hierarchy)
        {
            // Compiler lookup skips unsuitable members rather than letting them hide an eligible base getter.
            IPropertySymbol? property = declared.GetMembers("Value").OfType<IPropertySymbol>()
                .FirstOrDefault(property => property is
                {
                    IsStatic: false,
                    IsIndexer: false,
                    RefKind: RefKind.None,
                    Type.SpecialType: SpecialType.System_Object,
                    GetMethod.DeclaredAccessibility: Accessibility.Public,
                });

            if (property is null)
            {
                continue;
            }

            // Inherited interface getters must resolve by hiding, not by traversal order.
            if (match is null)
            {
                match = FindConstructedUnionMember(property, constructed);
                if (!isProvider || SymbolEqualityComparer.Default.Equals(declared, hierarchy[0].Declared))
                {
                    return match;
                }
            }
            else if (!match.ContainingType.AllInterfaces.Contains(constructed, SymbolEqualityComparer.Default))
            {
                return null;
            }
        }

        return match;
    }

    private static List<(IMethodSymbol Declared, IMethodSymbol Constructed)> GetUnionFactories(
        INamedTypeSymbol unionType,
        List<(INamedTypeSymbol Declared, INamedTypeSymbol Constructed)> hierarchy)
    {
        List<(IMethodSymbol Declared, IMethodSymbol Constructed)> result = [];
        foreach ((INamedTypeSymbol declared, INamedTypeSymbol constructed) in hierarchy)
        {
            foreach (IMethodSymbol method in declared.GetMembers("Create").OfType<IMethodSymbol>())
            {
                if (method is not
                    {
                        IsStatic: true,
                        Arity: 0,
                        MethodKind: MethodKind.Ordinary,
                        DeclaredAccessibility: Accessibility.Public,
                        RefKind: RefKind.None,
                        Parameters: [{ RefKind: RefKind.None or RefKind.In }],
                    } ||
                    !SymbolEqualityComparer.Default.Equals(method.ReturnType, unionType.OriginalDefinition))
                {
                    continue;
                }

                // Inherited declarations already substitute the enclosing union's type parameters.
                // Closed signatures would hide distinct cases that consumers must validate as collisions.
                if (result.Any(previous => Shadows(previous.Declared, method)))
                {
                    continue;
                }

                result.Add((method, FindConstructedUnionMember(method, constructed)));
            }
        }

        return result;

        static bool Shadows(IMethodSymbol derived, IMethodSymbol candidate) =>
            !SymbolEqualityComparer.Default.Equals(derived.ContainingType, candidate.ContainingType) &&
            derived.ContainingType.AllInterfaces.Contains(candidate.ContainingType, SymbolEqualityComparer.Default) &&
            derived.Parameters[0].RefKind == candidate.Parameters[0].RefKind &&
            SymbolEqualityComparer.Default.Equals(derived.Parameters[0].Type, candidate.Parameters[0].Type);
    }

    private static TMember FindConstructedUnionMember<TMember>(TMember declaration, INamedTypeSymbol constructed)
        where TMember : ISymbol
    {
        return constructed.GetMembers(declaration.Name).OfType<TMember>()
            .First(member => SymbolEqualityComparer.Default.Equals(member.OriginalDefinition, declaration.OriginalDefinition));
    }
}
