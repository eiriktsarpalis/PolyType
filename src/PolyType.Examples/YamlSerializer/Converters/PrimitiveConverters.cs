using PolyType.Utilities;
using System.Globalization;
using System.Numerics;

namespace PolyType.Examples.YamlSerializer.Converters;

internal sealed class BoolConverter : YamlConverter<bool>
{
    public override bool Read(YamlReader reader) => bool.Parse(reader.ReadScalar()!);
    public override void Write(YamlWriter writer, bool value) => writer.WriteBool(value);
}

internal sealed class ByteConverter : YamlConverter<byte>
{
    public override byte Read(YamlReader reader) => byte.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, byte value) => writer.WriteInteger(value);
}

internal sealed class UInt16Converter : YamlConverter<ushort>
{
    public override ushort Read(YamlReader reader) => ushort.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, ushort value) => writer.WriteInteger(value);
}

internal sealed class UInt32Converter : YamlConverter<uint>
{
    public override uint Read(YamlReader reader) => uint.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, uint value) => writer.WriteUnsignedInteger(value);
}

internal sealed class UInt64Converter : YamlConverter<ulong>
{
    public override ulong Read(YamlReader reader) => ulong.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, ulong value) => writer.WriteUnsignedInteger(value);
}

#if NET
internal sealed class UInt128Converter : YamlConverter<UInt128>
{
    public override UInt128 Read(YamlReader reader) => UInt128.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, UInt128 value) => writer.WriteRawScalar(value.ToString(CultureInfo.InvariantCulture));
}
#endif

internal sealed class SByteConverter : YamlConverter<sbyte>
{
    public override sbyte Read(YamlReader reader) => sbyte.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, sbyte value) => writer.WriteInteger(value);
}

internal sealed class Int16Converter : YamlConverter<short>
{
    public override short Read(YamlReader reader) => short.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, short value) => writer.WriteInteger(value);
}

internal sealed class Int32Converter : YamlConverter<int>
{
    public override int Read(YamlReader reader) => int.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, int value) => writer.WriteInteger(value);
}

internal sealed class Int64Converter : YamlConverter<long>
{
    public override long Read(YamlReader reader) => long.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, long value) => writer.WriteInteger(value);
}

#if NET
internal sealed class Int128Converter : YamlConverter<Int128>
{
    public override Int128 Read(YamlReader reader) => Int128.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, Int128 value) => writer.WriteRawScalar(value.ToString(CultureInfo.InvariantCulture));
}
#endif

internal sealed class StringConverter : YamlConverter<string>
{
    public override string? Read(YamlReader reader)
    {
        if (reader.TryReadNull())
        {
            return null;
        }

        return reader.ReadScalar();
    }

    public override void Write(YamlWriter writer, string value) => writer.WriteString(value);
}

internal sealed class CharConverter : YamlConverter<char>
{
    public override char Read(YamlReader reader) => reader.ReadScalar()![0];
    public override void Write(YamlWriter writer, char value) => writer.WriteString(value.ToString(CultureInfo.InvariantCulture));
}

#if NET
internal sealed class RuneConverter : YamlConverter<System.Text.Rune>
{
    public override System.Text.Rune Read(YamlReader reader) => System.Text.Rune.GetRuneAt(reader.ReadScalar()!, 0);
    public override void Write(YamlWriter writer, System.Text.Rune value) => writer.WriteString(value.ToString());
}

internal sealed class HalfConverter : YamlConverter<Half>
{
    public override Half Read(YamlReader reader) => Half.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, Half value) => writer.WriteRawScalar(value.ToString(CultureInfo.InvariantCulture));
}
#endif

internal sealed class SingleConverter : YamlConverter<float>
{
    public override float Read(YamlReader reader) => float.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, float value) => writer.WriteFloatingPoint(value);
}

internal sealed class DoubleConverter : YamlConverter<double>
{
    public override double Read(YamlReader reader) => double.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, double value) => writer.WriteFloatingPoint(value);
}

internal sealed class DecimalConverter : YamlConverter<decimal>
{
    public override decimal Read(YamlReader reader) => decimal.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, decimal value) => writer.WriteRawScalar(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class BigIntegerConverter : YamlConverter<BigInteger>
{
    public override BigInteger Read(YamlReader reader) => BigInteger.Parse(reader.ReadScalar()!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, BigInteger value) => writer.WriteRawScalar(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class ByteArrayConverter : YamlConverter<byte[]>
{
    public override byte[]? Read(YamlReader reader)
    {
        if (reader.TryReadNull())
        {
            return null;
        }

        return Convert.FromBase64String(reader.ReadScalar()!);
    }

    public override void Write(YamlWriter writer, byte[] value) => writer.WriteString(Convert.ToBase64String(value));
}

internal sealed class GuidConverter : YamlConverter<Guid>
{
    public override Guid Read(YamlReader reader) => Guid.Parse(reader.ReadScalar()!);
    public override void Write(YamlWriter writer, Guid value) => writer.WriteString(value.ToString());
}

internal sealed class DateTimeConverter : YamlConverter<DateTime>
{
    public override DateTime Read(YamlReader reader) => DateTime.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, DateTime value) => writer.WriteString(value.ToString("O", CultureInfo.InvariantCulture));
}

internal sealed class DateTimeOffsetConverter : YamlConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(YamlReader reader) => DateTimeOffset.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, DateTimeOffset value) => writer.WriteString(value.ToString("O", CultureInfo.InvariantCulture));
}

internal sealed class TimeSpanConverter : YamlConverter<TimeSpan>
{
    public override TimeSpan Read(YamlReader reader) => TimeSpan.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, TimeSpan value) => writer.WriteString(value.ToString());
}

#if NET
internal sealed class DateOnlyConverter : YamlConverter<DateOnly>
{
    public override DateOnly Read(YamlReader reader) => DateOnly.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, DateOnly value) => writer.WriteString(value.ToString("O", CultureInfo.InvariantCulture));
}

internal sealed class TimeOnlyConverter : YamlConverter<TimeOnly>
{
    public override TimeOnly Read(YamlReader reader) => TimeOnly.Parse(reader.ReadScalar()!, CultureInfo.InvariantCulture);
    public override void Write(YamlWriter writer, TimeOnly value) => writer.WriteString(value.ToString("O", CultureInfo.InvariantCulture));
}
#endif

internal sealed class UriConverter : YamlConverter<Uri>
{
    public override Uri? Read(YamlReader reader)
    {
        if (reader.TryReadNull())
        {
            return null;
        }

        return new Uri(reader.ReadScalar()!, UriKind.RelativeOrAbsolute);
    }

    public override void Write(YamlWriter writer, Uri value) => writer.WriteString(value.ToString());
}

internal sealed class VersionConverter : YamlConverter<Version>
{
    public override Version? Read(YamlReader reader)
    {
        if (reader.TryReadNull())
        {
            return null;
        }

        return Version.Parse(reader.ReadScalar()!);
    }

    public override void Write(YamlWriter writer, Version value) => writer.WriteString(value.ToString());
}

internal sealed class ObjectConverter(TypeCache cache) : YamlConverter<object>
{
    public override object? Read(YamlReader reader)
    {
        if (reader.TryReadNull())
        {
            return null;
        }

        if (reader.IsMappingStart)
        {
            reader.ReadMappingStart();
            reader.ReadMappingEnd();

            return new object();
        }

        string? scalar = reader.ReadScalar();
        if (scalar is null)
        {
            return new object();
        }

        if (scalar.Equals("true", StringComparison.Ordinal))
        {
            return true;
        }

        if (scalar.Equals("false", StringComparison.Ordinal))
        {
            return false;
        }

        if (int.TryParse(scalar, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int intVal))
        {
            return intVal;
        }

        if (long.TryParse(scalar, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long longVal))
        {
            return longVal;
        }

        if (double.TryParse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out double dblVal))
        {
            return dblVal;
        }

        return scalar;
    }

    public override void Write(YamlWriter writer, object value)
    {
        Type runtimeType = value.GetType();
        if (runtimeType != typeof(object))
        {
            var derivedConverter = (IYamlConverter)cache.GetOrAdd(runtimeType)!;
            derivedConverter.Write(writer, value);
        }
        else
        {
            writer.BeginMapping();
            writer.EndMapping();
        }
    }
}
