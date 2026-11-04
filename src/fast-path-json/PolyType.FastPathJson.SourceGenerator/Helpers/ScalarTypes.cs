using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;

namespace PolyType.FastPathJson.SourceGenerator.Helpers;

internal sealed class ScalarTypes
{
    public const string Prefix = "global::PolyType.FastPathJson.Converters.";
    public const string GeneratedPrefix = "global::PolyType.FastPathJson.Generated.GeneratedJsonConverters.";

    private readonly HashSet<ITypeSymbol> _types = new(SymbolEqualityComparer.Default);

    public ScalarTypes(Compilation compilation)
    {
        ReadOnlySpan<SpecialType> specialTypes =
        [
            SpecialType.System_Boolean,
            SpecialType.System_SByte,
            SpecialType.System_Int16,
            SpecialType.System_Int32,
            SpecialType.System_Int64,
            SpecialType.System_Byte,
            SpecialType.System_UInt16,
            SpecialType.System_UInt32,
            SpecialType.System_UInt64,
            SpecialType.System_Single,
            SpecialType.System_Double,
            SpecialType.System_Decimal,
            SpecialType.System_Char,
            SpecialType.System_String,
            SpecialType.System_DateTime,
        ];
        foreach (SpecialType specialType in specialTypes)
        {
            _types.Add(compilation.GetSpecialType(specialType));
        }

        _types.Add(compilation.CreateArrayTypeSymbol(compilation.GetSpecialType(SpecialType.System_Byte)));

        ReadOnlySpan<string> metadataNames =
        [
            "System.Guid",
            "System.DateTimeOffset",
            "System.TimeSpan",
            "System.DateOnly",
            "System.TimeOnly",
            "System.Int128",
            "System.UInt128",
            "System.Half",
            "System.Uri",
            "System.Version",
            "System.Numerics.BigInteger",
            "System.Text.Rune",
        ];
        foreach (string metadataName in metadataNames)
        {
            if (compilation.GetTypeByMetadataName(metadataName) is { } type)
            {
                _types.Add(type);
            }
        }
    }

    public bool Contains(ITypeSymbol type) => _types.Contains(type);

    public string? GetConverter(ITypeSymbol type) =>
        Contains(type) ? type.CreateTypeIdentifier([]) + "Converter" : null;
}
