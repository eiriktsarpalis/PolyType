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

        throw new NotSupportedException($"Deserialization for type {typeof(T)} is not supported.");
    }

    public sealed override void Write(YamlWriter writer, T value)
    {
        writer.BeginMapping();
        WriteMappingContent(writer, value);
        writer.EndMapping();
    }

    internal sealed override void WriteMappingContent(YamlWriter writer, T value)
    {
        foreach (YamlPropertyConverter<T> property in _propertiesToWrite)
        {
            property.Write(writer, ref value);
        }
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

        reader.ReadMappingStart();
        T result = ReadMappingContentCore(reader);
        reader.ReadMappingEnd();

        return result;
    }

    internal sealed override T? ReadMappingContent(YamlReader reader) => ReadMappingContentCore(reader);

    private T ReadMappingContentCore(YamlReader reader)
    {
        T result = defaultConstructor();
        Dictionary<string, YamlPropertyConverter<T>> propertiesToRead = _propertiesToRead;

        while (reader.TryReadMappingKey(out string key))
        {
            if (!propertiesToRead.TryGetValue(key, out YamlPropertyConverter<T>? propConverter))
            {
                reader.SkipValue();
                continue;
            }

            propConverter.Read(reader, ref result);
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

        reader.ReadMappingStart();
        TDeclaringType result = ReadMappingContentCore(reader);
        reader.ReadMappingEnd();

        return result;
    }

    internal override TDeclaringType? ReadMappingContent(YamlReader reader) => ReadMappingContentCore(reader);

    private TDeclaringType ReadMappingContentCore(YamlReader reader)
    {
        TArgumentState argumentState = createArgumentState();
        Dictionary<string, YamlPropertyConverter<TArgumentState>> ctorParams = _constructorParameters;

        while (reader.TryReadMappingKey(out string key))
        {
            if (!ctorParams.TryGetValue(key, out YamlPropertyConverter<TArgumentState>? propertyConverter))
            {
                reader.SkipValue();
                continue;
            }

            propertyConverter.Read(reader, ref argumentState);
        }

        if (!argumentState.AreRequiredArgumentsSet)
        {
            Helpers.ThrowMissingRequiredArguments(ref argumentState, parameters);
        }

        return createObject(ref argumentState);
    }
}
