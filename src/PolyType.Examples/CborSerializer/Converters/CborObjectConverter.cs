using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using System.Diagnostics;
using System.Formats.Cbor;

namespace PolyType.Examples.CborSerializer.Converters;

internal class CborObjectConverter<T>(CborPropertyConverter<T>[] properties) : CborConverter<T>
{
    private readonly CborPropertyConverter<T>[] _propertiesToWrite = properties.Where(prop => prop.HasGetter).ToArray();

    public override T? Read(CborReader reader)
    {
        if (default(T) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        throw new NotSupportedException($"Deserialization for type {typeof(T)} is not supported.");
    }

    public sealed override void Write(CborWriter writer, T? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartMap(_propertiesToWrite.Length);
        foreach (CborPropertyConverter<T> property in _propertiesToWrite)
        {
            writer.WriteTextString(property.Name);
            property.Write(writer, ref value);
        }

        writer.WriteEndMap();
    }

    protected static Exception CreateDuplicatePropertyException(string key) =>
        new InvalidOperationException($"Duplicate property '{key}' found in CBOR object.");
}

internal sealed class CborObjectConverterWithDefaultCtor<T>(Func<T> defaultConstructor, CborPropertyConverter<T>[] propertyConverters, IReadOnlyList<IPropertyShape> properties) : CborObjectConverter<T>(propertyConverters)
{
    private readonly Dictionary<string, CborPropertyConverter<T>> _propertiesToRead = propertyConverters.Where(prop => prop.HasSetter).ToDictionary(prop => prop.Name);

    public sealed override T? Read(CborReader reader)
    {
        if (default(T) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        reader.ReadStartMap();
        T result = defaultConstructor();
        Dictionary<string, CborPropertyConverter<T>> propertiesToRead = _propertiesToRead;
        DuplicatePropertyValidator validator = new(properties, static prop => CreateDuplicatePropertyException(prop.Name));

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            string key = reader.ReadTextString();

            if (!propertiesToRead.TryGetValue(key, out CborPropertyConverter<T>? propConverter))
            {
                reader.SkipValue();
                continue;
            }

            validator.MarkAsRead(propConverter.Position);
            propConverter.Read(reader, ref result);
        }

        reader.ReadEndMap();
        return result;
    }
}

internal sealed class CborObjectConverterWithParameterizedCtor<TDeclaringType, TArgumentState>(
    Func<TArgumentState> createArgumentState,
    Constructor<TArgumentState, TDeclaringType> createObject,
    CborPropertyConverter<TArgumentState>[] constructorParameters,
    CborPropertyConverter<TDeclaringType>[] properties,
    IReadOnlyList<IParameterShape> parameters) : CborObjectConverter<TDeclaringType>(properties)
    where TArgumentState : IArgumentState
{
    private readonly Dictionary<string, CborPropertyConverter<TArgumentState>> _constructorParameters = constructorParameters
        .ToDictionary(prop => prop.Name, StringComparer.Ordinal);

    public override TDeclaringType? Read(CborReader reader)
    {
        if (default(TDeclaringType) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        reader.ReadStartMap();
        TArgumentState argumentState = createArgumentState();
        Dictionary<string, CborPropertyConverter<TArgumentState>> ctorParams = _constructorParameters;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            string key = reader.ReadTextString();
            if (!ctorParams.TryGetValue(key, out CborPropertyConverter<TArgumentState>? propertyConverter))
            {
                reader.SkipValue();
                continue;
            }

            if (argumentState.IsArgumentSet(propertyConverter.Position))
            {
                ThrowDuplicateProperty(key);
                static void ThrowDuplicateProperty(string key) => throw CreateDuplicatePropertyException(key);
            }

            propertyConverter.Read(reader, ref argumentState);
            Debug.Assert(argumentState.IsArgumentSet(propertyConverter.Position));
        }

        reader.ReadEndMap();

        if (!argumentState.AreRequiredArgumentsSet)
        {
            Helpers.ThrowMissingRequiredArguments(ref argumentState, parameters);
        }

        return createObject(ref argumentState);
    }
}