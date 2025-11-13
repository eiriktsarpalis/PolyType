using Microsoft.CodeAnalysis;
using System.Diagnostics;

namespace PolyType.Roslyn;

public partial class TypeDataModelGenerator
{
    private bool TryMapEnum(ITypeSymbol type, ref TypeDataModelGenerationContext ctx, out TypeDataModel? model, out TypeDataModelGenerationStatus status)
    {
        if (type.TypeKind is not TypeKind.Enum)
        {
            model = null;
            status = default;
            return false;
        }

        INamedTypeSymbol enumType = (INamedTypeSymbol)type;
        INamedTypeSymbol underlyingType = enumType.EnumUnderlyingType!;
        status = IncludeNestedType(underlyingType, ref ctx);
        Debug.Assert(status is TypeDataModelGenerationStatus.Success);

        Dictionary<string, object> members = new(StringComparer.Ordinal);
        foreach (ISymbol member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { ConstantValue: { } value } field)
            {
                string name = this.GetEnumValueName(field);
                members.Add(name, value);
            }
        }

        bool isFlags = KnownSymbols.FlagsAttribute is { } flagsAttr && 
            enumType.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, flagsAttr));

        model = new EnumDataModel
        {
            Type = type,
            Requirements = TypeShapeRequirements.Full,
            UnderlyingType = underlyingType,
            Members = members,
            IsFlags = isFlags,
        };

        return true;
    }

    /// <summary>
    /// Gets the name of given enum value.
    /// </summary>
    /// <param name="field">The field symbol of the enum.</param>
    /// <returns>The name of the enum value.</returns>
    protected virtual string GetEnumValueName(IFieldSymbol field) => field.Name;
}
