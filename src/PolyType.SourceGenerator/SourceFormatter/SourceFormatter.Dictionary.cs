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
            ImmutableArray<ConstructionParameterType> parametersWithComparer = dictionaryType.ParameterLists.FirstOrDefault(list => list.Contains(ConstructionParameterType.IEqualityComparerOfT));

            if (parametersWithComparer.IsDefault)
            {
                return $"static options => static () => new {typeName}()";
            }

            string args = parametersWithComparer switch
            {
                [ConstructionParameterType.IEqualityComparerOfT] => "options.EqualityComparer",
                _ => ""
            };

            return $"static options => options is null" +
                $" ? () => new {typeName}()" +
                $" : () => new {typeName}({args})";
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
                { StaticFactoryMethod: string factory, IsTupleEnumerableFactory: true } => $"global::System.Linq.Enumerable.Select(values, kvp => new global::System.Tuple<{dictionaryType.KeyType.FullyQualifiedName},{dictionaryType.ValueType.FullyQualifiedName}>(kvp.Key, kvp.Value))",
                { KeyValueTypesContainNullableAnnotations: true } => $"values!",
                _ => $"values"
            };

            ImmutableArray<ConstructionParameterType> parametersWithComparer = dictionaryType.ParameterLists.FirstOrDefault(list => list.Contains(ConstructionParameterType.IEqualityComparerOfT));

            if (parametersWithComparer.IsDefault)
            {
                return dictionaryType switch
                {
                    { StaticFactoryMethod: string factory } => $"static options => static values => {factory}({valuesExpr})",
                    _ => $"static options => static values => new {dictionaryType.Type.FullyQualifiedName}({valuesExpr})",
                };
            }

            string optionArgsExpr = parametersWithComparer switch
            {
                [ConstructionParameterType.IEnumerableOfT, ConstructionParameterType.IEqualityComparerOfT] => $"{valuesExpr}, options.EqualityComparer",
                [ConstructionParameterType.IEnumerableOfT] => valuesExpr,
                _ => throw new InvalidOperationException("Unexpected parameter list."),
            };

            return dictionaryType switch
            {
                { StaticFactoryMethod: string factory } => $"static options => options is null" +
                    $" ? static values => {factory}({valuesExpr})" +
                    $" : static values => {factory}({optionArgsExpr})",
                _ => $"static options => options is null" +
                    $" ? static values => new {dictionaryType.Type.FullyQualifiedName}({valuesExpr})" +
                    $" : static values => new {dictionaryType.Type.FullyQualifiedName}({optionArgsExpr}))",
            };
        }

        static string FormatSpanConstructorFunc(DictionaryShapeModel dictionaryType)
        {
            if (dictionaryType.ConstructionStrategy is not CollectionConstructionStrategy.Span)
            {
                return "null";
            }

            string suppressSuffix = dictionaryType.KeyValueTypesContainNullableAnnotations ? "!" : "";
            string valuesExpr = $"values{suppressSuffix}";

            if (dictionaryType is { StaticFactoryMethod: string factory1, CtorRequiresDictionaryConversion: true })
            {
                return $"static options => options is null" +
                    $" ? values => {factory1}(global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionary({valuesExpr}, keyComparer: null))" +
                    $" : values => {factory1}(global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionary({valuesExpr}, keyComparer: options.EqualityComparer))";
            }
            else if (dictionaryType is { CtorRequiresDictionaryConversion: true })
            {
                return $"static options => options is null" +
                    $" ? values => new {dictionaryType.Type.FullyQualifiedName}(global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionary({valuesExpr}, keyComparer: null))" +
                    $" : values => new {dictionaryType.Type.FullyQualifiedName}(global::PolyType.SourceGenModel.CollectionHelpers.CreateDictionary({valuesExpr}, keyComparer: options.EqualityComparer))";
            }

            ImmutableArray<ConstructionParameterType> parametersWithComparer = dictionaryType.ParameterLists.FirstOrDefault(
                list => list.Contains(ConstructionParameterType.IEqualityComparerOfT) && list.Contains(ConstructionParameterType.SpanOfT));

            string optionArgsExpr = parametersWithComparer switch
            {
                { IsDefault: true } => valuesExpr, // Assume a constructor that accepts span exists
                [ConstructionParameterType.IEqualityComparerOfT, ConstructionParameterType.SpanOfT] => $"options.EqualityComparer, {valuesExpr}",
                [ConstructionParameterType.SpanOfT, ConstructionParameterType.IEqualityComparerOfT] => $"{valuesExpr}, options.EqualityComparer",
                _ => throw new InvalidOperationException("Unexpected parameter list."),
            };

            return dictionaryType switch
            {
                { StaticFactoryMethod: string factory2 } => $"static options => options is null" + 
                    $" ? values => {factory2}({valuesExpr})" +
                    $" : values => {factory2}({optionArgsExpr})",
                _ => $"static options => options is null" +
                    $" ? values => new {dictionaryType.Type.FullyQualifiedName}({valuesExpr})" +
                    $" : values => new {dictionaryType.Type.FullyQualifiedName}({optionArgsExpr})",
            };
        }
    }
}
