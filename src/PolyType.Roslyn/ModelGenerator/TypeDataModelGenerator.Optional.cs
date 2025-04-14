using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapOptional(ITypeSymbol type, ImmutableArray<AssociatedTypeModel> associatedTypes, ref TypeDataModelGenerationContext ctx, TypeShapeDepth depth, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        model = null;
        status = default;

        if (type.OriginalDefinition.SpecialType is not SpecialType.System_Nullable_T)
        {
            return false;
        }

        ITypeSymbol elementType = ((INamedTypeSymbol)type).TypeArguments[0]!;
        if ((status = IncludeNestedType(elementType, ref ctx, depth)) != TypeDataModelGenerationStatus.Success)
        {
            // return true but a null model to indicate that the type is an unsupported nullable type
            return true;
        }

        model = new OptionalDataModel
        {
            Type = type,
            Depth = TypeShapeDepth.All,
            ElementType = elementType,
            AssociatedTypes = associatedTypes,
        };

        return true;
    }
}
