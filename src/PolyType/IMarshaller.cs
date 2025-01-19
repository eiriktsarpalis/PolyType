namespace PolyType;

/// <summary>
/// Represents a bidirectional mapping between <typeparamref name="T"/> and <typeparamref name="TSurrogate"/>.
/// </summary>
/// <typeparam name="T">The source type.</typeparam>
/// <typeparam name="TSurrogate">The surrogate type.</typeparam>
/// <remarks>
/// Implementations should form a bijection between <typeparamref name="T"/> and <typeparamref name="TSurrogate"/>,
/// meaning that chaining <see cref="ToSurrogate"/> with <see cref="FromSurrogate"/> and vice versa should
/// always produce equal results.
/// </remarks>
public interface IMarshaller<T, TSurrogate>
{
    /// <summary>
    /// Maps an instance of <typeparamref name="T"/> to <typeparamref name="TSurrogate"/>.
    /// </summary>
    /// <param name="value">The input of type <typeparamref name="T"/>.</param>
    /// <returns>A value of type <typeparamref name="TSurrogate"/> corresponding to <paramref name="value"/>.</returns>
    TSurrogate? ToSurrogate(T? value);

    /// <summary>
    /// Maps an instance of <typeparamref name="TSurrogate"/> back to <typeparamref name="T"/>.
    /// </summary>
    /// <param name="surrogate">The input of type <typeparamref name="TSurrogate"/>.</param>
    /// <returns>A value of type <typeparamref name="T"/> corresponding to <paramref name="surrogate"/>.</returns>
    T? FromSurrogate(TSurrogate? surrogate);
}