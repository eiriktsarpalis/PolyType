using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;
using System.Reflection;

namespace PolyType.Roslyn;

/// <summary>
/// Provides functionality and extensibility points for generating <see cref="TypeDataModel"/>
/// instances for a given set of <see cref="ITypeSymbol"/> inputs.
/// </summary>
public partial class TypeDataModelGenerator
{
    private ImmutableDictionary<ITypeSymbol, TypeDataModel> _generatedModels;
    private List<EquatableDiagnostic>? _diagnostics;
    private ImmutableDictionary<ITypeSymbol, ImmutableArray<AssociatedTypeModel>> _associatedTypes;

    /// <summary>
    /// Creates a new <see cref="TypeDataModelGenerator"/> instance.
    /// </summary>
    /// <param name="generationScope">The context symbol used to determine accessibility for processed types.</param>
    /// <param name="knownSymbols">The known symbols cache constructed from the current <see cref="Compilation"/>.</param>
    /// <param name="cancellationToken">The cancellation token to be used by the generator.</param>
    public TypeDataModelGenerator(ISymbol generationScope, KnownSymbols knownSymbols, CancellationToken cancellationToken)
    {
        GenerationScope = generationScope;
        KnownSymbols = knownSymbols;
        CancellationToken = cancellationToken;
        _generatedModels = ImmutableDictionary.Create<ITypeSymbol, TypeDataModel>(SymbolComparer);
        _associatedTypes = ImmutableDictionary.Create<ITypeSymbol, ImmutableArray<AssociatedTypeModel>>(SymbolComparer);
    }

    /// <summary>
    /// The context symbol used to determine accessibility for processed types.
    /// </summary>
    public ISymbol GenerationScope { get; }

    /// <summary>
    /// The default location to be used for diagnostics.
    /// </summary>
    public virtual Location? DefaultLocation => GenerationScope.Locations.FirstOrDefault();

    /// <summary>
    /// The known symbols cache constructed from the current <see cref="Compilation" />.
    /// </summary>
    public KnownSymbols KnownSymbols { get; }

    /// <summary>
    /// The cancellation token to be used by the generator.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Full generated models, keyed by their type symbol.
    /// </summary>
    public ImmutableDictionary<ITypeSymbol, TypeDataModel> GeneratedModels => _generatedModels;

    /// <summary>
    /// Gets the associated types, indexed by their originating types.
    /// </summary>
    public ImmutableDictionary<ITypeSymbol, ImmutableArray<AssociatedTypeModel>> AssociatedTypes => _associatedTypes;

    /// <summary>
    /// The <see cref="SymbolEqualityComparer"/> used to identify type symbols.
    /// Defaults to <see cref="SymbolEqualityComparer.Default"/>.
    /// </summary>
    public virtual SymbolEqualityComparer SymbolComparer => SymbolEqualityComparer.Default;

    /// <summary>
    /// Attempt to generate a <see cref="TypeDataModel"/> for the given <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The type for which to generate a data model.</param>
    /// <returns>The generation status for the given type.</returns>
    public TypeDataModelGenerationStatus IncludeType(ITypeSymbol type)
    {
        ImmutableDictionary<ITypeSymbol, TypeDataModel> generatedModelsSnapshot = _generatedModels;
        var ctx = new TypeDataModelGenerationContext(ImmutableStack<ITypeSymbol>.Empty, generatedModelsSnapshot);
        TypeDataModelGenerationStatus status = IncludeNestedType(type, ref ctx);

        if (status is TypeDataModelGenerationStatus.Success)
        {
            if (Interlocked.CompareExchange(ref _generatedModels, ctx.GeneratedModels, generatedModelsSnapshot) != generatedModelsSnapshot)
            {
                throw new InvalidOperationException("Model generator is being called concurrently by another thread.");
            }
        }

        return status;
    }

    /// <summary>
    /// Gets the list of equatable diagnostics that have been recorded by this model generator.
    /// </summary>
    public List<EquatableDiagnostic> Diagnostics => _diagnostics ??= [];

    /// <summary>
    /// Adds a new diagnostic to the <see cref="Diagnostics"/> property.
    /// </summary>
    public void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[] messageArgs)
    {
        if (location is not null && !KnownSymbols.Compilation.ContainsLocation(location))
        {
            // If the location is outside the current compilation,
            // fall back to the default location for the generator.
            location = DefaultLocation;
        }

        Diagnostics.Add(new EquatableDiagnostic(descriptor, location, messageArgs));
    }

    /// <summary>
    /// When overridden, performs normalization operations on the given type before it is processed.
    /// </summary>
    protected virtual ITypeSymbol NormalizeType(ITypeSymbol type) => type;

    /// <summary>
    /// When overridden, returns the derived types of the given type.
    /// </summary>
    /// <param name="type">The base type to resolve derived types from.</param>
    /// <returns>An IEnumerable containing derived type models</returns>
    protected virtual IEnumerable<DerivedTypeModel> ResolveDerivedTypes(ITypeSymbol type) => [];

    /// <summary>
    ///  Resolves the method symbols that should be included for the given type.
    /// </summary>
    /// <param name="type">The declaring type from which to resolve the methods.</param>
    /// <param name="bindingFlags">The binding flags of the methods to include.</param>
    /// <returns>A struct containing resolved method information.</returns>
    protected virtual IEnumerable<ResolvedMethodSymbol> ResolveMethods(ITypeSymbol type, BindingFlags bindingFlags) => [];

    /// <summary>
    ///  Resolves the event symbols that should be included for the given type.
    /// </summary>
    /// <param name="type">The declaring type from which to resolve the methods.</param>
    /// <param name="bindingFlags">The binding flags of the methods to include.</param>
    /// <returns>A struct containing resolved event information.</returns>
    protected virtual IEnumerable<ResolvedEventSymbol> ResolveEvents(ITypeSymbol type, BindingFlags bindingFlags) => [];

    /// <summary>
    /// Wraps the <see cref="MapType(ITypeSymbol, TypeDataKind?, BindingFlags?, ImmutableArray{AssociatedTypeModel}, ref TypeDataModelGenerationContext, TypeShapeRequirements, out TypeDataModel?)"/> method
    /// with pre- and post-processing steps necessary for a type graph traversal.
    /// </summary>
    /// <param name="type">The type for which to generate a data model.</param>
    /// <param name="ctx">The context token holding state for the current type graph traversal.</param>
    /// <param name="requirements">The detail to include in the shape.</param>
    /// <returns>The model generation status for the given type.</returns>
    protected TypeDataModelGenerationStatus IncludeNestedType(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, TypeShapeRequirements requirements = TypeShapeRequirements.Full)
    {
        CancellationToken.ThrowIfCancellationRequested();
        type = NormalizeType(type);

        if (ctx.GeneratedModels.TryGetValue(type, out TypeDataModel? model))
        {
            // Consider that a prior request may have produced a shape with fewer requirements than this run.
            if ((requirements & ~model.Requirements) == TypeShapeRequirements.None)
            {
                model.IsRootType |= ctx.Stack.IsEmpty;
                return TypeDataModelGenerationStatus.Success;
            }
            else
            {
                // Although we've generated a shape for this type already,
                // it is missing some of the requirements our caller requires.
                // Regenerate it with a union of the flags between the two requests.
                requirements |= model.Requirements;
            }
        }

        if (!IsSupportedType(type))
        {
            return TypeDataModelGenerationStatus.UnsupportedType;
        }

        if (!IsAccessibleSymbol(type))
        {
            return TypeDataModelGenerationStatus.InaccessibleType;
        }

        if (ctx.Stack.Contains(type, SymbolComparer))
        {
            // Recursive type detected, skip with success
            return TypeDataModelGenerationStatus.Success;
        }

        // Create a new snapshot with the current type pushed onto the stack.
        // Only commit the generated model if the type is successfully mapped.
        TypeDataModelGenerationContext scopedCtx = ctx.Push(type);
        TypeDataModelGenerationStatus status = MapType(type, requestedKind: null, methodBindingFlags: null, ImmutableArray<AssociatedTypeModel>.Empty, ref scopedCtx, requirements, out model);

        if (status is TypeDataModelGenerationStatus.Success != model is not null)
        {
            throw new InvalidOperationException($"The '{nameof(MapType)}' method returned inconsistent {nameof(TypeDataModelGenerationStatus)} and {nameof(TypeDataModel)} results.");
        }

        if (model != null)
        {
            ctx = scopedCtx.Commit(model);
            model.IsRootType = ctx.Stack.IsEmpty;
        }

        return status;
    }

    /// <summary>
    /// The core, overridable data model mapping method for a given type.
    /// </summary>
    /// <param name="type">The type for which to generate a data model.</param>
    /// <param name="requestedKind">The target kind as specified in configuration.</param>
    /// <param name="methodBindingFlags">The binding flags used to resolve method shapes.</param>
    /// <param name="requirements">The detail to include in the shape.</param>
    /// <param name="associatedTypes">Associated types for this shape.</param>
    /// <param name="ctx">The context token holding state for the current type graph traversal.</param>
    /// <param name="model">The model that the current symbol is being mapped to.</param>
    /// <returns>The model generation status for the given type.</returns>
    /// <remarks>
    /// The method should only be overridden but not invoked directly.
    /// Call <see cref="IncludeNestedType(ITypeSymbol, ref TypeDataModelGenerationContext, TypeShapeRequirements)"/> instead.
    /// </remarks>
    protected virtual TypeDataModelGenerationStatus MapType(
        ITypeSymbol type,
        TypeDataKind? requestedKind,
        BindingFlags? methodBindingFlags,
        ImmutableArray<AssociatedTypeModel> associatedTypes,
        ref TypeDataModelGenerationContext ctx,
        TypeShapeRequirements requirements,
        out TypeDataModel? model)
    {
        TypeDataModelGenerationStatus status;
        IncludeAssociatedShapes(type, associatedTypes, ref ctx);
        ImmutableArray<MethodDataModel> methodModels = MapMethods(type, ref ctx, methodBindingFlags);
        ImmutableArray<EventDataModel> eventModels = MapEvents(type, ref ctx, methodBindingFlags);

        switch (requestedKind)
        {
            // If the configuration specifies an explicit kind, try to resolve that or fall back to no shape.
            case TypeDataKind.Enum:
                if (TryMapEnum(type, ref ctx, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Optional:
                if (TryMapOptional(type, ref ctx, methodModels, eventModels, requirements, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Dictionary:
                if (TryMapDictionary(type, ref ctx, methodModels, eventModels, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Enumerable:
                if (TryMapEnumerable(type, ref ctx, methodModels, eventModels, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Tuple:
                if (TryMapTuple(type, ref ctx, methodModels, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Delegate:
                if (TryMapDelegate(type, ref ctx, methodModels, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.Object:
                if (TryMapObject(type, ref ctx, methodModels, eventModels, requirements, out model, out status))
                {
                    return status;
                }
                goto None;

            case TypeDataKind.None:
                goto None;
        }

        if (TryMapEnum(type, ref ctx, out model, out status))
        {
            return status;
        }

        if (TryMapOptional(type, ref ctx, methodModels, eventModels, requirements, out model, out status))
        {
            return status;
        }

        // Important: Dictionary resolution goes before Enumerable
        // since Dictionary also implements IEnumerable
        if (TryMapDictionary(type, ref ctx, methodModels, eventModels, out model, out status))
        {
            return status;
        }

        if (TryMapEnumerable(type, ref ctx, methodModels, eventModels, out model, out status))
        {
            return status;
        }

        if (TryMapTuple(type, ref ctx, methodModels, out model, out status))
        {
            return status;
        }

        if (TryMapDelegate(type, ref ctx, methodModels, out model, out status))
        {
            return status;
        }

        if (TryMapObject(type, ref ctx, methodModels, eventModels, requirements, out model, out status))
        {
            return status;
        }

    None:
        // A supported type of unrecognized kind, do not include any metadata.
        model = new TypeDataModel
        {
            Type = type,
            DerivedTypes = IncludeDerivedTypes(type, ref ctx, requirements),
            Methods = methodModels,
            Events = eventModels,
            Requirements = TypeShapeRequirements.Full,
        };

        return TypeDataModelGenerationStatus.Success;
    }

    /// <summary>
    /// Determines if the specified symbol is accessible within the <see cref="GenerationScope"/> symbol.
    /// </summary>
    protected virtual bool IsAccessibleSymbol(ISymbol symbol)
    {
        return KnownSymbols.Compilation.IsSymbolAccessibleWithin(symbol, within: GenerationScope);
    }

    /// <summary>
    /// Determines if the specified symbol is supported for data model generation.
    /// </summary>
    /// <remarks>
    /// By default, unsupported types are void, pointers, and generic type definitions.
    /// </remarks>
    protected virtual bool IsSupportedType(ITypeSymbol type)
    {
        return type.TypeKind is not (TypeKind.Pointer or TypeKind.Error) &&
          type.SpecialType is not SpecialType.System_Void && !type.ContainsGenericParameters();
    }

    /// <summary>
    /// Gets the associated types for a given type, as specified by 3rd party custom attributes.
    /// </summary>
    /// <param name="symbol">The type (or property or field with a type of interest) whose associated types are sought.</param>
    /// <param name="associatedTypes">The associated types for the given <paramref name="symbol"/>.</param>
    protected virtual void ParseCustomAssociatedTypeAttributes(
        ISymbol symbol,
        out ImmutableArray<AssociatedTypeModel> associatedTypes)
    {
        associatedTypes = ImmutableArray<AssociatedTypeModel>.Empty;
    }

    private ImmutableArray<DerivedTypeModel> IncludeDerivedTypes(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, TypeShapeRequirements requirements)
    {
        // 1. Resolve the shapes for all derived types.
        List<DerivedTypeModel> derivedTypeModels = [];
        DerivedTypeModel baseTypeModel = new() { Type = type, Name = null!, Tag = -1, IsTagSpecified = false, Index = -1, IsBaseType = true };
        foreach (DerivedTypeModel derivedType in ResolveDerivedTypes(type))
        {
            if (IncludeNestedType(derivedType.Type, ref ctx, requirements) is TypeDataModelGenerationStatus.Success)
            {
                derivedTypeModels.Add(derivedType);
            }

            if (derivedType.IsBaseType)
            {
                baseTypeModel = derivedType; // Replace the placeholder value for the base type.
            }
        }

        // 2. Perform a topological sort of the derived types, starting from most derived down to the base type.
        if (derivedTypeModels.Count < 2)
        {
            return derivedTypeModels.ToImmutableArray();
        }

        return CommonHelpers.TraverseGraphWithTopologicalSort(baseTypeModel, GetDerivedTypes)
            .Where(unionCase => unionCase.Index >= 0)
            .Reverse()
            .ToImmutableArray();

        IReadOnlyCollection<DerivedTypeModel> GetDerivedTypes(DerivedTypeModel current)
        {
            List<DerivedTypeModel> derivedTypes = [];
            foreach (DerivedTypeModel derivedType in derivedTypeModels)
            {
                if (!SymbolEqualityComparer.Default.Equals(current.Type, derivedType.Type) && current.Type.IsAssignableFrom(derivedType.Type))
                {
                    derivedTypes.Add(derivedType);
                }
            }

            return derivedTypes;
        }
    }

    /// <summary>
    /// Maps the methods resolved from the current type and their parameters to an array of <see cref="MethodDataModel"/>.
    /// </summary>
    /// <param name="type">The type from which to resolve methods.</param>
    /// <param name="ctx">The current model generation context.</param>
    /// <param name="bindingFlags">The binding flags to use when resolving methods.</param>
    /// <returns>An array of mapped method data models.</returns>
    protected ImmutableArray<MethodDataModel> MapMethods(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, BindingFlags? bindingFlags)
    {
        ImmutableArray<MethodDataModel>.Builder results = ImmutableArray.CreateBuilder<MethodDataModel>();
        foreach (ResolvedMethodSymbol resolvedMethod in ResolveMethods(type, bindingFlags ?? BindingFlags.Default))
        {
            TypeDataModelGenerationContext scopedCtx = ctx;
            if (MapMethod(resolvedMethod, ref scopedCtx, out MethodDataModel methodDataModel) is TypeDataModelGenerationStatus.Success)
            {
                results.Add(methodDataModel);
                ctx = scopedCtx;
            }
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Maps the given <paramref name="resolvedMethod"/> and its parameters to a <see cref="MethodDataModel"/>.
    /// </summary>
    /// <param name="resolvedMethod">The method to try to map.</param>
    /// <param name="ctx">The current model generation context.</param>
    /// <param name="result">The mapped method data model.</param>
    /// <returns>The result of the mapping operation.</returns>
    protected virtual TypeDataModelGenerationStatus MapMethod(ResolvedMethodSymbol resolvedMethod, ref TypeDataModelGenerationContext ctx, out MethodDataModel result)
    {
        TypeDataModelGenerationStatus status;

        var parameters = ImmutableArray.CreateBuilder<ParameterDataModel>(resolvedMethod.MethodSymbol.Parameters.Length);
        foreach (IParameterSymbol parameter in resolvedMethod.MethodSymbol.Parameters)
        {
            if (parameter.RefKind is RefKind.Out)
            {
                result = default;
                return TypeDataModelGenerationStatus.UnsupportedType;
            }

            if ((status = IncludeNestedType(parameter.Type, ref ctx)) != TypeDataModelGenerationStatus.Success)
            {
                result = default;
                return status;
            }

            ParameterDataModel parameterModel = MapParameter(parameter);
            parameters.Add(parameterModel);
        }

        ITypeSymbol? returnType = GetEffectiveReturnType(resolvedMethod.MethodSymbol, out MethodReturnTypeKind returnTypeKind);
        if (returnType is not null && (status = IncludeNestedType(returnType, ref ctx)) != TypeDataModelGenerationStatus.Success)
        {
            result = default;
            return status;
        }

        result = new MethodDataModel
        {
            Name = resolvedMethod.CustomName ?? resolvedMethod.MethodSymbol.Name,
            Method = resolvedMethod.MethodSymbol,
            ReturnedValueType = returnType,
            ReturnTypeKind = returnTypeKind,
            Parameters = parameters.ToImmutable(),
            IsAmbiguous = resolvedMethod.IsAmbiguous,
        };

        return TypeDataModelGenerationStatus.Success;
    }

    /// <summary>
    /// Extracts the actual return type from async wrappers like Task or ValueTask,
    /// returning null if the method returns void or the non-generic Task or ValueTask.
    /// </summary>
    /// <param name="method">The method from which to extract the return type.</param>
    /// <param name="kind">The inferred type kind.</param>
    /// <returns>A type symbol representing the underlying type being returned.</returns>
    protected virtual ITypeSymbol? GetEffectiveReturnType(IMethodSymbol method, out MethodReturnTypeKind kind)
    {
        ITypeSymbol? returnType = method.ReturnType;
        kind = MethodReturnTypeKind.Unrecognized;

        if (returnType.SpecialType is SpecialType.System_Void)
        {
            returnType = null;
            kind = MethodReturnTypeKind.Void;
        }
        else if (KnownSymbols.TaskType.IsAssignableFrom(returnType))
        {
            if (returnType is INamedTypeSymbol { IsGenericType: true } namedType)
            {
                returnType = namedType.TypeArguments[0]; // Task<T>
                kind = MethodReturnTypeKind.TaskOfT;
            }
            else
            {
                returnType = null; // Task
                kind = MethodReturnTypeKind.Task;
            }
        }
        else if (SymbolEqualityComparer.Default.Equals(returnType, KnownSymbols.ValueTaskType))
        {
            // ValueTask
            returnType = null;
            kind = MethodReturnTypeKind.ValueTask;
        }
        else if (returnType.GetCompatibleGenericBaseType(KnownSymbols.ValueTaskOfTType) is { } valueTaskOf)
        {
            // ValueTask<T>
            returnType = valueTaskOf.TypeArguments[0];
            kind = MethodReturnTypeKind.ValueTaskOfT;
        }

        return returnType;
    }

    /// <summary>
    /// Maps the events resolved from the current type and their parameters to an array of <see cref="EventDataModel"/>.
    /// </summary>
    /// <param name="type">The type from which to resolve methods.</param>
    /// <param name="ctx">The current model generation context.</param>
    /// <param name="bindingFlags">The binding flags to use when resolving methods.</param>
    /// <returns>An array of mapped method data models.</returns>
    protected ImmutableArray<EventDataModel> MapEvents(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, BindingFlags? bindingFlags)
    {
        ImmutableArray<EventDataModel>.Builder results = ImmutableArray.CreateBuilder<EventDataModel>();
        foreach (ResolvedEventSymbol resolvedEvent in ResolveEvents(type, bindingFlags ?? BindingFlags.Default))
        {
            TypeDataModelGenerationContext scopedCtx = ctx;
            if (MapEvent(resolvedEvent, ref scopedCtx, out EventDataModel eventDataModel) is TypeDataModelGenerationStatus.Success)
            {
                results.Add(eventDataModel);
                ctx = scopedCtx;
            }
        }

        return results.ToImmutable();
    }

    /// <summary>
    /// Maps the given <paramref name="resolvedEvent"/> and its parameters to a <see cref="EventDataModel"/>.
    /// </summary>
    /// <param name="resolvedEvent">The event to try to map.</param>
    /// <param name="ctx">The current model generation context.</param>
    /// <param name="result">The mapped event data model.</param>
    /// <returns>The result of the mapping operation.</returns>
    protected virtual TypeDataModelGenerationStatus MapEvent(ResolvedEventSymbol resolvedEvent, ref TypeDataModelGenerationContext ctx, out EventDataModel result)
    {
        TypeDataModelGenerationStatus status = IncludeNestedType(resolvedEvent.Event.Type, ref ctx);
        if (status != TypeDataModelGenerationStatus.Success)
        {
            result = default;
            return status;
        }

        result = new EventDataModel
        {
            Name = resolvedEvent.CustomName ?? resolvedEvent.Event.Name,
            Event = resolvedEvent.Event,
            IsAmbiguous = resolvedEvent.IsAmbiguous,
        };

        return status;
    }
}
