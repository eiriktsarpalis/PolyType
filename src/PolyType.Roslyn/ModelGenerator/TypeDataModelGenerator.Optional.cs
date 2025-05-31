using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapOptional(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, TypeShapeRequirements requirements, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        model = null;
        status = default;

        if (type.OriginalDefinition.SpecialType is not SpecialType.System_Nullable_T)
        {
            return false;
        }

        ITypeSymbol elementType = ((INamedTypeSymbol)type).TypeArguments[0]!;
        if ((status = IncludeNestedType(elementType, ref ctx, requirements)) != TypeDataModelGenerationStatus.Success)
        {
            // return true but a null model to indicate that the type is an unsupported nullable type
            return true;
        }

        model = new OptionalDataModel
        {
            Type = type,
            Depth = TypeShapeRequirements.Full,
            ElementType = elementType,
        };

        return true;
    }
}
