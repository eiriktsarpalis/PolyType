using PolyType.Abstractions;

namespace PolyType.Examples.YamlSerializer.Converters;

internal sealed class YamlOptionalConverter<TOptional, TElement>(
    YamlConverter<TElement> elementConverter,
    OptionDeconstructor<TOptional, TElement> deconstructor,
    Func<TOptional> createNone,
    Func<TElement, TOptional> createSome) : YamlConverter<TOptional>
{
    public override TOptional? Read(YamlReader reader) => reader.TryReadNull() ? createNone() : createSome(elementConverter.Read(reader)!);
    public override void Write(YamlWriter writer, TOptional value)
    {
        if (deconstructor(value, out TElement? element))
        {
            elementConverter.Write(writer, element);
            return;
        }

        writer.WriteNull();
    }
}
