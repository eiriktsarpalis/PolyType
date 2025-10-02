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
    private static readonly string s_newLine = "\r\n";
    private char[] _buffer;
    private int _length;
    private int _indentation;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of <see cref="SourceWriter"/>.
    /// </summary>
    public SourceWriter()
    {
        IndentationChar = ' ';
        CharsPerIndentation = 4;
        _buffer = ArrayPool<char>.Shared.Rent(1024);
        _length = 0;
    }

    /// <summary>
    /// Creates a new instance of <see cref="SourceWriter"/> with the specified indentation settings.
    /// </summary>
    /// <param name="indentationChar">The indentation character to be used.</param>
    /// <param name="charsPerIndentation">The number of characters per indentation to be applied.</param>
    public SourceWriter(char indentationChar, int charsPerIndentation)
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
        _buffer = ArrayPool<char>.Shared.Rent(1024);
        _length = 0;
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
        EnsureCapacity(_length + s_newLine.Length);
        s_newLine.AsSpan().CopyTo(_buffer.AsSpan(_length));
        _length += s_newLine.Length;
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
}
