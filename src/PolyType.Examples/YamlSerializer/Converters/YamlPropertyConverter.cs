using PolyType.Abstractions;
using System.Diagnostics;

namespace PolyType.Examples.YamlSerializer.Converters;

internal abstract class YamlPropertyConverter<TDeclaringType>(string name)
{
    public string Name { get; } = name;
    public abstract bool HasGetter { get; }
    public abstract bool HasSetter { get; }
    public bool IsParameter { get; private protected init; }

    public abstract void Write(YamlWriter writer, ref TDeclaringType value);
    public abstract void Read(YamlReader reader, string? inlineValue, ref TDeclaringType value);
}

internal sealed class YamlPropertyConverter<TDeclaringType, TPropertyType> : YamlPropertyConverter<TDeclaringType>
{
    private readonly YamlConverter<TPropertyType> _propertyConverter;
    private readonly Getter<TDeclaringType, TPropertyType>? _getter;
    private readonly Setter<TDeclaringType, TPropertyType>? _setter;

    public YamlPropertyConverter(IPropertyShape<TDeclaringType, TPropertyType> property, YamlConverter<TPropertyType> propertyConverter)
        : base(property.Name)
    {
        _propertyConverter = propertyConverter;

        if (property.HasGetter)
        {
            _getter = property.GetGetter();
        }

        if (property.HasSetter)
        {
            _setter = property.GetSetter();
        }
    }

    public YamlPropertyConverter(IParameterShape<TDeclaringType, TPropertyType> parameter, YamlConverter<TPropertyType> propertyConverter)
        : base(parameter.Name)
    {
        _propertyConverter = propertyConverter;
        _setter = parameter.GetSetter();
        IsParameter = parameter.Kind is ParameterKind.MethodParameter;
    }

    public override bool HasGetter => _getter is not null;
    public override bool HasSetter => _setter is not null;

    public override void Read(YamlReader reader, string? inlineValue, ref TDeclaringType declaringType)
    {
        DebugExt.Assert(_setter is not null);

        TPropertyType? result;
        if (inlineValue is not null)
        {
            result = ReadInlineValue(reader, inlineValue);
        }
        else
        {
            result = _propertyConverter.Read(reader);
        }

        _setter(ref declaringType, result!);
    }

    public override void Write(YamlWriter writer, ref TDeclaringType declaringType)
    {
        DebugExt.Assert(_getter is not null);
        TPropertyType value = _getter(ref declaringType);
        writer.WriteKey(Name);

        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            _propertyConverter.Write(writer, value);
        }
    }

    private TPropertyType? ReadInlineValue(YamlReader reader, string inlineValue)
    {
        // For inline values, we need to parse the scalar directly
        // Create a mini reader for the inline value
        var inlineReader = new YamlReader(inlineValue);

        return _propertyConverter.Read(inlineReader);
    }
}
