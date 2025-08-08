using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Defines an injective marshaler between two types in a subtype relationship.
/// </summary>
/// <typeparam name="TDerived">The derived type.</typeparam>
/// <typeparam name="TBase">The base type.</typeparam>
public sealed class SubtypeMarshaler<TDerived, TBase> : IMarshaler<TDerived, TBase>
    where TDerived : TBase
{
    /// <summary>
    /// The singleton instance of the subtype marshaler.
    /// </summary>
    public static SubtypeMarshaler<TDerived, TBase> Instance { get; } = new();

    private SubtypeMarshaler() { }

    /// <inheritdoc/>
    public TBase? Marshal(TDerived? value) => value;

    /// <inheritdoc/>
    public TDerived? Unmarshal(TBase? value) => (TDerived?)value;
}
