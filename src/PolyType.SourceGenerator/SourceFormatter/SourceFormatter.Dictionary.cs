using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private static readonly ImmutableArray<ImmutableArray<ConstructionParameterType>> SpanEqualityComparer = ImmutableArray.Create(ImmutableArray.Create(ConstructionParameterType.SpanOfT, ConstructionParameterType.IEqualityComparerOfT));

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

            string typeName = dictionaryType.ImplementationTypeFQN ?? dictionaryType.Type.FullyQualifiedName;
            return FormatCollectionInitializer(dictionaryType.ParameterLists, dictionaryType.KeyType, dictionaryType.StaticFactoryMethod ?? $"new {typeName}({{0}})", null);
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

            string factory = dictionaryType.StaticFactoryMethod is not null
                ? $"{dictionaryType.StaticFactoryMethod}({{0}})"
                : $"new {dictionaryType.Type.FullyQualifiedName}({{0}})";
            return FormatCollectionInitializer(dictionaryType.ParameterLists, dictionaryType.KeyType, factory, valuesExpression: valuesExpr);
        }

        static string FormatSpanConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Span)
            {
                return "null";
            }

            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            string valuesExpr = $"values{suppressSuffix}";

            string factoryExpression = dictionaryType switch
            {
                { StaticFactoryMethod: string factory, CtorRequiresDictionaryConversion: true } => $"{factory}(global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionary({{0}}))",
                { CtorRequiresDictionaryConversion: true } => $"new {dictionaryType.Type.FullyQualifiedName}(global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionary({{0}}))",
                { StaticFactoryMethod: string factory1 } => $"{factory1}({{0}})",
                _ => $"new {dictionaryType.Type.FullyQualifiedName}({{0}})",
            };

            return FormatCollectionInitializer(dictionaryType.CtorRequiresDictionaryConversion ? SpanEqualityComparer : dictionaryType.ParameterLists, dictionaryType.KeyType, factoryExpression, valuesExpression: valuesExpr);
        }
    }
}
