using System.Diagnostics;
using System.Formats.Cbor;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class CborUnionCaseConverter<TUnionCase, TUnion>(CborConverter<TUnionCase> underlying) : CborConverter<TUnion>
    where TUnionCase : TUnion
{
    public override TUnion? Read(CborReader reader) => underlying.Read(reader);
    public override void Write(CborWriter writer, TUnion? value) => underlying.Write(writer, (TUnionCase)value!);
}
