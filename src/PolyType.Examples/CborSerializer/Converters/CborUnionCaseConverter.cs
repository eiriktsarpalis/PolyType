using System.Diagnostics;
using System.Formats.Cbor;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class CborUnionCaseConverter<TUnionCase, TUnion>(CborConverter<TUnionCase> underlying, IMarshaler<TUnionCase, TUnion> marshaler) : CborConverter<TUnion>
{
    public override TUnion? Read(CborReader reader) => marshaler.Marshal(underlying.Read(reader));
    public override void Write(CborWriter writer, TUnion? value) => underlying.Write(writer, marshaler.Unmarshal(value));
}
