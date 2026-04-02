using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace PolyType.Examples.YamlSerializer;

/// <summary>
/// A YAML writer that wraps YamlDotNet's <see cref="Emitter"/>.
/// </summary>
public sealed class YamlWriter : IDisposable
{
    private readonly StringWriter _stringWriter;
    private readonly Emitter _emitter;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlWriter"/> class.
    /// </summary>
    public YamlWriter()
    {
        _stringWriter = new StringWriter();
        _emitter = new Emitter(_stringWriter);
        _emitter.Emit(new StreamStart());
        _emitter.Emit(new DocumentStart());
    }

    /// <summary>
    /// Writes a null value.
    /// </summary>
    public void WriteNull() => WriteRawScalar("null", ScalarStyle.Plain);

    /// <summary>
    /// Writes a string value, selecting the appropriate YAML quoting style.
    /// </summary>
    public void WriteString(string value)
    {
        if (value.Length == 0)
        {
            WriteRawScalar(value, ScalarStyle.SingleQuoted);
            return;
        }

        if (NeedsDoubleQuoting(value))
        {
            WriteRawScalar(value, ScalarStyle.DoubleQuoted);
            return;
        }

        if (NeedsQuoting(value))
        {
            WriteRawScalar(value, ScalarStyle.SingleQuoted);
            return;
        }

        WriteRawScalar(value, ScalarStyle.Plain);
    }

    /// <summary>
    /// Writes an integer value.
    /// </summary>
    public void WriteInteger(long value) => WriteRawScalar(value.ToString(CultureInfo.InvariantCulture), ScalarStyle.Plain);

    /// <summary>
    /// Writes an unsigned integer value.
    /// </summary>
    public void WriteUnsignedInteger(ulong value) => WriteRawScalar(value.ToString(CultureInfo.InvariantCulture), ScalarStyle.Plain);

    /// <summary>
    /// Writes a floating-point value.
    /// </summary>
    public void WriteFloatingPoint(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            WriteRawScalar(".inf", ScalarStyle.Plain);
        }
        else if (double.IsNegativeInfinity(value))
        {
            WriteRawScalar("-.inf", ScalarStyle.Plain);
        }
        else if (double.IsNaN(value))
        {
            WriteRawScalar(".nan", ScalarStyle.Plain);
        }
        else
        {
            WriteRawScalar(value.ToString("G", CultureInfo.InvariantCulture), ScalarStyle.Plain);
        }
    }

    /// <summary>
    /// Writes a boolean value.
    /// </summary>
    public void WriteBool(bool value) => WriteRawScalar(value ? "true" : "false", ScalarStyle.Plain);

    /// <summary>
    /// Begins a YAML mapping (object).
    /// </summary>
    public void BeginMapping() => _emitter.Emit(new MappingStart(null, null, isImplicit: true, MappingStyle.Block));

    /// <summary>
    /// Ends a YAML mapping (object).
    /// </summary>
    public void EndMapping() => _emitter.Emit(new MappingEnd());

    /// <summary>
    /// Writes a mapping key.
    /// </summary>
    public void WriteKey(string key)
    {
        ScalarStyle style = NeedsDoubleQuoting(key) ? ScalarStyle.DoubleQuoted
            : KeyNeedsQuoting(key) ? ScalarStyle.SingleQuoted
            : ScalarStyle.Plain;
        bool isPlainImplicit = style is ScalarStyle.Plain;
        bool isQuotedImplicit = style is not ScalarStyle.Plain;
        _emitter.Emit(new Scalar(null, null, key, style, isPlainImplicit, isQuotedImplicit));
    }

    /// <summary>
    /// Begins a YAML sequence (array/list).
    /// </summary>
    public void BeginSequence() => _emitter.Emit(new SequenceStart(null, null, isImplicit: true, SequenceStyle.Block));

    /// <summary>
    /// Ends a YAML sequence (array/list).
    /// </summary>
    public void EndSequence() => _emitter.Emit(new SequenceEnd());

    /// <summary>
    /// Returns the generated YAML string.
    /// </summary>
    public override string ToString()
    {
        _emitter.Emit(new DocumentEnd(true));
        _emitter.Emit(new StreamEnd());
        string result = _stringWriter.ToString();

        // Normalize line endings and trim trailing whitespace/document markers
        result = result.Replace("\r\n", "\n");
        result = result.TrimEnd('\n', '\r', '.').TrimEnd();

        // Strip leading document start marker ("---") if present
        if (result.StartsWith("--- ", StringComparison.Ordinal))
        {
            result = result.Substring(4);
        }
        else if (result is "---")
        {
            result = string.Empty;
        }

        return result;
    }

    /// <inheritdoc/>
    public void Dispose() => _stringWriter.Dispose();

    internal void WriteRawScalar(string value) => WriteRawScalar(value, ScalarStyle.Plain);

    private void WriteRawScalar(string value, ScalarStyle style)
    {
        bool isPlainImplicit = style is ScalarStyle.Plain;
        bool isQuotedImplicit = style is not ScalarStyle.Plain;
        _emitter.Emit(new Scalar(null, null, value, style, isPlainImplicit, isQuotedImplicit));
    }

    private static bool NeedsQuoting(string value)
    {
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("~", StringComparison.Ordinal) ||
            value.Equals("{}", StringComparison.Ordinal) ||
            value.Equals("[]", StringComparison.Ordinal) ||
            value.Equals(".inf", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("-.inf", StringComparison.OrdinalIgnoreCase) ||
            value.Equals(".nan", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }

        return false;
    }

    private static bool KeyNeedsQuoting(string key)
    {
        return key.Length == 0 || NeedsQuoting(key);
    }

    private static bool NeedsDoubleQuoting(string value)
    {
        foreach (char c in value)
        {
            if (char.IsControl(c) && c is not ' ')
            {
                return true;
            }
        }

        return false;
    }
}
