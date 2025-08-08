using System.Formats.Cbor;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class CborSurrogateConverter<T, TSurrogate>(IMarshaler<T, TSurrogate> marshaler, CborConverter<TSurrogate> surrogateConverter) : CborConverter<T>
{
    public override void Write(CborWriter writer, T? value) =>
        surrogateConverter.Write(writer, marshaler.Marshal(value));

    public override T? Read(CborReader reader) =>
        marshaler.Unmarshal(surrogateConverter.Read(reader));
}