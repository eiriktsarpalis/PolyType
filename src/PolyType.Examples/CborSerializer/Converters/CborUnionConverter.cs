using PolyType.Abstractions;
using System.Formats.Cbor;
using System.Text.Json;

namespace PolyType.Examples.CborSerializer.Converters;

internal sealed class CborUnionConverter<TUnion>(
    Getter<TUnion, int> getUnionCaseIndex,
    CborConverter<TUnion> baseConverter,
    KeyValuePair<int, CborConverter<TUnion>>[] unionCaseConverters) : CborConverter<TUnion>
{
    private readonly Dictionary<int, CborConverter<TUnion>> _unionCaseIndex = unionCaseConverters.ToDictionary(p => p.Key, p => p.Value);

    public override TUnion? Read(CborReader reader)
    {
        if (default(TUnion) is null && reader.PeekState() is CborReaderState.Null)
        {
            reader.ReadNull();
            return default;
        }

        if (reader.PeekState() is not CborReaderState.Tag)
        {
            return baseConverter.Read(reader);
        }

        int tag = ReadUnionTag(reader, out bool isArrayIndented);
        if (!_unionCaseIndex.TryGetValue(tag, out CborConverter<TUnion>? unionCaseConverter))
        {
            throw new JsonException($"Unrecognized union tag {tag}.");
        }

        TUnion? result = unionCaseConverter.Read(reader);

        if (isArrayIndented)
        {
            reader.ReadEndArray();
        }

        return result;
    }

    public override void Write(CborWriter writer, TUnion? value)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        int index = getUnionCaseIndex(ref value);
        if (index < 0)
        {
            baseConverter.Write(writer, value);
            return;
        }

        var entry = unionCaseConverters[index];
        WriteUnionTag(writer, unionTag: entry.Key, out bool isArrayIndented);
        entry.Value.Write(writer, value);

        if (isArrayIndented)
        {
            writer.WriteEndArray();
        }
    }

    // Union tag encoding per https://cabo.github.io/cbor-discriminated-unions/draft-bormann-cbor-discriminated-unions.html
    private static int ReadUnionTag(CborReader reader, out bool isArrayIndented)
    {
        isArrayIndented = false;
        ulong tag = (ulong)reader.ReadTag();
        switch (tag)
        {
            // Alternatives 0..6 can be encoded with Tag number 185..191
            case >= 185 and <= 191:
                return (int)(tag - 185);

            // Alternatives 7..127 can be encoded with Tag number 1927..2047
            case >= 1927 and <= 2047:
                return (int)(tag - 1920);

            // Alternatives 128+ can be encoded with Tag number 184 and array with two elements: alternative number and data item.
            case 184:
                reader.ReadStartArray();
                int unionTag = reader.ReadInt32();
                isArrayIndented = true;
                return unionTag;

            default:
                return -1;
        }
    }

    private static void WriteUnionTag(CborWriter writer, int unionTag, out bool isArrayIndented)
    {
        isArrayIndented = false;
        switch (unionTag)
        {
            // Alternatives 0..6 can be encoded with Tag number 185..191
            case >= 0 and <= 6:
                writer.WriteTag((CborTag)(185 + unionTag));
                break;

            // Alternatives 7..127 can be encoded with Tag number 1927..2047
            case >= 7 and <= 127:
                writer.WriteTag((CborTag)(1920 + unionTag));
                break;

            // Alternatives 128+ can be encoded with Tag number 184 and array with two elements: alternative number and data item.
            default:
                writer.WriteTag((CborTag)184);
                writer.WriteStartArray(2);
                writer.WriteInt32(unionTag);
                isArrayIndented = true;
                break;
        }
    }
}
