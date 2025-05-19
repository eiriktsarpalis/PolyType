using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatDictionaryTypeShapeFactory(SourceWriter writer, string methodName, DictionaryShapeModel dictionaryShapeModel)
    {
        writer.WriteLine($$"""
            private global::PolyType.Abstractions.ITypeShape<{{dictionaryShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenDictionaryTypeShape<{{dictionaryShapeModel.Type.FullyQualifiedName}}, {{dictionaryShapeModel.KeyType.FullyQualifiedName}}, {{dictionaryShapeModel.ValueType.FullyQualifiedName}}>
                {
                    KeyType = {{GetShapeModel(dictionaryShapeModel.KeyType).SourceIdentifier}},
                    ValueType = {{GetShapeModel(dictionaryShapeModel.ValueType).SourceIdentifier}},
                    GetDictionaryFunc = {{FormatGetDictionaryFunc(dictionaryShapeModel)}},
                    ComparerOptions = {{FormatComparerOptions(dictionaryShapeModel.ConstructionComparer)}},
                    ConstructionStrategy = {{FormatCollectionConstructionStrategy(dictionaryShapeModel.ConstructionStrategy)}},
                    DefaultConstructorFunc = {{FormatDefaultConstructorFunc(dictionaryShapeModel)}},
                    AddKeyValuePairFunc = {{FormatAddKeyValuePairFunc(dictionaryShapeModel)}},
                    EnumerableConstructorFunc = {{FormatEnumerableConstructorFunc(dictionaryShapeModel)}},
                    SpanConstructorFunc = {{FormatSpanConstructorFunc(dictionaryShapeModel)}},
                    Provider = this,
                };
            }
            """, trimNullAssignmentLines: true);

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
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, ImplementationTypeFQN: null }
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => dict[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
                { ConstructionStrategy: CollectionConstructionStrategy.Mutable, ImplementationTypeFQN: { } implementationTypeFQN }
                    => $"static (ref {dictionaryType.Type.FullyQualifiedName} dict, global::System.Collections.Generic.KeyValuePair<{dictionaryType.KeyType.FullyQualifiedName}, {dictionaryType.ValueType.FullyQualifiedName}> kvp) => (({implementationTypeFQN})dict)[kvp.Key{suppressSuffix}] = kvp.Value{suppressSuffix}",
                _ => "null",
            };
        }

        static string FormatEnumerableConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
            {
                return "null";
            }

            string valuesExpr = dictionaryType switch
            {
                { StaticFactoryMethod: not null, IsTupleEnumerableFactory: true } => $"global::System.Linq.Enumerable.Select(values, kvp => new global::System.Tuple<{dictionaryType.KeyType.FullyQualifiedName},{dictionaryType.ValueType.FullyQualifiedName}>(kvp.Key, kvp.Value))",
                { KeyValueTypesContainNullableAnnotations: true } => $"values!",
                _ => $"values"
            };

            return FormatCollectionInitializer(dictionaryType, valuesExpr);
        }

        static string FormatSpanConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Span)
            {
                return "null";
            }

            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            string valuesExpr = $"values{suppressSuffix}";

            if (dictionaryType.CtorRequiresDictionaryConversion)
            {
                string fac = dictionaryType.StaticFactoryMethod is string factory
                    ? $"{factory}(global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionary({{0}}))"
                    : $"new {dictionaryType.Type.FullyQualifiedName}(global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionary({{0}}))";
                return FormatCollectionInitializer(dictionaryType.ConstructionComparer, dictionaryType.KeyType, fac, valuesExpr);
            }

            return FormatCollectionInitializer(dictionaryType, valuesExpr);
        }
    }

    private static string FormatCollectionInitializer(DictionaryShapeModel dictionaryType, string? valuesExpr)
    {
        string factory = dictionaryType.StaticFactoryMethod is not null
          ? $"{dictionaryType.StaticFactoryMethod}({{0}})"
          : $"new {dictionaryType.ImplementationTypeFQN ?? dictionaryType.Type.FullyQualifiedName}({{0}})";
        return FormatCollectionInitializer(dictionaryType.ConstructionComparer, dictionaryType.KeyType, factory, valuesExpr);
    }
}
