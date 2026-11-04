using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.FastPathJson.SourceGenerator.Helpers;
using PolyType.FastPathJson.SourceGenerator.Model;
using PolyType.Roslyn;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;

namespace PolyType.FastPathJson.SourceGenerator;

internal static class Parser
{
    private static readonly DiagnosticDescriptor s_invalidDeclaration = new(
        "FJ0001", "Invalid JSON generator declaration", "Cannot generate JSON for '{0}': {1}",
        "FastPathJson", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor s_unsupportedType = new(
        "FJ0002", "Unsupported JSON generator contract", "Cannot generate JSON for '{0}': {1}",
        "FastPathJson", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static (GenerationModel Model, ImmutableArray<Diagnostic> Diagnostics) Parse(
        Compilation compilation, ImmutableArray<TypeDeclarationSyntax> candidates, CancellationToken token)
    {
        ScalarTypes scalarTypes = new(compilation);
        Dictionary<ITypeSymbol, TypeDataModel> models = new(SymbolEqualityComparer.Default);
        Dictionary<INamedTypeSymbol, List<ITypeSymbol>> roots = new(SymbolEqualityComparer.Default);
        List<(INamedTypeSymbol Holder, ITypeSymbol Type, Location Location)> requests = [];
        List<Diagnostic> diagnostics = [];
        HashSet<INamedTypeSymbol> visited = new(SymbolEqualityComparer.Default);

        foreach (TypeDeclarationSyntax candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            if (compilation.GetSemanticModel(candidate.SyntaxTree).GetDeclaredSymbol(candidate, token) is not INamedTypeSymbol holder ||
                !visited.Add(holder))
            {
                continue;
            }

            foreach (AttributeData attribute in holder.GetAttributes())
            {
                string? name = attribute.AttributeClass?.OriginalDefinition.MetadataName;
                if (attribute.AttributeClass?.ContainingNamespace.ToDisplayString() != "PolyType.FastPathJson" ||
                    name is not ("GenerateJsonAttribute" or "GenerateJsonForAttribute" or "GenerateJsonForAttribute`1"))
                {
                    continue;
                }

                ITypeSymbol? root = name switch
                {
                    "GenerateJsonAttribute" => holder,
                    "GenerateJsonForAttribute`1" => attribute.AttributeClass!.TypeArguments[0],
                    _ => attribute.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol,
                };
                Location location = attribute.ApplicationSyntaxReference?.GetSyntax(token).GetLocation() ?? candidate.GetLocation();
                string? declarationError = ValidateDeclaration(holder, name == "GenerateJsonAttribute", token);
                if (declarationError is not null || root is null)
                {
                    diagnostics.Add(Diagnostic.Create(s_invalidDeclaration, location, holder.Name, declarationError ?? "a closed target type is required"));
                    continue;
                }

                requests.Add((holder, root, location));
            }
        }

        foreach (var request in requests)
        {
            token.ThrowIfCancellationRequested();
            DataModelExtractor extractor = new(compilation, scalarTypes, token);
            TypeDataModelGenerationStatus status = extractor.IncludeType(request.Type);
            string? error = extractor.Errors.FirstOrDefault();
            if (status is not TypeDataModelGenerationStatus.Success)
            {
                error ??= $"'{request.Type}' is unsupported or inaccessible ({status})";
            }

            error ??= extractor.GeneratedModels.Values
                .OrderBy(m => m.Type.GetFullyQualifiedName(), StringComparer.Ordinal)
                .Select(m => ValidateModel(m, compilation, scalarTypes)).FirstOrDefault(e => e is not null);
            error ??= extractor.GeneratedModels.Values.OfType<ObjectDataModel>()
                .Where(m => m.DerivedTypes.Any(d => extractor.GeneratedModels[d.Type] is not ObjectDataModel))
                .Select(m => $"'{m.Type}': only object-shaped hierarchy cases are supported").FirstOrDefault();
            if (error is not null)
            {
                diagnostics.Add(Diagnostic.Create(s_unsupportedType, request.Location, request.Type.ToDisplayString(), error));
                continue;
            }

            if (!roots.TryGetValue(request.Holder, out List<ITypeSymbol>? holderRoots))
            {
                roots.Add(request.Holder, holderRoots = []);
            }

            if (!holderRoots.Contains(request.Type, SymbolEqualityComparer.Default))
            {
                holderRoots.Add(request.Type);
            }

            foreach (var entry in extractor.GeneratedModels)
            {
                models[entry.Key] = entry.Value;
            }
        }

        Dictionary<ITypeSymbol, string> witnesses = new(SymbolEqualityComparer.Default);
        foreach (var entry in roots.OrderBy(r => r.Key.GetFullyQualifiedName(), StringComparer.Ordinal))
        {
            if (!compilation.IsSymbolAccessibleWithin(entry.Key, compilation.Assembly))
            {
                continue;
            }

            foreach (ITypeSymbol root in entry.Value)
            {
                if (!witnesses.ContainsKey(root) || SymbolEqualityComparer.Default.Equals(root, entry.Key))
                {
                    witnesses[root] = entry.Key.GetFullyQualifiedName();
                }
            }
        }

        Dictionary<ITypeSymbol, TypeRef> types = new(SymbolEqualityComparer.Default);
        Dictionary<ITypeSymbol, string> typeIdentifiers = new(SymbolEqualityComparer.Default);
        TypeDataModel[] orderedModels = models.Values.OrderBy(m => m.Type.GetFullyQualifiedName(), StringComparer.Ordinal).ToArray();
        Dictionary<string, int> shortNameCounts = orderedModels
            .GroupBy(m => TypeIdentifierHelpers.CreateTypeIdentifier(m.Type, [], includeNamespaces: false), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
        foreach (TypeDataModel model in orderedModels)
        {
            string shortName = TypeIdentifierHelpers.CreateTypeIdentifier(model.Type, [], includeNamespaces: false);
            string typeIdentifier = shortNameCounts[shortName] == 1
                ? shortName
                : TypeIdentifierHelpers.CreateTypeIdentifier(model.Type, [], includeNamespaces: true);
            string baseName = typeIdentifier + "Converter";
            string identifier = baseName;
            string sourceIdentifier = typeIdentifier;
            int suffix = 1;
            while (!identifiers.Add(identifier))
            {
                string disambiguator = "_" + (suffix++).ToString(System.Globalization.CultureInfo.InvariantCulture);
                identifier = baseName + disambiguator;
                sourceIdentifier = typeIdentifier + disambiguator;
            }

            typeIdentifiers.Add(model.Type, sourceIdentifier);
            witnesses.TryGetValue(model.Type, out string? witness);
            types.Add(model.Type, new TypeRef(model.Type.GetFullyQualifiedName(), ScalarTypes.GeneratedPrefix + identifier,
                model.Type.IsValueType, Witness: witness));
        }

        TypeRef Resolve(ITypeSymbol type)
        {
            TypeRef result = types[type];
            TypeDataModel model = models[type];
            if (scalarTypes.GetConverter(type) is { } scalar)
            {
                return result with
                {
                    Converter = ScalarTypes.Prefix + scalar,
                    SupportsPropertyNames = scalar is not ("Array_ByteConverter" or "BigIntegerConverter" or "RuneConverter"),
                };
            }

            if (model is EnumDataModel)
            {
                return result with { Converter = $"{ScalarTypes.Prefix}EnumConverter<{result.Name}>", SupportsPropertyNames = true };
            }

            if (model is OptionalDataModel optional)
            {
                TypeRef element = Resolve(optional.ElementType);
                return result with { Converter = $"{ScalarTypes.Prefix}NullableConverter<{element.Name}, {element.WitnessType}>" };
            }

            if (model is EnumerableDataModel enumerable && enumerable.EnumerableKind is EnumerableKind.ArrayOfT)
            {
                TypeRef element = Resolve(enumerable.ElementType);
                return result with { ReadConverter = $"{ScalarTypes.Prefix}ArrayConverter<{element.Name}, {element.ReadWitnessType}>" };
            }

            if (model is EnumerableDataModel list && IsDefinition(type, "System.Collections.Generic", "List`1"))
            {
                TypeRef element = Resolve(list.ElementType);
                return result with { ReadConverter = $"{ScalarTypes.Prefix}ListConverter<{element.Name}, {element.ReadWitnessType}>" };
            }

            if (model is DictionaryDataModel dictionary && IsDefinition(type, "System.Collections.Generic", "Dictionary`2"))
            {
                TypeRef key = Resolve(dictionary.KeyType), value = Resolve(dictionary.ValueType);
                return result with { ReadConverter = $"{ScalarTypes.Prefix}DictionaryConverter<{key.Name}, {value.Name}, {key.ReadWitnessType}, {value.ReadWitnessType}>" };
            }

            return result;
        }

        List<ConverterModel> converters = [];
        foreach (TypeDataModel model in orderedModels)
        {
            TypeRef type = Resolve(model.Type);
            if (type.Converter.StartsWith(ScalarTypes.GeneratedPrefix, StringComparison.Ordinal))
            {
                converters.Add(MapModel(model, type, Resolve));
            }
        }

        List<RootModel> rootModels = [];
        Dictionary<string, int> rootNameCounts = roots.Keys
            .GroupBy(t => t.CreateTypeIdentifier([], includeNamespaces: false), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        HashSet<string> rootIdentifiers = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> sourceIdentifiers = new(
            converters.Where(c => c.Type.Witness is null)
                .Select(c => "Converters." + c.Type.Converter.Substring(ScalarTypes.GeneratedPrefix.Length)),
            StringComparer.OrdinalIgnoreCase);
        foreach (var entry in roots.OrderBy(r => r.Key.GetFullyQualifiedName(), StringComparer.Ordinal))
        {
            string shortName = entry.Key.CreateTypeIdentifier([], includeNamespaces: false);
            string baseName = rootNameCounts[shortName] == 1
                ? shortName
                : entry.Key.CreateTypeIdentifier([], includeNamespaces: true);
            string identifier = baseName;
            int suffix = 1;
            while (!rootIdentifiers.Add(identifier))
            {
                identifier = baseName + "_" + (suffix++).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            List<string> declarations = [];
            for (INamedTypeSymbol? current = entry.Key; current is not null; current = current.ContainingType)
            {
                var syntax = (TypeDeclarationSyntax)current.DeclaringSyntaxReferences[0].GetSyntax(token);
                string kind = syntax is RecordDeclarationSyntax record
                    ? "record" + (record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? " struct" : "")
                    : syntax.Keyword.Text;
                declarations.Add($"{syntax.Modifiers} {kind} {syntax.Identifier.Text}".Trim());
            }

            declarations.Reverse();
            string? ns = entry.Key.ContainingNamespace.IsGlobalNamespace ? null : entry.Key.ContainingNamespace.ToDisplayString();
            var typeDeclarations = declarations.ToImmutableEquatableArray();
            foreach (ITypeSymbol root in entry.Value.OrderBy(t => t.GetFullyQualifiedName(), StringComparer.Ordinal))
            {
                string sourceBaseName = SymbolEqualityComparer.Default.Equals(root, entry.Key)
                    ? identifier
                    : identifier + "." + typeIdentifiers[root];
                string sourceIdentifier = sourceBaseName;
                int sourceSuffix = 1;
                while (!sourceIdentifiers.Add(sourceIdentifier))
                {
                    sourceIdentifier = sourceBaseName + "_" + (sourceSuffix++).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                rootModels.Add(new RootModel(sourceIdentifier, ns, typeDeclarations, Resolve(root)));
            }
        }

        return (new GenerationModel(converters.ToImmutableEquatableArray(), rootModels.ToImmutableEquatableArray()), diagnostics.ToImmutableArray());
    }

    private static string? ValidateDeclaration(INamedTypeSymbol holder, bool isTarget, CancellationToken token)
    {
        if (holder.ContainsGenericParameters() || holder.IsStatic || holder.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return isTarget && holder.TypeKind is TypeKind.Interface
                ? "interface roots require a separate witness type"
                : "the annotated type must be a non-static, non-generic class or struct";
        }

        for (INamedTypeSymbol? type = holder; type is not null; type = type.ContainingType)
        {
            foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(token) is not TypeDeclarationSyntax declaration ||
                    !declaration.Modifiers.Any(SyntaxKind.PartialKeyword) ||
                    declaration.Modifiers.Any(m => m.ValueText == "file"))
                {
                    return "the annotated type and its containing types must be partial and must not be file-local";
                }
            }
        }

        return null;
    }

    private static string? ValidateModel(TypeDataModel model, Compilation compilation, ScalarTypes scalarTypes)
    {
        string Unsupported(string reason) => $"'{model.Type}': {reason}";
        if (model is EnumDataModel or TupleDataModel || scalarTypes.Contains(model.Type))
        {
            return null;
        }

        switch (model)
        {
            case OptionalDataModel when model.Type.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T:
                return null;
            case ObjectDataModel obj:
                if (obj.Constructors.IsEmpty && obj.DerivedTypes.IsEmpty)
                {
                    return Unsupported("no accessible constructor is available");
                }

                if (obj.Properties.Any(p => p.IsAmbiguous || p.IncludeGetter && !p.IsGetterAccessible || p.IncludeSetter && !p.IsSetterAccessible))
                {
                    return Unsupported("inaccessible or ambiguous members require accessors that this prototype does not generate");
                }

                if (obj.Properties.GroupBy(p => p.LogicalName ?? p.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
                {
                    return Unsupported("duplicate JSON member names are not supported");
                }

                if (obj.Constructors.Any(c => !compilation.IsSymbolAccessibleWithin(c.Constructor, compilation.Assembly) ||
                    c.Constructor.Parameters.Any(p => p.RefKind is not RefKind.None)))
                {
                    return Unsupported("inaccessible constructors and ref/in/out parameters are not supported");
                }

                if (obj.Constructors.Any(c => c.MemberInitializers.Any(p =>
                    (p.IsInitOnly || p.IsRequiredBySyntax) && !(p.IsRequiredByPolicy ?? p.IsRequiredBySyntax))))
                {
                    return Unsupported("optional init-only members must be bound to constructor parameters to preserve omitted initializer values");
                }

                if (obj.DerivedTypes.Any(d => d.Type.TypeKind is not (TypeKind.Class or TypeKind.Interface)))
                {
                    return Unsupported("only object-shaped hierarchy cases are supported");
                }

                return null;
            case EnumerableDataModel enumerable when enumerable.DerivedTypes.IsEmpty:
                if (enumerable.EnumerableKind is EnumerableKind.ArrayOfT or EnumerableKind.MultiDimensionalArrayOfT)
                {
                    return null;
                }

                if (enumerable.EnumerableKind is not EnumerableKind.IEnumerableOfT)
                {
                    return Unsupported("this enumerable kind is outside the prototype");
                }

                if (enumerable.Type.IsValueType && enumerable.InsertionMode is EnumerableInsertionMode.ExplicitICollectionOfT)
                {
                    return Unsupported("mutating a boxed struct collection is not supported");
                }

                return ValidateFactory(enumerable.FactoryMethod, enumerable.FactorySignature, model.Type);
            case DictionaryDataModel dictionary when dictionary.DerivedTypes.IsEmpty:
                if (dictionary.DictionaryKind is DictionaryKind.IDictionary ||
                    dictionary.Type.IsValueType && !dictionary.AvailableInsertionModes.HasFlag(DictionaryInsertionMode.SetItem))
                {
                    return Unsupported("non-generic or explicitly implemented struct dictionaries are outside the prototype");
                }

                return ValidateFactory(dictionary.FactoryMethod, dictionary.FactorySignature, model.Type);
            default:
                return Unsupported($"model kind '{model.Kind}' is outside the prototype");
        }
    }

    private static string? ValidateFactory(IMethodSymbol? factory, ImmutableArray<CollectionConstructorParameter> signature, ITypeSymbol type)
    {
        if (factory is null || signature.Contains(CollectionConstructorParameter.Unrecognized) ||
            signature.Contains(CollectionConstructorParameter.TupleEnumerable) ||
            factory.ContainingNamespace.ToDisplayString() == "PolyType.SourceGenModel")
        {
            return $"'{type}': no supported, runtime-independent collection construction strategy is available";
        }

        return null;
    }

    private static ConverterModel MapModel(TypeDataModel model, TypeRef type, Func<ITypeSymbol, TypeRef> resolve) => model switch
    {
        ObjectDataModel obj => MapObject(obj, type, resolve),
        TupleDataModel tuple => MapTuple(tuple, type, resolve),
        EnumerableDataModel enumerable when type.ReadConverter is not null =>
            new BuiltInCollectionConverterModel(type, resolve(enumerable.ElementType)),
        DictionaryDataModel dictionary when type.ReadConverter is not null =>
            new BuiltInCollectionConverterModel(type, resolve(dictionary.ValueType), resolve(dictionary.KeyType)),
        EnumerableDataModel { EnumerableKind: EnumerableKind.MultiDimensionalArrayOfT } array =>
            MapArray(array, type, resolve),
        EnumerableDataModel enumerable => MapEnumerable(enumerable, type, resolve),
        DictionaryDataModel dictionary => MapDictionary(dictionary, type, resolve),
        _ => throw new InvalidOperationException("A model was mapped without validating its kind."),
    };

    private static ObjectConverterModel MapObject(ObjectDataModel model, TypeRef type, Func<ITypeSymbol, TypeRef> resolve)
    {
        var properties = model.Properties.OrderBy(p => p.Order)
            .Where(p => p.IncludeGetter)
            .Select(p => new WriteProperty(p.LogicalName ?? p.Name,
                model.Type.IsReferenceType && SymbolEqualityComparer.Default.Equals(model.Type, p.DeclaringType)
                    ? $"value.{TypeIdentifierHelpers.EscapeKeywordIdentifier(p.Name)}"
                    : $"(({p.DeclaringType.GetFullyQualifiedName()})value).{TypeIdentifierHelpers.EscapeKeywordIdentifier(p.Name)}",
                resolve(p.PropertyType), p.IsGetterNonNullable && p.PropertyType.IsReferenceType)).ToImmutableEquatableArray();
        List<ReadSlot> slots = [];
        List<MemberAssignment> assignments = [];
        string? construction = null;

        if (model.Constructors.FirstOrDefault() is { Constructor: not null } constructor)
        {
            List<string> arguments = [], initializers = [];
            foreach (ParameterDataModel parameter in constructor.Parameters)
            {
                IParameterSymbol p = parameter.Parameter;
                PropertyDataModel? property = DataModelExtractor.FindProperty(model.Properties, p);
                string name = property?.LogicalName ?? property?.Name ?? p.Name;
                bool required = property?.IsRequiredByPolicy is true || !parameter.HasDefaultValue;
                arguments.Add($"arg{slots.Count}!");
                slots.Add(new ReadSlot(name, resolve(p.Type), parameter.DefaultValueExpr ?? "default", required, parameter.IsNonNullable && p.Type.IsReferenceType));
            }

            foreach (PropertyDataModel property in constructor.MemberInitializers)
            {
                string name = property.LogicalName ?? property.Name;
                int slot = slots.FindIndex(s => s.Name == name);
                if (slot < 0)
                {
                    slot = slots.Count;
                    slots.Add(new ReadSlot(name, resolve(property.PropertyType), "default",
                        property.IsRequiredByPolicy ?? property.IsRequiredBySyntax,
                        property.IsSetterNonNullable && property.PropertyType.IsReferenceType));
                }

                if (property.IsInitOnly || property.IsRequiredBySyntax)
                {
                    initializers.Add($"{TypeIdentifierHelpers.EscapeKeywordIdentifier(property.Name)} = arg{slot}!");
                }
                else
                {
                    string access = model.Type.IsValueType || SymbolEqualityComparer.Default.Equals(model.Type, property.DeclaringType)
                        ? $"result.{TypeIdentifierHelpers.EscapeKeywordIdentifier(property.Name)}"
                        : $"(({property.DeclaringType.GetFullyQualifiedName()})result).{TypeIdentifierHelpers.EscapeKeywordIdentifier(property.Name)}";
                    assignments.Add(new MemberAssignment(access, slot));
                }
            }

            construction = $"new {type.Name}({string.Join(", ", arguments)})";
            if (initializers.Count > 0)
            {
                construction += " { " + string.Join(", ", initializers) + " }";
            }
        }

        return new ObjectConverterModel(type, properties, slots.ToImmutableEquatableArray(), construction, assignments.ToImmutableEquatableArray(),
            model.DerivedTypes.Select(d => new UnionCase(resolve(d.Type), d.Name, d.IsBaseType)).ToImmutableEquatableArray());
    }

    private static ObjectConverterModel MapTuple(TupleDataModel model, TypeRef type, Func<ITypeSymbol, TypeRef> resolve)
    {
        List<WriteProperty> properties = [];
        List<ReadSlot> slots = [];
        for (int i = 0; i < model.Elements.Length; i++)
        {
            PropertyDataModel element = model.Elements[i];
            string access = model.IsValueTuple
                ? $"value.{TypeIdentifierHelpers.EscapeKeywordIdentifier(element.Name)}"
                : "value." + string.Concat(Enumerable.Repeat("Rest.", i / 7)) + $"Item{i % 7 + 1}";
            TypeRef elementType = resolve(element.PropertyType);
            properties.Add(new WriteProperty("Item" + (i + 1), access, elementType, false));
            slots.Add(new ReadSlot("Item" + (i + 1), elementType, "default", true, false));
        }

        string construction;
        if (model.IsValueTuple)
        {
            construction = "(" + string.Join(", ", Enumerable.Range(0, slots.Count).Select(i => $"arg{i}!")) + ")";
        }
        else
        {
            int offset = 0;
            construction = ConstructTuple((INamedTypeSymbol)model.Type, ref offset);
        }

        return new ObjectConverterModel(type, properties.ToImmutableEquatableArray(), slots.ToImmutableEquatableArray(),
            construction, [], []);

        static string ConstructTuple(INamedTypeSymbol tuple, ref int offset)
        {
            List<string> arguments = [];
            for (int i = 0; i < tuple.TypeArguments.Length; i++)
            {
                arguments.Add(i == 7 ? ConstructTuple((INamedTypeSymbol)tuple.TypeArguments[i], ref offset) : $"arg{offset++}!");
            }

            return $"new {tuple.GetFullyQualifiedName()}({string.Join(", ", arguments)})";
        }
    }

    private static EnumerableConverterModel MapEnumerable(EnumerableDataModel model, TypeRef type, Func<ITypeSymbol, TypeRef> resolve)
    {
        TypeRef element = resolve(model.ElementType);
        string? append = model.AppendMethod is null ? null : model.InsertionMode switch
        {
            EnumerableInsertionMode.ExplicitICollectionOfT => $"((global::System.Collections.Generic.ICollection<{element.Name}>)result).Add(element);",
            _ => $"result.{TypeIdentifierHelpers.EscapeKeywordIdentifier(model.AppendMethod.Name)}(element);",
        };
        string construction = FactoryCall(model.FactoryMethod!, model.FactorySignature, element.Name, null, append is not null);
        bool requiresInterfaceEnumeration = !HasEnumerationPattern(model.Type,
            elementType => SymbolEqualityComparer.Default.Equals(elementType, model.ElementType));
        return new EnumerableConverterModel(type, element, construction, append, requiresInterfaceEnumeration);
    }

    private static ArrayConverterModel MapArray(EnumerableDataModel model, TypeRef type, Func<ITypeSymbol, TypeRef> resolve)
    {
        string suffix = "";
        ITypeSymbol element = model.ElementType;
        while (element is IArrayTypeSymbol array)
        {
            suffix += "[" + new string(',', array.Rank - 1) + "]";
            element = array.ElementType;
        }

        string lengths = string.Join(", ", Enumerable.Range(0, model.Rank).Select(d => "n" + d));
        string construction = $"new {element.GetFullyQualifiedName()}[{lengths}]{suffix}";
        return new ArrayConverterModel(type, resolve(model.ElementType), model.Rank, construction);
    }

    private static DictionaryConverterModel MapDictionary(DictionaryDataModel model, TypeRef type, Func<ITypeSymbol, TypeRef> resolve)
    {
        TypeRef key = resolve(model.KeyType), value = resolve(model.ValueType);
        bool parameterized = model.FactorySignature.Any(p => p is CollectionConstructorParameter.Dictionary or CollectionConstructorParameter.List or CollectionConstructorParameter.Span);
        string? insert = parameterized ? null : model.AvailableInsertionModes.HasFlag(DictionaryInsertionMode.SetItem)
            ? "result[key] = element;"
            : $"((global::System.Collections.Generic.IDictionary<{key.Name}, {value.Name}>)result)[key] = element;";
        string construction = FactoryCall(model.FactoryMethod!, model.FactorySignature, key.Name, value.Name, insert is not null);
        bool requiresInterfaceEnumeration = !HasEnumerationPattern(model.Type, elementType =>
            elementType is INamedTypeSymbol { TypeArguments: [var keyType, var valueType] } &&
            IsDefinition(elementType, "System.Collections.Generic", "KeyValuePair`2") &&
            SymbolEqualityComparer.Default.Equals(keyType, model.KeyType) &&
            SymbolEqualityComparer.Default.Equals(valueType, model.ValueType));
        return new DictionaryConverterModel(type, key, value, construction, insert, requiresInterfaceEnumeration);
    }

    private static bool HasEnumerationPattern(ITypeSymbol type, Func<ITypeSymbol, bool> isElementType)
    {
        IMethodSymbol[] getEnumerators = type.ResolveVisibleMembers<IMethodSymbol>()
            .Where(m => !m.IsAmbiguous && m.Symbol is
            {
                Name: "GetEnumerator", DeclaredAccessibility: Accessibility.Public,
                IsStatic: false, IsGenericMethod: false, Parameters.IsEmpty: true,
            })
            .Select(m => m.Symbol)
            .ToArray();
        if (getEnumerators is not [var getEnumerator])
        {
            return false;
        }

        ITypeSymbol enumerator = getEnumerator.ReturnType;

        return enumerator.ResolveVisibleMembers<IPropertySymbol>().Any(p => !p.IsAmbiguous && p.Symbol is
            {
                Name: "Current", IsStatic: false, IsIndexer: false,
                GetMethod.DeclaredAccessibility: Accessibility.Public,
            } && isElementType(p.Symbol.Type)) &&
            enumerator.ResolveVisibleMembers<IMethodSymbol>().Any(m => !m.IsAmbiguous && m.Symbol is
            {
                Name: "MoveNext", DeclaredAccessibility: Accessibility.Public,
                IsStatic: false, IsGenericMethod: false, Parameters.IsEmpty: true,
                ReturnType.SpecialType: SpecialType.System_Boolean,
            });
    }

    private static string FactoryCall(IMethodSymbol factory, ImmutableArray<CollectionConstructorParameter> signature, string key, string? value, bool mutable)
    {
        string elements = value is null ? "items" : "global::System.Linq.Enumerable.ToArray(items)";
        string[] arguments = signature.Select(p => p switch
        {
            CollectionConstructorParameter.List => elements,
            CollectionConstructorParameter.Dictionary => "items",
            CollectionConstructorParameter.Span => value is null
                ? "global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(items)"
                : "global::System.MemoryExtensions.AsSpan(" + elements + ")",
            CollectionConstructorParameter.HashSet => $"new global::System.Collections.Generic.HashSet<{key}>(items)",
            CollectionConstructorParameter.Capacity or CollectionConstructorParameter.CapacityOptional => mutable ? "0" : "items.Count",
            CollectionConstructorParameter.EqualityComparer or CollectionConstructorParameter.EqualityComparerOptional => $"global::System.Collections.Generic.EqualityComparer<{key}>.Default",
            CollectionConstructorParameter.Comparer or CollectionConstructorParameter.ComparerOptional => $"global::System.Collections.Generic.Comparer<{key}>.Default",
            _ => throw new InvalidOperationException("An unsupported collection factory was not diagnosed."),
        }).ToArray();
        string target = factory.MethodKind is MethodKind.Constructor
            ? "new " + factory.ContainingType.GetFullyQualifiedName()
            : factory.ContainingType.GetFullyQualifiedName() + "." + TypeIdentifierHelpers.EscapeKeywordIdentifier(factory.Name) +
                (factory.IsGenericMethod ? "<" + string.Join(", ", factory.TypeArguments.Select(t => t.GetFullyQualifiedName())) + ">" : "");
        return target + "(" + string.Join(", ", arguments) + ")";
    }

    private static bool IsDefinition(ITypeSymbol type, string ns, string metadataName) =>
        type is INamedTypeSymbol named && named.MetadataName == metadataName && named.ContainingNamespace.ToDisplayString() == ns;
}
