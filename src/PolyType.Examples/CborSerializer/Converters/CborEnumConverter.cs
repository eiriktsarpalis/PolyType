using System.Formats.Cbor;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class CborEnumConverter<TEnum, TUnderlying>(IReadOnlyDictionary<string, TUnderlying> members, IReadOnlyDictionary<TUnderlying, string> reverseLookup) : CborConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(CborReader reader)
    {
        return reader.PeekState() switch
        {
            CborReaderState.TextString => (TEnum)(object)members[reader.ReadTextString()]!,
            CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger when typeof(TUnderlying) == typeof(int) => (TEnum)(object)reader.ReadInt32(),
            _ => throw new NotSupportedException("Unexpected token type."),
        };
    }

    public override void Write(CborWriter writer, TEnum value)
    {
        if (reverseLookup.TryGetValue((TUnderlying)(object)value, out string? name))
        {
            writer.WriteTextString(name);
        }
        else if (typeof(TUnderlying) == typeof(int))
        {
            writer.WriteInt32((int)(object)value);
        }
        else
        {
            throw new NotSupportedException();
        }
    }
}
