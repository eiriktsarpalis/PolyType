using System.Globalization;

namespace PolyType.Examples.YamlSerializer;

/// <summary>
/// A simple YAML reader/parser that supports block-style YAML.
/// </summary>
public sealed class YamlReader
{
    private readonly string[] _lines;
    private int _lineIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlReader"/> class.
    /// </summary>
    /// <param name="yaml">The YAML string to parse.</param>
    public YamlReader(string yaml)
    {
        _lines = yaml.Split('\n');
        _lineIndex = 0;
    }

    /// <summary>
    /// Gets the current indentation level of the reader.
    /// </summary>
    public int CurrentIndent => _lineIndex < _lines.Length ? GetIndent(_lines[_lineIndex]) : 0;

    /// <summary>
    /// Gets a value indicating whether the reader has reached the end of the input.
    /// </summary>
    public bool IsEof => _lineIndex >= _lines.Length;

    /// <summary>
    /// Reads a scalar value at the current position.
    /// </summary>
    /// <returns>The scalar value as a string, or null if the value is null.</returns>
    public string? ReadScalar()
    {
        if (IsEof)
        {
            throw new InvalidOperationException("Unexpected end of YAML input.");
        }

        string line = _lines[_lineIndex].TrimStart();
        _lineIndex++;

        return ParseScalarValue(line);
    }

    /// <summary>
    /// Reads an inline scalar value (the value part after "key: value").
    /// </summary>
    /// <param name="rawValue">The raw inline value to parse.</param>
    /// <returns>The scalar value as a string, or null if the value is null.</returns>
    public static string? ReadInlineScalar(string rawValue)
    {
        return ParseScalarValue(rawValue.Trim());
    }

    /// <summary>
    /// Determines whether the current line is a null value.
    /// </summary>
    public bool IsNull()
    {
        if (IsEof)
        {
            return false;
        }

        string line = _lines[_lineIndex].TrimStart();

        return line is "null" or "~" or "";
    }

    /// <summary>
    /// Reads and consumes a null value, returning true if successful.
    /// </summary>
    public bool TryReadNull()
    {
        if (IsNull())
        {
            _lineIndex++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Peeks at the current line to determine what kind of YAML node it is.
    /// </summary>
    public YamlNodeKind PeekNodeKind()
    {
        if (IsEof)
        {
            return YamlNodeKind.Scalar;
        }

        string line = _lines[_lineIndex].TrimStart();

        if (line.StartsWith("- ", StringComparison.Ordinal))
        {
            return YamlNodeKind.Sequence;
        }

        // Check if it's a mapping key (contains ":" not inside quotes)
        if (IsMappingKey(line))
        {
            return YamlNodeKind.Mapping;
        }

        return YamlNodeKind.Scalar;
    }

    /// <summary>
    /// Tries to read a mapping entry. Returns the key and whether there's an inline value.
    /// </summary>
    /// <param name="expectedIndent">The expected indentation level for the mapping entry.</param>
    /// <param name="key">The mapping key.</param>
    /// <param name="inlineValue">The inline value, if any.</param>
    /// <returns>True if an entry was read.</returns>
    public bool TryReadMappingEntry(int expectedIndent, out string key, out string? inlineValue)
    {
        key = string.Empty;
        inlineValue = null;

        if (IsEof)
        {
            return false;
        }

        string line = _lines[_lineIndex];
        int indent = GetIndent(line);

        if (indent != expectedIndent)
        {
            return false;
        }

        string trimmed = line.TrimStart();
        if (!IsMappingKey(trimmed))
        {
            return false;
        }

        int colonIndex = FindKeyColonIndex(trimmed);
        if (colonIndex < 0)
        {
            return false;
        }

        string rawKey = trimmed.Substring(0, colonIndex);
        key = UnquoteKey(rawKey);
        string remainder = trimmed.Substring(colonIndex + 1).Trim();

        _lineIndex++;

        if (remainder.Length > 0)
        {
            inlineValue = remainder;
        }

        return true;
    }

    /// <summary>
    /// Tries to read a sequence entry prefix ("- " or bare "-").
    /// </summary>
    /// <param name="expectedIndent">The expected indentation for the sequence item.</param>
    /// <param name="inlineValue">The inline value after the "- " prefix, if any.</param>
    /// <returns>True if a sequence entry was read.</returns>
    public bool TryReadSequenceEntry(int expectedIndent, out string? inlineValue)
    {
        inlineValue = null;

        if (IsEof)
        {
            return false;
        }

        string line = _lines[_lineIndex];
        int indent = GetIndent(line);

        if (indent != expectedIndent)
        {
            return false;
        }

        string trimmed = line.TrimStart();

        if (trimmed == "-")
        {
            _lineIndex++;

            return true;
        }

        if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
        {
            return false;
        }

        _lineIndex++;
        string remainder = trimmed.Substring(2).Trim();

        if (remainder.Length > 0)
        {
            inlineValue = remainder;
        }

        return true;
    }

    /// <summary>
    /// Tries to read an empty mapping ("{}").
    /// </summary>
    /// <returns>True if an empty mapping was consumed.</returns>
    public bool TryReadEmptyMapping()
    {
        if (!IsEof)
        {
            string line = _lines[_lineIndex].TrimStart();
            if (line == "{}")
            {
                _lineIndex++;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tries to read an empty collection ("[]" or "{}").
    /// </summary>
    /// <returns>True if an empty collection was consumed.</returns>
    public bool TryReadEmptyCollection()
    {
        if (!IsEof)
        {
            string line = _lines[_lineIndex].TrimStart();
            if (line is "[]" or "{}")
            {
                _lineIndex++;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Skips all lines at or deeper than the specified indent level.
    /// </summary>
    public void SkipNode(int baseIndent)
    {
        // Skip the first line (if any)
        if (!IsEof)
        {
            _lineIndex++;
        }

        // Skip any lines that are indented deeper
        while (!IsEof)
        {
            int indent = GetIndent(_lines[_lineIndex]);
            if (indent <= baseIndent)
            {
                break;
            }

            _lineIndex++;
        }
    }

    private static string? ParseScalarValue(string value)
    {
        if (value is "null" or "~" or "")
        {
            return null;
        }

        // Handle single-quoted strings
        if (value.Length >= 2 && value[0] == '\'')
        {
            string inner = value.Substring(1, value.Length - 2);
            return inner.Replace("''", "'");
        }

        // Handle double-quoted strings
        if (value.Length >= 2 && value[0] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    private static bool IsMappingKey(string line)
    {
        // Look for a colon followed by a space or end of line, not inside quotes
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
            }
            else if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (c == ':' && !inSingleQuote && !inDoubleQuote)
            {
                // Colon at end of line or followed by space
                if (i + 1 >= line.Length || line[i + 1] == ' ')
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int GetIndent(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ')
            {
                count++;
            }
            else
            {
                break;
            }
        }

        return count / 2; // 2-space indentation
    }

    private static int FindKeyColonIndex(string line)
    {
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\'' && !inDoubleQuote)
            {
                if (inSingleQuote && i + 1 < line.Length && line[i + 1] == '\'')
                {
                    i++; // Skip escaped single quote
                }
                else
                {
                    inSingleQuote = !inSingleQuote;
                }
            }
            else if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (c == ':' && !inSingleQuote && !inDoubleQuote)
            {
                if (i + 1 >= line.Length || line[i + 1] == ' ')
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string UnquoteKey(string key)
    {
        if (key.Length >= 2 && key[0] == '\'')
        {
            string inner = key.Substring(1, key.Length - 2);

            return inner.Replace("''", "'");
        }

        if (key.Length >= 2 && key[0] == '"')
        {
            return key.Substring(1, key.Length - 2);
        }

        return key;
    }
}

/// <summary>
/// Describes the kind of YAML node at the current reader position.
/// </summary>
public enum YamlNodeKind
{
    /// <summary>A scalar value.</summary>
    Scalar,

    /// <summary>A sequence (array/list).</summary>
    Sequence,

    /// <summary>A mapping (object/dictionary).</summary>
    Mapping,
}
