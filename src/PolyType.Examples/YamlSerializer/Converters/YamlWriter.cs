using System.Globalization;
using System.Text;

namespace PolyType.Examples.YamlSerializer;

/// <summary>
/// A simple YAML writer that produces block-style YAML output.
/// </summary>
public sealed class YamlWriter
{
    private readonly StringBuilder _sb = new();
    private int _indent = -1;
    private bool _isInline;
    private bool _needsNewLine;

    /// <summary>
    /// Writes a null value.
    /// </summary>
    public void WriteNull() => WriteRawScalar("null");

    /// <summary>
    /// Writes a string value, quoting it if necessary.
    /// </summary>
    public void WriteString(string value)
    {
        if (value.Length == 0)
        {
            WriteRawScalar("''");
            return;
        }

        if (NeedsQuoting(value))
        {
            WriteRawScalar($"'{EscapeSingleQuoted(value)}'");
            return;
        }

        WriteRawScalar(value);
    }

    /// <summary>
    /// Writes an integer value.
    /// </summary>
    public void WriteInteger(long value) => WriteRawScalar(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Writes an unsigned integer value.
    /// </summary>
    public void WriteUnsignedInteger(ulong value) => WriteRawScalar(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Writes a floating-point value.
    /// </summary>
    public void WriteFloatingPoint(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            WriteRawScalar(".inf");
        }
        else if (double.IsNegativeInfinity(value))
        {
            WriteRawScalar("-.inf");
        }
        else if (double.IsNaN(value))
        {
            WriteRawScalar(".nan");
        }
        else
        {
            WriteRawScalar(value.ToString("G", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a boolean value.
    /// </summary>
    public void WriteBool(bool value) => WriteRawScalar(value ? "true" : "false");

    /// <summary>
    /// Begins a YAML mapping (object).
    /// </summary>
    public void BeginMapping()
    {
        if (_isInline)
        {
            _isInline = false;
            _indent++;
            _needsNewLine = true;
            return;
        }

        _indent++;
    }

    /// <summary>
    /// Ends a YAML mapping (object).
    /// </summary>
    public void EndMapping() => _indent--;

    /// <summary>
    /// Writes a mapping key.
    /// </summary>
    public void WriteKey(string key)
    {
        EmitNewLineIfNeeded();
        WriteIndent();
        if (KeyNeedsQuoting(key))
        {
            _sb.Append('\'');
            _sb.Append(EscapeSingleQuoted(key));
            _sb.Append('\'');
        }
        else
        {
            _sb.Append(key);
        }

        _sb.Append(':');
        _isInline = true;
    }

    /// <summary>
    /// Begins a YAML sequence (array/list).
    /// </summary>
    public void BeginSequence()
    {
        if (_isInline)
        {
            _isInline = false;
            _indent++;
            _needsNewLine = true;
            return;
        }

        _indent++;
    }

    /// <summary>
    /// Ends a YAML sequence (array/list).
    /// </summary>
    public void EndSequence() => _indent--;

    /// <summary>
    /// Writes a sequence item prefix ("-").
    /// </summary>
    public void WriteSequenceEntry()
    {
        EmitNewLineIfNeeded();
        WriteIndent();
        _sb.Append('-');
        _isInline = true;
    }

    /// <summary>
    /// Returns the generated YAML string.
    /// </summary>
    public override string ToString() => _sb.ToString().TrimEnd('\n');

    internal void WriteRawScalar(string value)
    {
        if (_isInline)
        {
            _sb.Append(' ');
            _isInline = false;
        }
        else
        {
            EmitNewLineIfNeeded();
            WriteIndent();
        }

        _sb.Append(value);
        _sb.Append('\n');
    }

    private void EmitNewLineIfNeeded()
    {
        if (_needsNewLine)
        {
            _sb.Append('\n');
            _needsNewLine = false;
        }
    }

    private void WriteIndent()
    {
        int effectiveIndent = Math.Max(0, _indent);
        for (int i = 0; i < effectiveIndent; i++)
        {
            _sb.Append("  ");
        }
    }

    private static bool NeedsQuoting(string value)
    {
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("~", StringComparison.Ordinal) ||
            value.Equals("{}", StringComparison.Ordinal) ||
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

        foreach (char c in value)
        {
            switch (c)
            {
                case ':':
                case '#':
                case '[':
                case ']':
                case '{':
                case '}':
                case '&':
                case '*':
                case '!':
                case '|':
                case '>':
                case '\'':
                case '"':
                case '%':
                case '@':
                case '`':
                case '\n':
                case '\r':
                case '\t':
                    return true;
            }
        }

        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]))
        {
            return true;
        }

        if (value[0] is '-' or '?' or ',' or '.')
        {
            return true;
        }

        return false;
    }

    private static bool KeyNeedsQuoting(string key)
    {
        if (key.Length == 0)
        {
            return true;
        }

        foreach (char c in key)
        {
            if (c is ':' or '#' or '[' or ']' or '{' or '}' or '&' or '*' or '!' or
                '|' or '>' or '\'' or '"' or '%' or '@' or '`' or '\n' or '\r' or '\t' or
                '\f' or '\b' or ',' or '.')
            {
                return true;
            }
        }

        if (char.IsWhiteSpace(key[0]) || char.IsWhiteSpace(key[key.Length - 1]))
        {
            return true;
        }

        if (key[0] is '-' or '?')
        {
            return true;
        }

        return false;
    }

    private static string EscapeSingleQuoted(string value)
    {
        return value.Replace("'", "''");
    }
}
