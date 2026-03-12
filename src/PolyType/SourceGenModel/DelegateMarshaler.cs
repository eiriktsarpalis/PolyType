namespace PolyType.SourceGenModel;

/// <summary>
/// Defines a delegate-based marshaler between two types without any type constraints.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target type.</typeparam>
public sealed class DelegateMarshaler<TSource, TTarget> : IMarshaler<TSource, TTarget>
{
    private readonly Func<TSource?, TTarget?> _marshal;
    private readonly Func<TTarget?, TSource?> _unmarshal;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateMarshaler{TSource, TTarget}"/> class.
    /// </summary>
    /// <param name="marshal">A delegate that converts from <typeparamref name="TSource"/> to <typeparamref name="TTarget"/>.</param>
    /// <param name="unmarshal">A delegate that converts from <typeparamref name="TTarget"/> to <typeparamref name="TSource"/>.</param>
    public DelegateMarshaler(Func<TSource?, TTarget?> marshal, Func<TTarget?, TSource?> unmarshal)
    {
        _marshal = marshal;
        _unmarshal = unmarshal;
    }

    /// <inheritdoc/>
    public TTarget? Marshal(TSource? value) => _marshal(value);

    /// <inheritdoc/>
    public TSource? Unmarshal(TTarget? value) => _unmarshal(value);
}
