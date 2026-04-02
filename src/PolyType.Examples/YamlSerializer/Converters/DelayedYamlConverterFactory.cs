using PolyType.Abstractions;
using PolyType.Utilities;

namespace PolyType.Examples.YamlSerializer.Converters;

internal sealed class DelayedYamlConverterFactory : IDelayedValueFactory
{
    public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
        new DelayedValue<YamlConverter<T>>(self => new DelayedYamlConverter<T>(self));

    private sealed class DelayedYamlConverter<T>(DelayedValue<YamlConverter<T>> self) : YamlConverter<T>
    {
        public override T? Read(YamlReader reader) => self.Result.Read(reader);
        public override void Write(YamlWriter writer, T value) => self.Result.Write(writer, value);
        internal override void WriteMappingContent(YamlWriter writer, T value) => self.Result.WriteMappingContent(writer, value);
        internal override T? ReadMappingContent(YamlReader reader) => self.Result.ReadMappingContent(reader);
    }
}
