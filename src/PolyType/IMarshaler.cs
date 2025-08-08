namespace PolyType;

/// <summary>
/// Represents a bidirectional mapping between <typeparamref name="TSource"/> and <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TSource">The source type from which we're marshaling.</typeparam>
/// <typeparam name="TTarget">The target type to which we're marshaling.</typeparam>
/// <remarks>
/// Implementations should form a bijection between <typeparamref name="TSource"/> and <typeparamref name="TTarget"/>,
/// meaning that chaining <see cref="Marshal"/> with <see cref="Unmarshal"/> and vice versa should
/// always produce equal results.
/// </remarks>
public interface IMarshaler<TSource, TTarget>
{
    /// <summary>
    /// Maps an instance of <typeparamref name="TSource"/> to <typeparamref name="TTarget"/>.
    /// </summary>
    /// <param name="value">The input of type <typeparamref name="TSource"/>.</param>
    /// <returns>A value of type <typeparamref name="TTarget"/> corresponding to <paramref name="value"/>.</returns>
    TTarget? Marshal(TSource? value);

    /// <summary>
    /// Maps an instance of <typeparamref name="TTarget"/> back to <typeparamref name="TSource"/>.
    /// </summary>
    /// <param name="value">The input of type <typeparamref name="TTarget"/>.</param>
    /// <returns>A value of type <typeparamref name="TSource"/> corresponding to <paramref name="value"/>.</returns>
    TSource? Unmarshal(TTarget? value);
}