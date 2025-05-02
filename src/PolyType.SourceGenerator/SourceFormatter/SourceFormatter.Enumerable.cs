using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatEnumerableTypeShapeFactory(SourceWriter writer, string methodName, EnumerableShapeModel enumerableShapeModel)
    {
        writer.WriteLine($$"""
            private global::PolyType.Abstractions.ITypeShape<{{enumerableShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenEnumerableTypeShape<{{enumerableShapeModel.Type.FullyQualifiedName}}, {{enumerableShapeModel.ElementType.FullyQualifiedName}}>
                {
                    ElementType = {{GetShapeModel(enumerableShapeModel.ElementType).SourceIdentifier}},
                    ConstructionStrategy = {{FormatCollectionConstructionStrategy(enumerableShapeModel.ConstructionStrategy)}},
                    DefaultConstructorFunc = {{FormatDefaultConstructorFunc(enumerableShapeModel)}},
                    EnumerableConstructorFunc = {{FormatEnumerableConstructorFunc(enumerableShapeModel)}},
                    SpanConstructorFunc = {{FormatSpanConstructorFunc(enumerableShapeModel)}},
                    GetEnumerableFunc = {{FormatGetEnumerableFunc(enumerableShapeModel)}},
                    AddElementFunc = {{FormatAddElementFunc(enumerableShapeModel)}},
                    IsAsyncEnumerable = {{FormatBool(enumerableShapeModel.Kind is EnumerableKind.AsyncEnumerableOfT)}},
                    Rank = {{enumerableShapeModel.Rank}},
                    Provider = this,
               };
            }
            """, trimNullAssignmentLines: true);

        static string FormatGetEnumerableFunc(EnumerableShapeModel enumerableType)
        {
            string suppressSuffix = enumerableType.ElementTypeContainsNullableAnnotations ? "!" : "";
            return enumerableType.Kind switch
            {
                EnumerableKind.IEnumerableOfT or
                EnumerableKind.ArrayOfT => $"static obj => obj{suppressSuffix}",
                EnumerableKind.MemoryOfT => $"static obj => global::System.Runtime.InteropServices.MemoryMarshal.ToEnumerable((global::System.ReadOnlyMemory<{enumerableType.ElementType.FullyQualifiedName}>)obj{suppressSuffix})",
                EnumerableKind.ReadOnlyMemoryOfT => $"static obj => global::System.Runtime.InteropServices.MemoryMarshal.ToEnumerable(obj{suppressSuffix})",
                EnumerableKind.IEnumerable => $"static obj => global::System.Linq.Enumerable.Cast<object>(obj{suppressSuffix})",
                EnumerableKind.MultiDimensionalArrayOfT => $"static obj => global::System.Linq.Enumerable.Cast<{enumerableType.ElementType.FullyQualifiedName}>(obj{suppressSuffix})",
                EnumerableKind.AsyncEnumerableOfT => $"static obj => throw new global::System.InvalidOperationException(\"Sync enumeration of IAsyncEnumerable instances is not supported.\")",
                _ => throw new ArgumentException(enumerableType.Kind.ToString()),
            };
        }

        static string FormatDefaultConstructorFunc(EnumerableShapeModel enumerableType)
        {
            if (enumerableType.ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
            {
                return "null";
            }

            string typeName = enumerableType.ImplementationTypeFQN ?? enumerableType.Type.FullyQualifiedName;
            ImmutableArray<ConstructionParameterType> parametersWithComparer = enumerableType.ParameterLists.FirstOrDefault(list => list.Contains(ConstructionParameterType.IEqualityComparerOfT));

            if (parametersWithComparer.IsDefault)
            {
                return $"static options => static () => new {typeName}()";
            }

            string optionArgsExpr = parametersWithComparer switch
            {
                [ConstructionParameterType.IEqualityComparerOfT] => "options.EqualityComparer",
                _ => ""
            };

            return $"static options => options is null" +
                $" ? () => new {typeName}()" +
                $" : () => new {typeName}({optionArgsExpr})";
        }

        static string FormatAddElementFunc(EnumerableShapeModel enumerableType)
        {
            string suppressSuffix = enumerableType.ElementTypeContainsNullableAnnotations ? "!" : "";
            return enumerableType switch
            {
                { AddElementMethod: { } addMethod, ImplementationTypeFQN: null } =>
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => obj.{addMethod}(value{suppressSuffix})",
                { AddElementMethod: { } addMethod, ImplementationTypeFQN: { } implTypeFQN } =>
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => (({implTypeFQN})obj).{addMethod}(value{suppressSuffix})",
                _ => "null",
            };
        }

        static string FormatSpanConstructorFunc(EnumerableShapeModel enumerableType)
        {
            if (enumerableType.ConstructionStrategy is not CollectionConstructionStrategy.Span)
            {
                return "null";
            }

            string suppressSuffix = enumerableType.ElementTypeContainsNullableAnnotations ? "!" : "";
            string valuesExpr = enumerableType.CtorRequiresListConversion ? $"global::PolyType.SourceGenModel.CollectionHelpers.CreateList(values{suppressSuffix})" : $"values{suppressSuffix}";

            if (enumerableType.Kind is EnumerableKind.ArrayOfT or EnumerableKind.ReadOnlyMemoryOfT or EnumerableKind.MemoryOfT)
            {
                return $"static options => static values => {valuesExpr}.ToArray()";
            }

            ImmutableArray<ConstructionParameterType> parametersWithComparer = enumerableType.ParameterLists.FirstOrDefault(
                list => list.Contains(ConstructionParameterType.IEqualityComparerOfT) && list.Contains(ConstructionParameterType.SpanOfT));

            string optionArgsExpr = parametersWithComparer switch
            {
                { IsDefault: true } => valuesExpr, // Assume a constructor that accepts span exists
                [ConstructionParameterType.IEqualityComparerOfT, ConstructionParameterType.SpanOfT] => $"options.EqualityComparer, {valuesExpr}",
                [ConstructionParameterType.SpanOfT, ConstructionParameterType.IEqualityComparerOfT] => $"{valuesExpr}, options.EqualityComparer",
                _ => throw new InvalidOperationException("Unexpected parameter list."),
            };

            return enumerableType switch
            {
                { StaticFactoryMethod: string spanFactory } => $"static options => options is null" +
                    $" ? static values => {spanFactory}({valuesExpr})" +
                    $" : static values => {spanFactory}({optionArgsExpr})",
                _ => $"static options => options is null" +
                    $" ? static values => new {enumerableType.Type.FullyQualifiedName}({valuesExpr})" +
                    $" : static values => new {enumerableType.Type.FullyQualifiedName}({optionArgsExpr})",
            };
        }

        static string FormatEnumerableConstructorFunc(EnumerableShapeModel enumerableType)
        {
            if (enumerableType.ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
            {
                return "null";
            }

            string suppressSuffix = enumerableType.ElementTypeContainsNullableAnnotations ? "!" : "";
            string valuesExpr = $"values{suppressSuffix}";

            ImmutableArray<ConstructionParameterType> parametersWithComparer = enumerableType.ParameterLists.FirstOrDefault(
                list => list.Contains(ConstructionParameterType.IEqualityComparerOfT) && list.Contains(ConstructionParameterType.SpanOfT));

            string optionArgsExpr = parametersWithComparer switch
            {
                { IsDefault: true } => valuesExpr, // Assume a constructor that accepts span exists
                [ConstructionParameterType.IEqualityComparerOfT, ConstructionParameterType.SpanOfT] => $"options.EqualityComparer, {valuesExpr}",
                [ConstructionParameterType.SpanOfT, ConstructionParameterType.IEqualityComparerOfT] => $"{valuesExpr}, options.EqualityComparer",
                _ => throw new InvalidOperationException("Unexpected parameter list."),
            };

            return enumerableType switch
            {
                { StaticFactoryMethod: { } enumerableFactory } => $"static options => options is null" +
                    $" ? static values => {enumerableFactory}({valuesExpr})" +
                    $" : static values => {enumerableFactory}({optionArgsExpr})",
                _ => $"static options => options is null" +
                    $" ? static values => new {enumerableType.Type.FullyQualifiedName}({valuesExpr})" +
                    $" : static values => new {enumerableType.Type.FullyQualifiedName}({optionArgsExpr})",
            };
        }
    }

    private static string FormatCollectionConstructionStrategy(CollectionConstructionStrategy strategy)
    {
        string identifier = strategy switch
        {
            CollectionConstructionStrategy.None => "None",
            CollectionConstructionStrategy.Mutable => "Mutable",
            CollectionConstructionStrategy.Enumerable => "Enumerable",
            CollectionConstructionStrategy.Span => "Span",
            _ => throw new ArgumentException(strategy.ToString()),
        };

        return $"global::PolyType.Abstractions.CollectionConstructionStrategy." + identifier;
    }
}
