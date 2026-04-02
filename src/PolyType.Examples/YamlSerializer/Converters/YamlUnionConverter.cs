using PolyType.Abstractions;

namespace PolyType.Examples.YamlSerializer.Converters;

internal sealed class YamlUnionConverter<TUnion>(
    Getter<TUnion, int> getUnionCaseIndex,
    YamlConverter<TUnion> baseConverter,
    KeyValuePair<string, YamlConverter<TUnion>>[] unionCaseConverters) : YamlConverter<TUnion>
{
    private const string TypeKey = "_type";
    private readonly Dictionary<string, YamlConverter<TUnion>> _unionCaseConverters = unionCaseConverters.ToDictionary(p => p.Key, p => p.Value);

    public override TUnion? Read(YamlReader reader)
    {
        if (default(TUnion) is null && reader.TryReadNull())
        {
            return default;
        }

        reader.ReadMappingStart();

        if (reader.TryReadMappingKey(out string firstKey) && firstKey == TypeKey)
        {
            string? typeName = reader.ReadScalar();

            if (typeName is not null && _unionCaseConverters.TryGetValue(typeName, out YamlConverter<TUnion>? derivedConverter))
            {
                TUnion? result = derivedConverter.ReadMappingContent(reader);
                reader.ReadMappingEnd();

                return result;
            }

            throw new InvalidOperationException($"Unrecognized union case type '{typeName}'.");
        }

        return baseConverter.ReadMappingContent(reader);
    }

    public override void Write(YamlWriter writer, TUnion value)
    {
        int index = getUnionCaseIndex(ref value);
        if (index < 0)
        {
            baseConverter.Write(writer, value);
            return;
        }

        var entry = unionCaseConverters[index];
        writer.BeginMapping();
        writer.WriteKey(TypeKey);
        writer.WriteString(entry.Key);
        entry.Value.WriteMappingContent(writer, value);
        writer.EndMapping();
    }
}
