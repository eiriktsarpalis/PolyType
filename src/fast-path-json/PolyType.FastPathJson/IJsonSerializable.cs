using System.Text.Json;

namespace PolyType.FastPathJson;

/// <summary>
/// Provides statically dispatched JSON serialization for <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The serialized type, which need not implement this interface itself.</typeparam>
public interface IJsonSerializable<T>
{
    /// <summary>Writes a single JSON value.</summary>
    /// <param name="writer">The writer to receive the value.</param>
    /// <param name="value">The value to serialize.</param>
    static abstract void Write(Utf8JsonWriter writer, T? value);

    /// <summary>
    /// Reads a single JSON value, starting on its first token and returning on its final token.
    /// </summary>
    /// <param name="reader">The reader positioned on the value's first token.</param>
    /// <returns>The deserialized value.</returns>
    static abstract T? Read(ref Utf8JsonReader reader);

    /// <summary>Writes a dictionary key as a JSON property name.</summary>
    /// <param name="writer">The writer to receive the property name.</param>
    /// <param name="value">The key to serialize.</param>
    /// <exception cref="NotSupportedException">The converter does not support property names.</exception>
    static virtual void WriteAsPropertyName(Utf8JsonWriter writer, T value) =>
        SerializationHelpers.ThrowUnsupportedPropertyName<T>();

    /// <summary>Reads a dictionary key without advancing past the property-name token.</summary>
    /// <param name="reader">The reader positioned on a property name.</param>
    /// <returns>The deserialized key.</returns>
    /// <exception cref="NotSupportedException">The converter does not support property names.</exception>
    static virtual T ReadAsPropertyName(ref Utf8JsonReader reader) =>
        SerializationHelpers.ThrowUnsupportedPropertyName<T>();
}
