using System.Text.Json;

namespace PolyType.Examples.JsonSerializer;

/// <summary>
/// Defines a dynamically typed event that can be subscribed to with <see cref="JsonEventHandler"/> callbacks.
/// </summary>
public abstract class JsonEvent
{
    /// <summary>
    /// Subscribes a <see cref="JsonEventHandler"/> to the current JSON event.
    /// </summary>
    /// <param name="handler">The event handler to add.</param>
    /// <returns>An <see cref="IDisposable"/> used to unsubscribe from the event.</returns>
    public abstract IDisposable Subscribe(JsonEventHandler handler);
}

/// <summary>
/// Defines a dynamically typed event that can be subscribed to with <see cref="AsyncJsonEventHandler"/> callbacks.
/// </summary>
public abstract class AsyncJsonEvent
{
    /// <summary>
    /// Subscribes a <see cref="AsyncJsonEventHandler"/> to the current JSON event.
    /// </summary>
    /// <param name="handler">The event handler to add.</param>
    /// <returns>An <see cref="IDisposable"/> used to unsubscribe from the event.</returns>
    public abstract IDisposable Subscribe(AsyncJsonEventHandler handler);
}

/// <summary>
/// Represents a JSON-based event handler delegate.
/// </summary>
/// <param name="sender">The source of the event.</param>
/// <param name="parameters">The event parameters.</param>
/// <returns>The result of the event handling.</returns>
public delegate JsonElement JsonEventHandler(object? sender, IReadOnlyDictionary<string, JsonElement> parameters);

/// <summary>
/// Represents an async JSON-based event handler delegate.
/// </summary>
/// <param name="sender">The source of the event.</param>
/// <param name="parameters">The event parameters.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The result of the event handling.</returns>
public delegate ValueTask<JsonElement> AsyncJsonEventHandler(
    object? sender,
    IReadOnlyDictionary<string, JsonElement> parameters,
    CancellationToken cancellationToken = default);