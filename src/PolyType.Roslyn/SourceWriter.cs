using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PolyType.Roslyn;

/// <summary>
/// A utility class for generating indented source code.
/// </summary>
public sealed class SourceWriter : IDisposable
{
    private readonly string _newLine;
    private char[] _buffer;
    private int _length;
    private int _indentation;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of <see cref="SourceWriter"/>.
    /// </summary>
    /// <param name="capacity">The initial capacity of the buffer.</param>
    public SourceWriter(int capacity = 1024)
    {
        IndentationChar = ' ';
        CharsPerIndentation = 4;
        _buffer = ArrayPool<char>.Shared.Rent(capacity);
        _length = 0;
        // StringBuilder.AppendLine() uses Environment.NewLine, so we cache it here
        // before entering any analyzer context where Environment is restricted
        _newLine = GetNewLine();
    }

    private static string GetNewLine()
    {
#pragma warning disable RS1035 // Environment access is safe during construction, before entering analyzer context
        return Environment.NewLine;
#pragma warning restore RS1035
    }

    /// <summary>
    /// Creates a new instance of <see cref="SourceWriter"/> with the specified indentation settings.
    /// </summary>
    /// <param name="indentationChar">The indentation character to be used.</param>
    /// <param name="charsPerIndentation">The number of characters per indentation to be applied.</param>
    /// <param name="capacity">The initial capacity of the buffer.</param>
    public SourceWriter(char indentationChar, int charsPerIndentation, int capacity = 1024)
    {
        if (!char.IsWhiteSpace(indentationChar))
        {
            throw new ArgumentOutOfRangeException(nameof(indentationChar));
        }

        if (charsPerIndentation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(charsPerIndentation));
        }

        IndentationChar = indentationChar;
        CharsPerIndentation = charsPerIndentation;
        _buffer = ArrayPool<char>.Shared.Rent(capacity);
        _length = 0;
        _newLine = GetNewLine();
    }

    /// <summary>
    /// Gets the character used for indentation.
    /// </summary>
    public char IndentationChar { get; }

    /// <summary>
    /// Gets the number of characters per indentation.
    /// </summary>
    public int CharsPerIndentation { get; }

    /// <summary>
    /// Gets the length of the generated source text.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets or sets the current indentation level.
    /// </summary>
    public int Indentation 
    {
        get => _indentation;
        set
        {
            if (value < 0)
            {
                Throw();
                static void Throw() => throw new ArgumentOutOfRangeException(nameof(value));
            }

            _indentation = value;
        }
    }

    /// <summary>
    /// Appends a new line with the current character.
    /// </summary>
    public void WriteLine(char value)
    {
        AddIndentation();
        Append(value);
        AppendLine();
    }

    /// <summary>
    /// Appends a new line with the specified text.
    /// </summary>
    /// <param name="text">The text to append.</param>
    /// <param name="trimNullAssignmentLines">Trims any lines containing 'Identifier = null,' assignments.</param>
    /// <param name="disableIndentation">Append text without preserving the current indentation.</param>
    public void WriteLine(
        [StringSyntax("c#-test")] string text,
        bool trimNullAssignmentLines = false,
        bool disableIndentation = false)
    {
        if (trimNullAssignmentLines)
        {
            // Since the ns2.0 Regex class doesn't support spans,
            // use Regex.Replace to preprocess the string instead
            // of doing a line-by-line replacement.
            text = s_nullAssignmentLineRegex.Replace(text, "");
        }

        if (_indentation == 0 || disableIndentation)
        {
            Append(text);
            AppendLine();
            return;
        }

        bool isFinalLine;
        ReadOnlySpan<char> remainingText = text.AsSpan();
        do
        {
            ReadOnlySpan<char> nextLine = GetNextLine(ref remainingText, out isFinalLine);

            AddIndentation();
            Append(nextLine);
            AppendLine();
        }
        while (!isFinalLine);
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Appends a new line with the specified interpolated string.
    /// </summary>
    /// <param name="handler">The interpolated string handler.</param>
    public void WriteLine([InterpolatedStringHandlerArgument("")] ref WriteLineInterpolatedStringHandler handler)
    {
        WriteLine(handler.ToStringAndClear());
    }

    /// <summary>
    /// Appends a new line with the specified interpolated string.
    /// </summary>
    /// <param name="handler">The interpolated string handler.</param>
    /// <param name="trimNullAssignmentLines">Trims any lines containing 'Identifier = null,' assignments.</param>
    /// <param name="disableIndentation">Append text without preserving the current indentation.</param>
    public void WriteLine(
        [InterpolatedStringHandlerArgument("")] ref WriteLineInterpolatedStringHandler handler,
        bool trimNullAssignmentLines = false,
        bool disableIndentation = false)
    {
        WriteLine(handler.ToStringAndClear(), trimNullAssignmentLines, disableIndentation);
    }
#endif

    // Horizontal whitespace regex: apply double negation on \s to exclude \r and \n
    private const string HWSR = @"[^\S\r\n]*";
    private static readonly Regex s_nullAssignmentLineRegex =
        new(@$"{HWSR}\w+{HWSR}={HWSR}null{HWSR},?{HWSR}\r?\n", RegexOptions.Compiled);

    /// <summary>
    /// Appends a new line to the source text.
    /// </summary>
    public void WriteLine() => AppendLine();

    /// <summary>
    /// Encodes the currently written source to a <see cref="SourceText"/> instance.
    /// </summary>
    public SourceText ToSourceText()
    {
        Debug.Assert(_indentation == 0 && _length > 0);
        return SourceText.From(new string(_buffer, 0, _length), Encoding.UTF8);
    }

    /// <summary>
    /// Renders the written text as a string.
    /// </summary>
    public override string ToString() => new string(_buffer, 0, _length);

    private void AddIndentation()
    {
        int count = CharsPerIndentation * _indentation;
        EnsureCapacity(_length + count);
        _buffer.AsSpan(_length, count).Fill(IndentationChar);
        _length += count;
    }

    private static ReadOnlySpan<char> GetNextLine(ref ReadOnlySpan<char> remainingText, out bool isFinalLine)
    {
        if (remainingText.IsEmpty)
        {
            isFinalLine = true;
            return default;
        }

        ReadOnlySpan<char> next;
        ReadOnlySpan<char> rest;

        int lineLength = remainingText.IndexOf('\n');
        if (lineLength == -1)
        {
            lineLength = remainingText.Length;
            isFinalLine = true;
            rest = default;
        }
        else
        {
            rest = remainingText[(lineLength + 1)..];
            isFinalLine = false;
        }

        if ((uint)lineLength > 0 && remainingText[lineLength - 1] == '\r')
        {
            lineLength--;
        }

        next = remainingText[..lineLength];
        remainingText = rest;
        return next;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Append(char value)
    {
        EnsureCapacity(_length + 1);
        _buffer[_length++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Append(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return;
        }

        EnsureCapacity(_length + span.Length);
        span.CopyTo(_buffer.AsSpan(_length));
        _length += span.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        EnsureCapacity(_length + text.Length);
        text.AsSpan().CopyTo(_buffer.AsSpan(_length));
        _length += text.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendLine()
    {
        EnsureCapacity(_length + _newLine.Length);
        _newLine.AsSpan().CopyTo(_buffer.AsSpan(_length));
        _length += _newLine.Length;
    }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= _buffer.Length)
        {
            return;
        }

        int newCapacity = Math.Max(requiredCapacity, _buffer.Length * 2);
        char[] newBuffer = ArrayPool<char>.Shared.Rent(newCapacity);
        _buffer.AsSpan(0, _length).CopyTo(newBuffer);
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    /// <summary>
    /// Disposes the SourceWriter and returns buffers to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = null!;
        _length = 0;
        _disposed = true;
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Provides an interpolated string handler for <see cref="SourceWriter.WriteLine"/> methods.
    /// </summary>
    [InterpolatedStringHandler]
    public struct WriteLineInterpolatedStringHandler
    {
        private DefaultInterpolatedStringHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="WriteLineInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="writer">The associated <see cref="SourceWriter"/> instance.</param>
        public WriteLineInterpolatedStringHandler(int literalLength, int formattedCount, SourceWriter writer)
        {
            _handler = new DefaultInterpolatedStringHandler(literalLength, formattedCount);
        }

        /// <summary>
        /// Appends a string value to the handler.
        /// </summary>
        public void AppendLiteral(string value) => _handler.AppendLiteral(value);

        /// <summary>
        /// Appends a formatted value to the handler.
        /// </summary>
        public void AppendFormatted<T>(T value) => _handler.AppendFormatted(value);

        /// <summary>
        /// Appends a formatted value to the handler with a format string.
        /// </summary>
        public void AppendFormatted<T>(T value, string? format) => _handler.AppendFormatted(value, format);

        /// <summary>
        /// Appends a formatted span value to the handler.
        /// </summary>
        public void AppendFormatted(ReadOnlySpan<char> value) => _handler.AppendFormatted(value);

        /// <summary>
        /// Converts the handler to a string and clears it.
        /// </summary>
        internal string ToStringAndClear() => _handler.ToStringAndClear();
    }
#endif
}
