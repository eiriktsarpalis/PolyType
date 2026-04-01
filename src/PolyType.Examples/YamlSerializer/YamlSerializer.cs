using PolyType.Abstractions;
using PolyType.Examples.YamlSerializer.Converters;
using PolyType.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.Examples.YamlSerializer;

/// <summary>
/// Provides a YAML serialization implementation built on top of PolyType.
/// </summary>
public static partial class YamlSerializer
{
    private static readonly MultiProviderTypeCache s_converterCaches = new()
    {
        DelayedValueFactory = new DelayedYamlConverterFactory(),
        ValueBuilderFactory = ctx => new Builder(ctx),
    };

    /// <summary>
    /// Builds a <see cref="YamlConverter{T}"/> instance from the specified shape.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="shape">The shape instance guiding converter construction.</param>
    /// <returns>A <see cref="YamlConverter{T}"/> instance.</returns>
    public static YamlConverter<T> CreateConverter<T>(ITypeShape<T> shape) =>
        (YamlConverter<T>)s_converterCaches.GetOrAdd(shape)!;

    /// <summary>
    /// Builds a <see cref="YamlConverter{T}"/> instance from the specified shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <param name="typeShapeProvider">The shape provider guiding converter construction.</param>
    /// <returns>A <see cref="YamlConverter{T}"/> instance.</returns>
    public static YamlConverter<T> CreateConverter<T>(ITypeShapeProvider typeShapeProvider) =>
        (YamlConverter<T>)s_converterCaches.GetOrAdd(typeof(T), typeShapeProvider)!;

    /// <summary>
    /// Builds a <see cref="YamlConverter{T}"/> instance from the reflection-based shape provider.
    /// </summary>
    /// <typeparam name="T">The type for which to build the converter.</typeparam>
    /// <returns>A <see cref="YamlConverter{T}"/> instance.</returns>
    [RequiresUnreferencedCode("PolyType reflection provider requires unreferenced code")]
    [RequiresDynamicCode("PolyType reflection provider requires dynamic code")]
    public static YamlConverter<T> CreateConverterUsingReflection<T>() =>
        CreateConverter<T>(ReflectionProvider.ReflectionTypeShapeProvider.Default);

    /// <summary>
    /// Serializes a value to a YAML string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="converter">The converter used to serialize the value.</param>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A YAML encoded string containing the serialized value.</returns>
    public static string Serialize<T>(this YamlConverter<T> converter, T? value)
    {
        var writer = new YamlWriter();

        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            converter.Write(writer, value);
        }

        return writer.ToString();
    }

    /// <summary>
    /// Deserializes a value from a YAML string using the provided converter.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="converter">The converter used to deserialize the value.</param>
    /// <param name="yaml">The YAML encoding to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(this YamlConverter<T> converter, string yaml)
    {
        var reader = new YamlReader(yaml);

        if (reader.TryReadNull())
        {
            return default;
        }

        return converter.Read(reader);
    }

#if NET
    /// <summary>
    /// Serializes a value to a YAML string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A YAML encoded string containing the serialized value.</returns>
    public static string Serialize<T>(T? value) where T : IShapeable<T> =>
        YamlSerializerCache<T, T>.Value.Serialize(value);

    /// <summary>
    /// Deserializes a value from a YAML string using its <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <param name="yaml">The YAML encoding to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(string yaml) where T : IShapeable<T> =>
        YamlSerializerCache<T, T>.Value.Deserialize(yaml);

    /// <summary>
    /// Serializes a value to a YAML string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="value">The value to be serialized.</param>
    /// <returns>A YAML encoded string containing the serialized value.</returns>
    public static string Serialize<T, TProvider>(T? value) where TProvider : IShapeable<T> =>
        YamlSerializerCache<T, TProvider>.Value.Serialize(value);

    /// <summary>
    /// Deserializes a value from a YAML string using an externally provided <see cref="IShapeable{T}"/> implementation.
    /// </summary>
    /// <typeparam name="T">The type of the value to deserialize.</typeparam>
    /// <typeparam name="TProvider">The type providing an <see cref="IShapeable{T}"/> implementation.</typeparam>
    /// <param name="yaml">The YAML encoding to be deserialized.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T, TProvider>(string yaml) where TProvider : IShapeable<T> =>
        YamlSerializerCache<T, TProvider>.Value.Deserialize(yaml);

    private static class YamlSerializerCache<T, TProvider> where TProvider : IShapeable<T>
    {
        public static YamlConverter<T> Value => s_value ??= CreateConverter(TProvider.GetTypeShape());
        private static YamlConverter<T>? s_value;
    }
#endif
}
