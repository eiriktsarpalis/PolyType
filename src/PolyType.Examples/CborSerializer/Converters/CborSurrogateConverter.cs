using System.Formats.Cbor;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class CborSurrogateConverter<T, TSurrogate>(IMarshaller<T, TSurrogate> marshaller, CborConverter<TSurrogate> surrogateConverter) : CborConverter<T>
{
    public override void Write(CborWriter writer, T? value) =>
        surrogateConverter.Write(writer, marshaller.ToSurrogate(value));

    public override T? Read(CborReader reader) =>
        marshaller.FromSurrogate(surrogateConverter.Read(reader));
}