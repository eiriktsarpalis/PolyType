using PolyType.Utilities;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Xml;

namespace PolyType.Examples.XmlSerializer.Converters;

internal sealed class BoolConverter : XmlConverter<bool>
{
    public override bool Read(XmlReader reader) => reader.ReadElementContentAsBoolean();
    public override void Write(XmlWriter writer, bool value) => writer.WriteValue(value);
}

internal sealed class ByteConverter : XmlConverter<byte>
{
    public override byte Read(XmlReader reader) => (byte)reader.ReadElementContentAsInt();
    public override void Write(XmlWriter writer, byte value) => writer.WriteValue(value);
}

internal sealed class UInt16Converter : XmlConverter<ushort>
{
    public override ushort Read(XmlReader reader) => (ushort)reader.ReadElementContentAsInt();
    public override void Write(XmlWriter writer, ushort value) => writer.WriteValue(value);
}

internal sealed class UInt32Converter : XmlConverter<uint>
{
    public override uint Read(XmlReader reader) => (uint)reader.ReadElementContentAsLong();
    public override void Write(XmlWriter writer, uint value) => writer.WriteValue(value);
}

internal sealed class UInt64Converter : XmlConverter<ulong>
{
    public override ulong Read(XmlReader reader) => ulong.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
    public override void Write(XmlWriter writer, ulong value) => writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
}

#if NET
internal sealed class UInt128Converter : XmlConverter<UInt128>
{
    public override UInt128 Read(XmlReader reader) => UInt128.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
    public override void Write(XmlWriter writer, UInt128 value) => writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
}
#endif

internal sealed class SByteConverter : XmlConverter<sbyte>
{
    public override sbyte Read(XmlReader reader) => (sbyte)reader.ReadElementContentAsInt();
    public override void Write(XmlWriter writer, sbyte value) => writer.WriteValue(value);
}

internal sealed class Int16Converter : XmlConverter<short>
{
    public override short Read(XmlReader reader) => (short)reader.ReadElementContentAsInt();
    public override void Write(XmlWriter writer, short value) => writer.WriteValue(value);
}

internal sealed class Int32Converter : XmlConverter<int>
{
    public override int Read(XmlReader reader) => reader.ReadElementContentAsInt();
    public override void Write(XmlWriter writer, int value) => writer.WriteValue(value);
}

internal sealed class Int64Converter : XmlConverter<long>
{
    public override long Read(XmlReader reader) => reader.ReadElementContentAsLong();
    public override void Write(XmlWriter writer, long value) => writer.WriteValue(value);
}

#if NET
internal sealed class Int128Converter : XmlConverter<Int128>
{
    public override Int128 Read(XmlReader reader) => Int128.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
    public override void Write(XmlWriter writer, Int128 value) => writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
}
#endif

internal sealed class StringConverter : XmlConverter<string>
{
    public override string? Read(XmlReader reader) => reader.TryReadNullElement() ? null : reader.ReadElementContentAsString();
    public override void Write(XmlWriter writer, string value) => writer.WriteValue(value);
}

internal sealed class CharConverter : XmlConverter<char>
{
    public override char Read(XmlReader reader) => reader.ReadElementContentAsString()[0];
    public override void Write(XmlWriter writer, char value) => writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
}

#if NET
internal sealed class RuneConverter : XmlConverter<Rune>
{
    public override Rune Read(XmlReader reader) => Rune.GetRuneAt(reader.ReadElementContentAsString(), 0);
    public override void Write(XmlWriter writer, Rune value) => writer.WriteValue(value.ToString());
}

internal sealed class HalfConverter : XmlConverter<Half>
{
    public override Half Read(XmlReader reader) => Half.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
    public override void Write(XmlWriter writer, Half value) => writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
}
#endif

internal sealed class SingleConverter : XmlConverter<float>
{
    public override float Read(XmlReader reader) => reader.ReadElementContentAsFloat();
    public override void Write(XmlWriter writer, float value) => writer.WriteValue(value);
}

internal sealed class DoubleConverter : XmlConverter<double>
{
    public override double Read(XmlReader reader) => reader.ReadElementContentAsDouble();
    public override void Write(XmlWriter writer, double value) => writer.WriteValue(value);
}

internal sealed class DecimalConverter : XmlConverter<decimal>
{
    public override decimal Read(XmlReader reader) => reader.ReadElementContentAsDecimal();
    public override void Write(XmlWriter writer, decimal value) => writer.WriteValue(value);
}

internal sealed class BigIntegerConverter : XmlConverter<BigInteger>
{
    public override BigInteger Read(XmlReader reader) => BigInteger.Parse(reader.ReadElementContentAsString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    public override void Write(XmlWriter writer, BigInteger value) => writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class ByteArrayConverter : XmlConverter<byte[]>
{
    public override byte[]? Read(XmlReader reader) => reader.TryReadNullElement() ? null : Convert.FromBase64String(reader.ReadElementContentAsString());
    public override void Write(XmlWriter writer, byte[] value) => writer.WriteValue(Convert.ToBase64String(value));
}

internal sealed class GuidConverter : XmlConverter<Guid>
{
    public override Guid Read(XmlReader reader) => Guid.Parse(reader.ReadElementContentAsString());
    public override void Write(XmlWriter writer, Guid value) => writer.WriteValue(value.ToString());
}

internal sealed class DateTimeConverter : XmlConverter<DateTime>
{
    public override DateTime Read(XmlReader reader) => DateTime.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
    public override void Write(XmlWriter writer, DateTime value) => writer.WriteValue(value);
}

internal sealed class DateTimeOffsetConverter : XmlConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(XmlReader reader) => DateTimeOffset.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, DateTimeOffset value) => writer.WriteValue(value);
}

internal sealed class TimeSpanConverter : XmlConverter<TimeSpan>
{
    public override TimeSpan Read(XmlReader reader) => TimeSpan.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
    public override void Write(XmlWriter writer, TimeSpan value) => writer.WriteValue(value.ToString());
}

#if NET
internal sealed class DateOnlyConverter : XmlConverter<DateOnly>
{
    public override DateOnly Read(XmlReader reader) => DateOnly.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);

    public override void Write(XmlWriter writer, DateOnly value) => writer.WriteValue(value.ToString("O", CultureInfo.InvariantCulture));
}
#endif

internal sealed class UriConverter : XmlConverter<Uri>
{
    public override Uri? Read(XmlReader reader) => reader.TryReadNullElement() ? null : new Uri(reader.ReadElementContentAsString(), UriKind.RelativeOrAbsolute);
    public override void Write(XmlWriter writer, Uri value) => writer.WriteValue(value);
}

internal sealed class VersionConverter : XmlConverter<Version>
{
    public override Version? Read(XmlReader reader) => reader.TryReadNullElement() ? null : Version.Parse(reader.ReadElementContentAsString());
    public override void Write(XmlWriter writer, Version value) => writer.WriteValue(value.ToString());
}

#if NET
internal sealed class TimeOnlyConverter : XmlConverter<TimeOnly>
{
    public override TimeOnly Read(XmlReader reader) => TimeOnly.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
    public override void Write(XmlWriter writer, TimeOnly value) => writer.WriteValue(value.ToString("O", CultureInfo.InvariantCulture));
}
#endif

internal sealed class ObjectConverter(TypeCache cache) : XmlConverter<object>
{
    public override object? Read(XmlReader reader)
    {
        if (reader.TryReadNullElement())
        {
            return null;
        }

        if (reader.IsEmptyElement)
        {
            reader.ReadStartElement();
            return new object();
        }

        return reader.ReadElementContentAsString();
    }

    public override void Write(XmlWriter writer, object value)
    {
        Type runtimeType = value.GetType();
        if (runtimeType != typeof(object))
        {
            var derivedConverter = (IXmlConverter)cache.GetOrAdd(runtimeType)!;
            derivedConverter.Write(writer, value);
        }
    }
}