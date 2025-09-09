using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatEnumerableTypeShapeFactory(SourceWriter writer, string methodName, EnumerableShapeModel enumerableShapeModel)
    {
        string? methodFactoryMethodName = CreateMethodsFactoryName(enumerableShapeModel);
        string? eventFactoryMethodName = CreateEventsFactoryName(enumerableShapeModel);
        string? associatedTypesFactoryMethodName = GetAssociatedTypesFactoryName(enumerableShapeModel);

        writer.WriteLine($$"""
            private global::PolyType.ITypeShape<{{enumerableShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenEnumerableTypeShape<{{enumerableShapeModel.Type.FullyQualifiedName}}, {{enumerableShapeModel.ElementType.FullyQualifiedName}}>
                {
                    ElementType = {{GetShapeModel(enumerableShapeModel.ElementType).SourceIdentifier}},
                    ConstructionStrategy = {{FormatCollectionConstructionStrategy(enumerableShapeModel.ConstructionStrategy)}},
                    DefaultConstructorFunc = {{FormatDefaultConstructorFunc(enumerableShapeModel)}},
                    ParameterizedConstructorFunc = {{FormatParameterizedConstructorFunc(enumerableShapeModel)}},
                    SupportedComparer = {{FormatComparerOptions(enumerableShapeModel.ConstructorParameters)}},
                    GetEnumerableFunc = {{FormatGetEnumerableFunc(enumerableShapeModel)}},
                    AppenderFunc = {{FormatAppenderFunc(enumerableShapeModel)}},
                    CreateMethodsFunc = {{FormatNull(methodFactoryMethodName)}},
                    CreateEventsFunc = {{FormatNull(eventFactoryMethodName)}},
                    IsAsyncEnumerable = {{FormatBool(enumerableShapeModel.Kind is EnumerableKind.IAsyncEnumerableOfT)}},
                    IsSetType = {{FormatBool(enumerableShapeModel.IsSetType)}},
                    Rank = {{enumerableShapeModel.Rank}},
                    GetAssociatedTypeShapeFunc = {{FormatNull(associatedTypesFactoryMethodName)}},
                    Provider = this,
               };
            }
            """, trimNullAssignmentLines: true);

        if (methodFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatMethodsFactory(writer, methodFactoryMethodName, enumerableShapeModel);
        }

        if (eventFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatEventsFactory(writer, eventFactoryMethodName, enumerableShapeModel);
        }

        if (associatedTypesFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatAssociatedTypesFactory(writer, enumerableShapeModel, associatedTypesFactoryMethodName);
        }

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
                EnumerableKind.IAsyncEnumerableOfT => $"static obj => throw new global::System.InvalidOperationException(\"Sync enumeration of IAsyncEnumerable instances is not supported.\")",
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
            return FormatCollectionInitializer(enumerableType.ConstructorParameters, enumerableType.ElementType, valueType: null, enumerableType.StaticFactoryMethod ?? $"new {typeName}({{0}})", null, enumerableType.ElementTypeContainsNullableAnnotations);
        }

        static string FormatAppenderFunc(EnumerableShapeModel enumerableType)
        {
            string suppressSuffix = enumerableType.ElementTypeContainsNullableAnnotations ? "!" : "";
            return enumerableType switch
            {
                { AppendMethod: { } addMethod, InsertionMode: EnumerableInsertionMode.AddMethod, AppendMethodReturnsBoolean: true } =>
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => obj.{addMethod}(value{suppressSuffix})",
                { AppendMethod: { } addMethod, InsertionMode: EnumerableInsertionMode.AddMethod } =>
                    $"static (ref {enumerableType.Type.FullyQualifiedName} obj, {enumerableType.ElementType.FullyQualifiedName} value) => {{ obj.{addMethod}(value{suppressSuffix}); return true; }}",
                { AppendMethod: { } addMethod, InsertionMode: EnumerableInsertionMode.ExplicitICollectionOfT } =>
                    $"global::PolyType.SourceGenModel.CollectionHelpers.CreateEnumerableAppender<{enumerableType.Type.FullyQualifiedName}, {enumerableType.ElementType.FullyQualifiedName}>()!",
                { AppendMethod: { } addMethod, InsertionMode: EnumerableInsertionMode.ExplicitIList } =>
                    $"global::PolyType.SourceGenModel.CollectionHelpers.CreateEnumerableAppender<{enumerableType.Type.FullyQualifiedName}>()!",
                _ => "null",
            };
        }

        static string FormatParameterizedConstructorFunc(EnumerableShapeModel enumerableType)
        {
            if (enumerableType.ConstructionStrategy is not CollectionConstructionStrategy.Parameterized)
            {
                return "null";
            }

            string elementType = enumerableType.ElementType.FullyQualifiedName;
            if (enumerableType.Kind is EnumerableKind.ArrayOfT or EnumerableKind.ReadOnlyMemoryOfT or EnumerableKind.MemoryOfT)
            {
                string suppressSuffix = enumerableType.ElementTypeContainsNullableAnnotations ? "!" : "";
                string optionsTypeName = FormatCollectionConstructionOptionsTypeName(enumerableType.ElementType);
                return $"static (global::System.ReadOnlySpan<{elementType}> values, in {FormatCollectionConstructionOptionsTypeName(enumerableType.ElementType)} options) => values.ToArray(){suppressSuffix}";
            }

            return FormatCollectionInitializer(enumerableType, enumerableType.ElementType.FullyQualifiedName);
        }
    }

    private static string FormatCollectionInitializer(EnumerableShapeModel enumerableType, string valuesType)
    {
        string factory = enumerableType.StaticFactoryMethod is not null
          ? $"{enumerableType.StaticFactoryMethod}({{0}})"
          : $"new {enumerableType.Type.FullyQualifiedName}({{0}})";

        return FormatCollectionInitializer(enumerableType.ConstructorParameters, enumerableType.ElementType, valueType: null, factory, valuesType, enumerableType.ElementTypeContainsNullableAnnotations);
    }

    private static string FormatCollectionConstructionStrategy(CollectionConstructionStrategy strategy)
    {
        string identifier = strategy switch
        {
            CollectionConstructionStrategy.None => "None",
            CollectionConstructionStrategy.Mutable => "Mutable",
            CollectionConstructionStrategy.Parameterized => "Parameterized",
            _ => throw new ArgumentException(strategy.ToString()),
        };

        return $"global::PolyType.Abstractions.CollectionConstructionStrategy." + identifier;
    }
}
