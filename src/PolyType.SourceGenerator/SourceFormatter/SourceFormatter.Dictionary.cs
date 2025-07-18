using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatDictionaryTypeShapeFactory(SourceWriter writer, string methodName, DictionaryShapeModel dictionaryShapeModel)
    {
        bool requiresCS8631Suppression = dictionaryShapeModel.KeyValueTypesContainNullableAnnotations && dictionaryShapeModel.Kind is DictionaryKind.IDictionaryOfKV;
        if (requiresCS8631Suppression)
        {
            // Need to emit a call to CollectionHelpers.AsReadOnlyDictionary<TDictionary, TKey, TValue>(...) which creates a nullability warning on the type parameters
            writer.WriteLine("#pragma warning disable CS8631 // Nullability of type argument doesn't match constraint type.", disableIndentation: true);
        }

        writer.WriteLine($$"""
            private global::PolyType.Abstractions.ITypeShape<{{dictionaryShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenDictionaryTypeShape<{{dictionaryShapeModel.Type.FullyQualifiedName}}, {{dictionaryShapeModel.KeyType.FullyQualifiedName}}, {{dictionaryShapeModel.ValueType.FullyQualifiedName}}>
                {
                    KeyType = {{GetShapeModel(dictionaryShapeModel.KeyType).SourceIdentifier}},
                    ValueType = {{GetShapeModel(dictionaryShapeModel.ValueType).SourceIdentifier}},
                    GetDictionaryFunc = {{FormatGetDictionaryFunc(dictionaryShapeModel)}},
                    SupportedComparer = {{FormatComparerOptions(dictionaryShapeModel.ConstructorParameters)}},
                    ConstructionStrategy = {{FormatCollectionConstructionStrategy(dictionaryShapeModel.ConstructionStrategy)}},
                    MutableConstructorFunc = {{FormatDefaultConstructorFunc(dictionaryShapeModel)}},
                    AddKeyValuePairFunc = {{FormatAddKeyValuePairFunc(dictionaryShapeModel)}},
                    SpanConstructorFunc = {{FormatSpanConstructorFunc(dictionaryShapeModel)}},
                    AssociatedTypeShapes = {{FormatAssociatedTypeShapes(dictionaryShapeModel)}},
                    Provider = this,
                };
            }
            """, trimNullAssignmentLines: true);

        if (requiresCS8631Suppression)
        {
            writer.WriteLine("#pragma warning restore CS8631 // Nullability of type argument doesn't match constraint type.", disableIndentation: true);
        }

        static string FormatGetDictionaryFunc(DictionaryShapeModel dictionaryType)
        {
            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            return dictionaryType.Kind switch
            {
                DictionaryKind.IReadOnlyDictionaryOfKV => $"static obj => obj{suppressSuffix}",
                DictionaryKind.IDictionaryOfKV => $"static obj => global::PolyType.SourceGenModel.CollectionHelpers.AsReadOnlyDictionary<{dictionaryType.Type.FullyQualifiedName}, {dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>(obj{suppressSuffix})",
                DictionaryKind.IDictionary => $"static obj => global::PolyType.SourceGenModel.CollectionHelpers.AsReadOnlyDictionary(obj{suppressSuffix})!",
                _ => throw new ArgumentException(dictionaryType.Kind.ToString()),
            };
        }

        static string FormatDefaultConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
            {
                return "null";
            }

            return FormatCollectionInitializer(dictionaryType, null);
        }

        static string FormatAddKeyValuePairFunc(DictionaryShapeModel dictionaryType)
        {
            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            return dictionaryType switch
            {
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, ImplementationTypeFQN: null, IndexerIsExplicitInterfaceImplementation: false }
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => dict[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, ImplementationTypeFQN: { } implementationTypeFQN }
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => (({implementationTypeFQN})dict)[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, IndexerIsExplicitInterfaceImplementation: true, Kind: DictionaryKind.IDictionary }
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => ((global::System.Collections.IDictionary)dict{suppressSuffix})[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, IndexerIsExplicitInterfaceImplementation: true, Kind: DictionaryKind.IDictionaryOfKV }
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => ((global::System.Collections.Generic.IDictionary<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>)dict{suppressSuffix})[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
                _ => "null",
            };
        }

        static string FormatKeyValueTypeName(DictionaryShapeModel dictionaryType)
            => $"global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType}, {dictionaryType.ValueType}>";

        static string FormatSpanConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Parameterized)
            {
                return "null";
            }

            string valuesType = FormatKeyValueTypeName(dictionaryType);
            return FormatCollectionInitializer(dictionaryType, valuesType);
        }
    }

    private static string FormatCollectionInitializer(DictionaryShapeModel dictionaryType, string? valuesType)
    {
        string factory = dictionaryType.StaticFactoryMethod is not null
          ? $"{dictionaryType.StaticFactoryMethod}({{0}})"
          : $"new {dictionaryType.ImplementationTypeFQN ?? dictionaryType.Type.FullyQualifiedName}({{0}})";
        return FormatCollectionInitializer(dictionaryType.ConstructorParameters, dictionaryType.KeyType, dictionaryType.ValueType, factory, valuesType, dictionaryType.KeyValueTypesContainNullableAnnotations);
    }
}
