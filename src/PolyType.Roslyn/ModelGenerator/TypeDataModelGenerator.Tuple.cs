﻿using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    /// <summary>
    /// Determines whether <see cref="System.Tuple" /> tuples should be mapped to 
    /// <see cref="TupleDataModel"/> instances and flattened to a single type for
    /// tuples containing over 8 elements.
    /// </summary>
    /// <remarks>
    /// Defaults to false, since C# treats
    /// <see cref="System.Tuple"/> types as regular classes. Set to true if 
    /// there is a need to handle F# models that do have syntactic sugar support
    /// for <see cref="System.Tuple"/>.
    /// </remarks>
    protected virtual bool FlattenSystemTupleTypes => false;

    private bool TryMapTuple(ITypeSymbol type, ImmutableArray<AssociatedTypeModel> associatedTypes, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        status = default;
        model = null;

        if (type is not INamedTypeSymbol namedType)
        {
            // Objects must be named classes, structs, or interfaces.
            return false;
        }

        if (namedType.IsTupleType)
        {
            var elements = new List<PropertyDataModel>();
            foreach (IFieldSymbol element in namedType.TupleElements)
            {
                if ((status = IncludeNestedType(element.Type, ref ctx)) != TypeDataModelGenerationStatus.Success)
                {
                    // Return true to indicate that the type is an unsupported tuple type.
                    return true;
                }

                PropertyDataModel propertyModel = MapField(element);
                elements.Add(propertyModel);
            }

            model = new TupleDataModel
            {
                Type = type,
                Depth = TypeShapeDepth.All,
                Elements = elements.ToImmutableArray(),
                IsValueTuple = true,
            };

            status = TypeDataModelGenerationStatus.Success;
            return true;
        }

        if (FlattenSystemTupleTypes && 
            RoslynHelpers.GetClassTupleProperties(KnownSymbols.CoreLibAssembly, namedType) 
            is IPropertySymbol[] classTupleProperties)
        {
            var elements = new List<PropertyDataModel>();
            foreach (IPropertySymbol elementProp in classTupleProperties)
            {
                if ((status = IncludeNestedType(elementProp.Type, ref ctx)) != TypeDataModelGenerationStatus.Success)
                {
                    // Return true to indicate that the type is an unsupported tuple type.
                    return true;
                }

                PropertyDataModel propertyModel = MapProperty(elementProp, includeGetter: true, includeSetter: false);
                elements.Add(propertyModel);
            }

            model = new TupleDataModel
            {
                Type = type,
                Depth = TypeShapeDepth.All,
                Elements = elements.ToImmutableArray(),
                IsValueTuple = false,
                AssociatedTypes = associatedTypes,
            };

            status = TypeDataModelGenerationStatus.Success;
            return true;
        }

        return false;
    }
}
