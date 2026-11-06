using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;

#nullable enable

namespace PolyType.Roslyn;

/// <summary>
/// A parameter data model wrapping an <see cref="IParameterSymbol"/>.
/// </summary>
#if POLYTYPE_ROSLYN_PUBLIC
public
#else
internal
#endif
readonly struct ParameterDataModel
{
    /// <summary>
    /// The parameter symbol that this model represents.
    /// </summary>
    public required IParameterSymbol Parameter { get; init; }

    /// <summary>
    /// True if the parameter does not accept null, accounting for its type and nullability attributes.
    /// </summary>
    public bool IsNonNullable => Parameter.IsNonNullableAnnotation();

    /// <summary>
    /// Whether the parameter declares a default value.
    /// </summary>
    public bool HasDefaultValue => Parameter.HasExplicitDefaultValue;

    /// <summary>
    /// The default value literal expressed as a valid C# expression.
    /// </summary>
    public string? DefaultValueExpr => Parameter.FormatDefaultValueExpr();
}