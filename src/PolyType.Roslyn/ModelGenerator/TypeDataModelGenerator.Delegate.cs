using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    /// <summary>
    /// Determines whether delegate parameters should be included in the generated model.
    /// </summary>
    protected virtual bool IncludeDelegateParameters => false;

    private bool TryMapDelegate(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, ImmutableArray<MethodDataModel> methodModels, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        if (type.TypeKind is not TypeKind.Delegate)
        {
            model = null;
            status = default;
            return false;
        }

        if (!IncludeDelegateParameters)
        {
            model = new TypeDataModel
            { 
                Type = type,
                Requirements = TypeShapeRequirements.None,
                Methods = methodModels,
            };

            status = TypeDataModelGenerationStatus.Success;
            return true;
        }

        var delegateType = (INamedTypeSymbol)type;
        ResolvedMethodSymbol resolvedMethodSymbol = new()
        { 
            MethodSymbol = delegateType.DelegateInvokeMethod!,
        };

        status = MapMethod(resolvedMethodSymbol, ref ctx, out MethodDataModel methodModel);
        if (status is not TypeDataModelGenerationStatus.Success)
        {
            model = null;
            return false;
        }

        model = new DelegateDataModel
        {
            Type = type,
            InvokeMethod = resolvedMethodSymbol.MethodSymbol,
            Requirements = TypeShapeRequirements.Full,
            ReturnedValueType = methodModel.ReturnedValueType,
            ReturnTypeKind = methodModel.ReturnTypeKind,
            Parameters = methodModel.Parameters,
            Methods = methodModels,
        };

        return true;
    }
}
