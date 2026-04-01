using PolyType.Abstractions;
using PolyType.Examples.Utilities;

namespace PolyType.Examples.YamlSerializer.Converters;

internal class YamlObjectConverter<T>(YamlPropertyConverter<T>[] properties) : YamlConverter<T>
{
    private readonly YamlPropertyConverter<T>[] _propertiesToWrite = properties.Where(prop => prop.HasGetter).ToArray();

    public override T? Read(YamlReader reader)
    {
        if (default(T) is null && reader.TryReadNull())
        {
            return default;
        }

        if (reader.TryReadEmptyMapping())
        {
            throw new NotSupportedException($"Deserialization for type {typeof(T)} is not supported.");
        }

        throw new NotSupportedException($"Deserialization for type {typeof(T)} is not supported.");
    }

    public sealed override void Write(YamlWriter writer, T value)
    {
        if (_propertiesToWrite.Length == 0)
        {
            writer.WriteRawScalar("{}");
            return;
        }

        writer.BeginMapping();
        foreach (YamlPropertyConverter<T> property in _propertiesToWrite)
        {
            property.Write(writer, ref value);
        }

        writer.EndMapping();
    }
}

internal sealed class YamlObjectConverterWithDefaultCtor<T>(Func<T> defaultConstructor, YamlPropertyConverter<T>[] properties) : YamlObjectConverter<T>(properties)
{
    private readonly Dictionary<string, YamlPropertyConverter<T>> _propertiesToRead = properties.Where(prop => prop.HasSetter).ToDictionary(prop => prop.Name);

    public sealed override T? Read(YamlReader reader)
    {
        if (default(T) is null && reader.TryReadNull())
        {
            return default;
        }

        if (reader.TryReadEmptyMapping())
        {
            return defaultConstructor();
        }

        T result = defaultConstructor();
        int expectedIndent = reader.CurrentIndent;

        Dictionary<string, YamlPropertyConverter<T>> propertiesToRead = _propertiesToRead;

        while (reader.TryReadMappingEntry(expectedIndent, out string key, out string? inlineValue))
        {
            if (!propertiesToRead.TryGetValue(key, out YamlPropertyConverter<T>? propConverter))
            {
                if (inlineValue is null)
                {
                    reader.SkipNode(expectedIndent);
                }

                continue;
            }

            propConverter.Read(reader, inlineValue, ref result);
        }

        return result;
    }
}

internal sealed class YamlObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
    Func<TArgumentState> createArgumentState,
    Constructor<TArgumentState, TDeclaringType> createObject,
    YamlPropertyConverter<TArgumentState>[] constructorParameters,
    YamlPropertyConverter<TDeclaringType>[] properties,
    IReadOnlyList<IParameterShape> parameters) : YamlObjectConverter<TDeclaringType>(properties)
    where TArgumentState : IArgumentState
{
    private readonly Dictionary<string, YamlPropertyConverter<TArgumentState>> _constructorParameters = constructorParameters
        .ToDictionary(param => param.Name, StringComparer.Ordinal);

    public override TDeclaringType? Read(YamlReader reader)
    {
        if (default(TDeclaringType) is null && reader.TryReadNull())
        {
            return default;
        }

        if (reader.TryReadEmptyMapping())
        {
            TArgumentState emptyState = createArgumentState();
            if (!emptyState.AreRequiredArgumentsSet)
            {
                Helpers.ThrowMissingRequiredArguments(ref emptyState, parameters);
            }

            return createObject(ref emptyState);
        }

        TArgumentState argumentState = createArgumentState();
        int expectedIndent = reader.CurrentIndent;

        Dictionary<string, YamlPropertyConverter<TArgumentState>> ctorParams = _constructorParameters;

        while (reader.TryReadMappingEntry(expectedIndent, out string key, out string? inlineValue))
        {
            if (!ctorParams.TryGetValue(key, out YamlPropertyConverter<TArgumentState>? propertyConverter))
            {
                if (inlineValue is null)
                {
                    reader.SkipNode(expectedIndent);
                }

                continue;
            }

            propertyConverter.Read(reader, inlineValue, ref argumentState);
        }

        if (!argumentState.AreRequiredArgumentsSet)
        {
            Helpers.ThrowMissingRequiredArguments(ref argumentState, parameters);
        }

        return createObject(ref argumentState);
    }
}
