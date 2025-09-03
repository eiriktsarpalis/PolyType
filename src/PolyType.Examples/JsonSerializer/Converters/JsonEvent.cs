using PolyType.Abstractions;

namespace PolyType.Examples.JsonSerializer.Converters;

internal sealed class JsonEvent<TDeclaringType, THandler>(
    object? target,
    Func<JsonEventHandler, THandler> wrapper,
    Setter<TDeclaringType?, THandler> subscriber,
    Setter<TDeclaringType?, THandler> unsubscriber) : JsonEvent
{
    private TDeclaringType? _target = (TDeclaringType?)target;
    private readonly Setter<TDeclaringType?, THandler> _subscriber = subscriber;
    private readonly Setter<TDeclaringType?, THandler> _unsubscriber = unsubscriber;

    public override IDisposable Subscribe(JsonEventHandler handler)
    {
        THandler wrappedHandler = wrapper(handler);
        _subscriber(ref _target, wrappedHandler);
        return new Disposer(this, wrappedHandler);
    }

    private sealed class Disposer(JsonEvent<TDeclaringType, THandler> parent, THandler handler) : IDisposable
    {
        private JsonEvent<TDeclaringType, THandler>? _parent = parent;
        private THandler _handler = handler;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _parent, null) is { } parent)
            {
                parent._unsubscriber(ref parent._target, _handler);
                _handler = default!;
            }
        }
    }
}

internal sealed class AsyncJsonEvent<TDeclaringType, THandler>(
    object? target,
    Func<AsyncJsonEventHandler, THandler> wrapper,
    Setter<TDeclaringType?, THandler> subscriber,
    Setter<TDeclaringType?, THandler> unsubscriber) : AsyncJsonEvent
{
    private TDeclaringType? _target = (TDeclaringType?)target;
    private readonly Setter<TDeclaringType?, THandler> _subscriber = subscriber;
    private readonly Setter<TDeclaringType?, THandler> _unsubscriber = unsubscriber;

    public override IDisposable Subscribe(AsyncJsonEventHandler handler)
    {
        THandler wrappedHandler = wrapper(handler);
        _subscriber(ref _target, wrappedHandler);
        return new Disposer(this, wrappedHandler);
    }

    private sealed class Disposer(AsyncJsonEvent<TDeclaringType, THandler> parent, THandler handler) : IDisposable
    {
        private AsyncJsonEvent<TDeclaringType, THandler>? _parent = parent;
        private THandler _handler = handler;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _parent, null) is { } parent)
            {
                parent._unsubscriber(ref parent._target, _handler);
                _handler = default!;
            }
        }
    }
}
