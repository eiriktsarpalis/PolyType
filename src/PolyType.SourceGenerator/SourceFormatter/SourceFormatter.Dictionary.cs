﻿using PolyType.Roslyn;
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
                    DefaultConstructorFunc = {{FormatDefaultConstructorFunc(dictionaryShapeModel)}},
                    OverwritingInserter = {{FormatOverwritingInserter(dictionaryShapeModel)}},
                    DiscardingInserter = {{FormatDiscardingInserter(dictionaryShapeModel)}},
                    ThrowingInserter = {{FormatThrowingInserter(dictionaryShapeModel)}},
                    ParameterizedConstructorFunc = {{FormatParameterizedConstructorFunc(dictionaryShapeModel)}},
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

        static string FormatOverwritingInserter(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
            {
                return "null";
            }

            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.SetItem) != 0)
            {
                return $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, {dictionaryType.KeyType.FullyQualifiedName} key, {dictionaryType.ValueType.FullyQualifiedName} value) => {{ dict[key{suppressSuffix}] = value{suppressSuffix}; return true; }}";
            }

            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.ExplicitIDictionaryOfT) != 0)
            {
                return $"global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionaryInserter<{dictionaryType.Type.FullyQualifiedName}, {dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>(global::PolyType.Abstractions.DictionaryInsertionMode.Overwrite)";
            }

            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.ExplicitIDictionary) != 0)
            {
                return $"global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionaryInserter<{dictionaryType.Type.FullyQualifiedName}>(global::PolyType.Abstractions.DictionaryInsertionMode.Overwrite)!";
            }

            return "null";
        }

        static string FormatDiscardingInserter(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
            {
                return "null";
            }

            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.TryAdd) != 0)
            {
                return $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, {dictionaryType.KeyType.FullyQualifiedName} key, {dictionaryType.ValueType.FullyQualifiedName} value) => dict.TryAdd(key{suppressSuffix}, value{suppressSuffix})";
            }

            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.ContainsKeyAdd) == DictionaryInsertionMode.ContainsKeyAdd)
            {
                return $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, {dictionaryType.KeyType.FullyQualifiedName} key, {dictionaryType.ValueType.FullyQualifiedName} value) => {{ if (dict.ContainsKey(key{suppressSuffix})) return false; dict.Add(key{suppressSuffix}, value{suppressSuffix}); return true; }}";
            }

            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.ExplicitIDictionaryOfT) != 0)
            {
                return $"global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionaryInserter<{dictionaryType.Type.FullyQualifiedName}, {dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>(global::PolyType.Abstractions.DictionaryInsertionMode.Discard)";
            }

            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.ExplicitIDictionary) != 0)
            {
                return $"global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionaryInserter<{dictionaryType.Type.FullyQualifiedName}>(global::PolyType.Abstractions.DictionaryInsertionMode.Discard)!";
            }

            return "null";
        }

        static string FormatThrowingInserter(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
            {
                return "null";
            }

            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.Add) != 0)
            {
                return $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, {dictionaryType.KeyType.FullyQualifiedName} key, {dictionaryType.ValueType.FullyQualifiedName} value) => {{ dict.Add(key{suppressSuffix}, value{suppressSuffix}); return true; }}";
            }

            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.ExplicitIDictionaryOfT) != 0)
            {
                return $"global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionaryInserter<{dictionaryType.Type.FullyQualifiedName}, {dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}>(global::PolyType.Abstractions.DictionaryInsertionMode.Throw)";
            }

            if ((dictionaryType.AvailableInsertionModes & DictionaryInsertionMode.ExplicitIDictionary) != 0)
            {
                return $"global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionaryInserter<{dictionaryType.Type.FullyQualifiedName}>(global::PolyType.Abstractions.DictionaryInsertionMode.Throw)!";
            }

            return "null";
        }

        static string FormatKeyValueTypeName(DictionaryShapeModel dictionaryType)
            => $"global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType}, {dictionaryType.ValueType}>";

        static string FormatParameterizedConstructorFunc(DictionaryShapeModel dictionaryType)
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
