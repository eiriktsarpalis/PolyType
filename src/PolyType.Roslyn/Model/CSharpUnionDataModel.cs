using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

/// <summary>
/// Describes a type following the C# union member pattern.
/// </summary>
public sealed class CSharpUnionDataModel : TypeDataModel
{
    /// <inheritdoc/>
    public override TypeDataKind Kind => TypeDataKind.CSharpUnion;

    /// <summary>
    /// Gets the creation members and their case types in declaration-context compiler enumeration order.
    /// </summary>
    /// <remarks>
    /// Distinct creation members are retained even when generic substitution gives them the same case type.
    /// Inherited factory hiding is resolved in the original union declaration's generic context.
    /// This order is independent of the order needed to match overlapping case types.
    /// </remarks>
    public required ImmutableArray<UnionCaseDataModel> UnionCases { get; init; }

    /// <summary>
    /// Gets the public instance property used to access the union payload.
    /// </summary>
    public required IPropertySymbol ValueProperty { get; init; }

    /// <summary>
    /// Gets the nested IUnionMembers interface, or <see langword="null"/> when constructors define the cases.
    /// </summary>
    public INamedTypeSymbol? UnionMemberProvider { get; init; }
}

/// <summary>
/// Describes a creation member of a C# union.
/// </summary>
public readonly struct UnionCaseDataModel
{
    /// <summary>
    /// Gets the case type after substituting the union's type arguments.
    /// </summary>
    public required ITypeSymbol Type { get; init; }

    /// <summary>
    /// Gets the case type in the context of the union's original definition.
    /// </summary>
    public required ITypeSymbol DeclaredType { get; init; }

    /// <summary>
    /// Gets the specific constructor or interface factory that creates this case.
    /// </summary>
    public required IMethodSymbol CreationMember { get; init; }

    /// <summary>
    /// Gets the public instance <c>bool TryGetValue(out TCase)</c> overload, when available.
    /// </summary>
    public IMethodSymbol? TryGetValueMethod { get; init; }

    /// <summary>
    /// Gets whether the creation parameter's nullable contract accepts null.
    /// </summary>
    public required bool IsNullable { get; init; }
}
