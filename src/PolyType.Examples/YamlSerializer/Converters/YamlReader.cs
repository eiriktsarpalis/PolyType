using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace PolyType.Examples.YamlSerializer;

/// <summary>
/// A YAML reader that wraps YamlDotNet's <see cref="Parser"/>.
/// </summary>
public sealed class YamlReader
{
    private readonly Parser _parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlReader"/> class.
    /// </summary>
    /// <param name="yaml">The YAML string to parse.</param>
    public YamlReader(string yaml)
    {
        _parser = new Parser(new StringReader(yaml));
        _parser.MoveNext(); // StreamStart
        _parser.MoveNext(); // DocumentStart
        _parser.MoveNext(); // Position at the first content event
    }

    /// <summary>
    /// Gets a value indicating whether the current position is a sequence end.
    /// </summary>
    public bool IsSequenceEnd => _parser.Current is SequenceEnd;

    /// <summary>
    /// Gets a value indicating whether the current position is a mapping start.
    /// </summary>
    public bool IsMappingStart => _parser.Current is MappingStart;

    /// <summary>
    /// Reads a scalar value at the current position.
    /// </summary>
    /// <returns>The scalar value as a string, or null if the value is null.</returns>
    public string? ReadScalar()
    {
        if (_parser.Current is not Scalar scalar)
        {
            throw new InvalidOperationException($"Expected a scalar but got {_parser.Current?.GetType().Name ?? "null"}.");
        }

        _parser.MoveNext();

        if (scalar.Value is "null" or "~" or "" && scalar.Style is ScalarStyle.Plain)
        {
            return null;
        }

        return scalar.Value;
    }

    /// <summary>
    /// Reads and consumes a null value, returning true if successful.
    /// </summary>
    public bool TryReadNull()
    {
        if (_parser.Current is Scalar { Style: ScalarStyle.Plain, Value: "null" or "~" or "" })
        {
            _parser.MoveNext();

            return true;
        }

        return false;
    }

    /// <summary>
    /// Consumes a <see cref="MappingStart"/> event.
    /// </summary>
    public void ReadMappingStart()
    {
        if (_parser.Current is not MappingStart)
        {
            throw new InvalidOperationException($"Expected MappingStart but got {_parser.Current?.GetType().Name ?? "null"}.");
        }

        _parser.MoveNext();
    }

    /// <summary>
    /// Consumes a <see cref="MappingEnd"/> event.
    /// </summary>
    public void ReadMappingEnd()
    {
        if (_parser.Current is not MappingEnd)
        {
            throw new InvalidOperationException($"Expected MappingEnd but got {_parser.Current?.GetType().Name ?? "null"}.");
        }

        _parser.MoveNext();
    }

    /// <summary>
    /// Tries to read a mapping key. Returns false when the mapping has ended.
    /// </summary>
    /// <param name="key">The mapping key that was read.</param>
    /// <returns>True if a key was read; false if the current event is <see cref="MappingEnd"/>.</returns>
    public bool TryReadMappingKey(out string key)
    {
        key = string.Empty;

        if (_parser.Current is MappingEnd)
        {
            return false;
        }

        if (_parser.Current is not Scalar keyScalar)
        {
            return false;
        }

        key = keyScalar.Value;
        _parser.MoveNext();

        return true;
    }

    /// <summary>
    /// Consumes a <see cref="SequenceStart"/> event.
    /// </summary>
    public void ReadSequenceStart()
    {
        if (_parser.Current is not SequenceStart)
        {
            throw new InvalidOperationException($"Expected SequenceStart but got {_parser.Current?.GetType().Name ?? "null"}.");
        }

        _parser.MoveNext();
    }

    /// <summary>
    /// Consumes a <see cref="SequenceEnd"/> event.
    /// </summary>
    public void ReadSequenceEnd()
    {
        if (_parser.Current is not SequenceEnd)
        {
            throw new InvalidOperationException($"Expected SequenceEnd but got {_parser.Current?.GetType().Name ?? "null"}.");
        }

        _parser.MoveNext();
    }

    /// <summary>
    /// Skips the current value node, including all children if it is a mapping or sequence.
    /// </summary>
    public void SkipValue()
    {
        if (_parser.Current is null)
        {
            return;
        }

        if (_parser.Current is Scalar)
        {
            _parser.MoveNext();

            return;
        }

        int depth = 0;
        do
        {
            if (_parser.Current is MappingStart or SequenceStart)
            {
                depth++;
            }
            else if (_parser.Current is MappingEnd or SequenceEnd)
            {
                depth--;
            }

            _parser.MoveNext();
        }
        while (depth > 0 && _parser.Current is not null);
    }
}
