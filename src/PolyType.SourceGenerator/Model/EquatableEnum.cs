using PolyType.Roslyn;

namespace PolyType.SourceGenerator.Model;

/// <summary>
/// An equatable wrapper around an enum.
/// </summary>
/// <param name="Value">The enum vlaue</param>
/// <remarks>
/// This is useful for use with <see cref="ImmutableEquatableDictionary{TKey, TValue}" /> which requires its type arguments to
/// implement <see cref="IEquatable{T}" /> (and enums don't, but are nevertheless equatable).
/// </remarks>
public record struct EquatableEnum<T>(T Value) where T : Enum;
