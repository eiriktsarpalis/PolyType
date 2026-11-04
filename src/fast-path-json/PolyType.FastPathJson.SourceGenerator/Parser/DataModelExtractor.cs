using Microsoft.CodeAnalysis;
using PolyType.FastPathJson.SourceGenerator.Helpers;
using PolyType.Roslyn;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;
using System.Reflection;

namespace PolyType.FastPathJson.SourceGenerator;

internal sealed class DataModelExtractor(Compilation compilation, ScalarTypes scalarTypes, CancellationToken token)
    : TypeDataModelGenerator(compilation.Assembly, new KnownSymbols(compilation), token)
{
    public List<string> Errors { get; } = [];

    protected override bool FlattenSystemTupleTypes => true;
    protected override ITypeSymbol NormalizeType(ITypeSymbol type) => type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
    protected override bool IsSupportedType(ITypeSymbol type) =>
        base.IsSupportedType(type) && !type.IsRefLikeType && !type.IsStatic && type.TypeKind is not TypeKind.FunctionPointer;

    protected override bool OnTypeTraversalStarting(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_Object || type.TypeKind is TypeKind.Delegate ||
            type is INamedTypeSymbol named && named.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.FSharp", StringComparison.Ordinal))
        {
            Errors.Add($"'{type}' requires open object dispatch or function/F# support, which is outside this prototype");
            return false;
        }

        return true;
    }

    protected override TypeDataModelGenerationStatus MapType(
        ITypeSymbol type, TypeDataKind? requestedKind, BindingFlags? methodBindingFlags,
        ImmutableArray<AssociatedTypeModel> associatedTypes, ref TypeDataModelGenerationContext ctx,
        TypeShapeRequirements requirements, out TypeDataModel? model)
    {
        if (scalarTypes.Contains(type))
        {
            model = new TypeDataModel { Type = type, Requirements = requirements };
            return TypeDataModelGenerationStatus.Success;
        }

        return base.MapType(type, requestedKind, methodBindingFlags, associatedTypes, ref ctx, requirements, out model);
    }

    protected override bool IncludeProperty(IPropertySymbol property, out string? customName, out int order, out bool includeGetter, out bool includeSetter)
    {
        bool included = base.IncludeProperty(property, out customName, out order, out includeGetter, out includeSetter);
        if (FindAttribute(property, "System.Text.Json.Serialization.JsonIncludeAttribute") is not null)
        {
            included = true;
            includeGetter = property.GetMethod is not null;
            includeSetter = property.SetMethod is not null;
        }

        included = ApplyJsonMetadata(property, included, out customName, out order);
        CheckMemberType(property, property.Type, included);
        return included;
    }

    protected override bool IncludeField(IFieldSymbol field, out string? customName, out int order, out bool includeGetter, out bool includeSetter)
    {
        bool included = base.IncludeField(field, out customName, out order, out includeGetter, out includeSetter);
        if (FindAttribute(field, "System.Text.Json.Serialization.JsonIncludeAttribute") is not null)
        {
            included = true;
            includeGetter = true;
            includeSetter = !field.IsReadOnly;
        }

        included = ApplyJsonMetadata(field, included, out customName, out order);
        CheckMemberType(field, field.Type, included);
        return included;
    }

    protected override bool? IsRequiredByPolicy(IPropertySymbol member) =>
        FindAttribute(member, "System.Text.Json.Serialization.JsonRequiredAttribute") is not null ? true : null;

    protected override bool? IsRequiredByPolicy(IFieldSymbol member) =>
        FindAttribute(member, "System.Text.Json.Serialization.JsonRequiredAttribute") is not null ? true : null;

    protected override IEnumerable<IMethodSymbol> ResolveConstructors(ITypeSymbol type, ImmutableArray<PropertyDataModel> properties)
    {
        if (type.IsAbstract || type.TypeKind is TypeKind.Interface)
        {
            return [];
        }

        IMethodSymbol[] annotated = type.GetMembers().OfType<IMethodSymbol>()
            .Where(c => c.MethodKind is MethodKind.Constructor &&
                FindAttribute(c, "System.Text.Json.Serialization.JsonConstructorAttribute") is not null).ToArray();
        if (annotated.Length > 0)
        {
            if (annotated.Length != 1)
            {
                Errors.Add($"'{type}' has multiple annotated constructors");
            }

            return annotated.Take(1);
        }

        return base.ResolveConstructors(type, properties)
            .Where(c => c.DeclaredAccessibility is Accessibility.Public)
            .OrderByDescending(c =>
            {
                int unmatched = 0, readOnly = 0;
                foreach (IParameterSymbol parameter in c.Parameters)
                {
                    PropertyDataModel? match = FindProperty(properties, parameter);
                    unmatched += match is null && !parameter.IsOptional ? 1 : 0;
                    readOnly += match is { IncludeGetter: true, IncludeSetter: false } ? 1 : 0;
                }

                return (-unmatched, readOnly, -c.Parameters.Length);
            })
            .Take(1);
    }

    protected override IEnumerable<DerivedTypeModel> ResolveDerivedTypes(ITypeSymbol type)
    {
        AttributeData? polymorphic = FindAttribute(type, "System.Text.Json.Serialization.JsonPolymorphicAttribute");
        if (GetNamedValue(polymorphic, "TypeDiscriminatorPropertyName") is string discriminator && discriminator != "$type" ||
            GetNamedValue(polymorphic, "IgnoreUnrecognizedTypeDiscriminators") is true ||
            GetNamedValue(polymorphic, "UnknownDerivedTypeHandling") is int handling && handling != 0)
        {
            Errors.Add($"'{type}' requests unsupported polymorphism options; this prototype uses a '$type' string discriminator");
            yield break;
        }

        int index = 0;
        HashSet<string> names = new(StringComparer.Ordinal);
        HashSet<ITypeSymbol> types = new(SymbolEqualityComparer.Default);
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Text.Json.Serialization.JsonDerivedTypeAttribute")
            {
                continue;
            }

            if (attribute.ConstructorArguments.FirstOrDefault().Value is not ITypeSymbol derived ||
                derived.ContainsGenericParameters() || !type.IsAssignableFrom(derived))
            {
                Errors.Add($"'{type}' has an unsupported derived-type registration");
                continue;
            }

            if (attribute.ConstructorArguments.Length != 2 || attribute.ConstructorArguments[1].Value is not string name)
            {
                Errors.Add($"'{type}' requires string derived-type discriminators");
                continue;
            }

            if (!names.Add(name) || !types.Add(derived))
            {
                Errors.Add($"'{type}' has duplicate derived-type metadata");
                continue;
            }

            yield return new DerivedTypeModel
            {
                Type = derived,
                Name = name,
                Tag = index,
                IsTagSpecified = false,
                Index = index++,
                IsBaseType = SymbolComparer.Equals(type, derived),
            };
        }
    }

    private bool ApplyJsonMetadata(ISymbol member, bool included, out string? customName, out int order)
    {
        customName = FindAttribute(member, "System.Text.Json.Serialization.JsonPropertyNameAttribute")?.ConstructorArguments.FirstOrDefault().Value as string;
        order = FindAttribute(member, "System.Text.Json.Serialization.JsonPropertyOrderAttribute")?.ConstructorArguments.FirstOrDefault().Value as int? ?? 0;
        if (FindAttribute(member, "System.Text.Json.Serialization.JsonIgnoreAttribute") is { } ignored)
        {
            int condition = GetNamedValue(ignored, "Condition") as int? ?? 1;
            if (condition == 1)
            {
                return false;
            }

            if (condition != 0)
            {
                Errors.Add($"member '{member}' requests conditional JSON omission, which is outside this prototype");
                return false;
            }
        }

        return included;
    }

    private void CheckMemberType(ISymbol member, ITypeSymbol type, bool included)
    {
        if (included && (!IsSupportedType(type) || !IsAccessibleSymbol(type)))
        {
            Errors.Add($"member '{member}' has an unsupported or inaccessible type");
        }
    }

    public static PropertyDataModel? FindProperty(IEnumerable<PropertyDataModel> properties, IParameterSymbol parameter)
    {
        foreach (PropertyDataModel property in properties)
        {
            if (SymbolEqualityComparer.Default.Equals(property.PropertyType, parameter.Type) &&
                property.Name.Length == parameter.Name.Length &&
                char.ToUpperInvariant(property.Name[0]) == char.ToUpperInvariant(parameter.Name[0]) &&
                string.Equals(property.Name.Substring(1), parameter.Name.Substring(1), StringComparison.Ordinal))
            {
                return property;
            }
        }

        return null;
    }

    public static AttributeData? FindAttribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);

    public static object? GetNamedValue(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(p => p.Key == name).Value.Value;
}
