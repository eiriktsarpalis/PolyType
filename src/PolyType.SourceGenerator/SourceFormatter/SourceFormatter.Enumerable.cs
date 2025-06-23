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
                    MutableConstructorFunc = {{FormatMutableConstructorFunc(enumerableShapeModel)}},
                    EnumerableConstructorFunc = {{FormatEnumerableConstructorFunc(enumerableShapeModel)}},
                    SpanConstructorFunc = {{FormatSpanConstructorFunc(enumerableShapeModel)}},
                    SupportedConstructionOptions = {{FormatComparerOptions(enumerableShapeModel.ConstructionComparer)}},
                    GetEnumerableFunc = {{FormatGetEnumerableFunc(enumerableShapeModel)}},
                    AddElementFunc = {{FormatAddElementFunc(enumerableShapeModel)}},
                    IsAsyncEnumerable = {{FormatBool(enumerableShapeModel.Kind is EnumerableKind.AsyncEnumerableOfT)}},
                    Rank = {{enumerableShapeModel.Rank}},
                    AssociatedTypeShapes = {{FormatAssociatedTypeShapes(enumerableShapeModel)}},
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

        static string FormatMutableConstructorFunc(EnumerableShapeModel enumerableType)
        {
            if (enumerableType.ConstructionStrategy is not CollectionConstructionStrategy.Mutable)
            {
                return "null";
            }

            string typeName = enumerableType.ImplementationTypeFQN ?? enumerableType.Type.FullyQualifiedName;
            return FormatCollectionInitializer(enumerableType.ConstructionComparer, enumerableType.HasConstructorWithoutComparer, enumerableType.ElementType, enumerableType.StaticFactoryMethod ?? $"new {typeName}({{0}})", null);
        }

        static string FormatAddElementFunc(EnumerableShapeModel enumerableType)
        {
            string suppressSuffix = enumerableType.ElementTypeContainsNullableAnnotations ? "!" : "";
            return enumerableType switch
            {
                { AddElementMethod: { } addMethod, ImplementationTypeFQN: null, AddMethodIsExplicitInterfaceImplementation: false } =>
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => obj.{addMethod}(value{suppressSuffix})",
                { AddElementMethod: { } addMethod, ImplementationTypeFQN: { } implTypeFQN } =>
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => (({implTypeFQN})obj).{addMethod}(value{suppressSuffix})",
                { AddElementMethod: { } addMethod, AddMethodIsExplicitInterfaceImplementation: true, Kind: EnumerableKind.IEnumerableOfT } =>
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => ((global::System.Collections.Generic.ICollection<{enumerableType.ElementType.FullyQualifiedName}>)obj{suppressSuffix}).{addMethod}(value{suppressSuffix})",
                { AddElementMethod: { } addMethod, AddMethodIsExplicitInterfaceImplementation: true, Kind: EnumerableKind.IEnumerable } =>
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => ((global::System.Collections.IList)obj{suppressSuffix}).{addMethod}(value{suppressSuffix})",
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
            string valuesType = $"global::System.ReadOnlySpan<{enumerableType.ElementType}>";
            string valuesExpr = enumerableType.CtorRequiresListConversion ? $"global::PolyType.SourceGenModel.CollectionHelpers.CreateList(values{suppressSuffix})" : $"values{suppressSuffix}";

            if (enumerableType.Kind is EnumerableKind.ArrayOfT or EnumerableKind.ReadOnlyMemoryOfT or EnumerableKind.MemoryOfT)
            {
                string optionsTypeName = FormatCollectionConstructionOptionsTypeName(enumerableType.ElementType);
                return $"static ({valuesType} values, in {FormatCollectionConstructionOptionsTypeName(enumerableType.ElementType)}? options) => {valuesExpr}.ToArray()";
            }

            return FormatCollectionInitializer(enumerableType, valuesType, valuesExpr);
        }

        static string FormatEnumerableConstructorFunc(EnumerableShapeModel enumerableType)
        {
            if (enumerableType.ConstructionStrategy is not CollectionConstructionStrategy.Enumerable)
            {
                return "null";
            }

            string suppressSuffix = enumerableType.ElementTypeContainsNullableAnnotations ? "!" : "";
            string valuesExpr = $"values{suppressSuffix}";
            return FormatCollectionInitializer(enumerableType, $"global::System.Collections.Generic.IEnumerable<{enumerableType.ElementType}>", valuesExpr);
        }
    }

    private static string FormatCollectionInitializer(EnumerableShapeModel enumerableType, string valuesType, string valuesExpr)
    {
        string factory = enumerableType.StaticFactoryMethod is not null
          ? $"{enumerableType.StaticFactoryMethod}({{0}})"
          : $"new {enumerableType.Type.FullyQualifiedName}({{0}})";
        return FormatCollectionInitializer(enumerableType.ConstructionComparer, enumerableType.HasConstructorWithoutComparer, enumerableType.ElementType, factory, (valuesType, valuesExpr));
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
