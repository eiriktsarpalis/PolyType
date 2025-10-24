using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions; // retained for potential future use
using System.IO;

namespace PolyType.Roslyn;

/// <summary>
/// A utility class for generating indented source code.
/// </summary>
public sealed class SourceWriter : IDisposable
{
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
    private static readonly string s_newLine = Environment.NewLine;
#pragma warning restore RS1035 // Do not use APIs banned for analyzers

    private char[] _buffer;
    private int _length;
    private int _indentation;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of <see cref="SourceWriter"/>.
    /// </summary>
    /// <param name="capacity">The initial capacity of the buffer.</param>
    public SourceWriter(int capacity = 1024) : this(' ', 4, capacity)
    {
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

            EnsureNotDisposed();
            _indentation = value;
        }
    }

    /// <summary>
    /// Appends a new line with the current character.
    /// </summary>
    public void WriteLine(char value)
    {
        EnsureNotDisposed();
        AddIndentation();
        EnsureCapacity(checked(_length + 1));
        _buffer[_length++] = value;
        AppendLine();
    }

    /// <summary>
    /// Appends a new line with the specified text.
    /// </summary>
    /// <param name="text">The text to append.</param>
    /// <param name="trimDefaultAssignmentLines">Trims any lines containing 'Identifier = null', 'Identifier = false', 'Identifier = 0', or 'Identifier = default' assignments.</param>
    /// <param name="disableIndentation">Append text without preserving the current indentation.</param>
    public void WriteLine(
        [StringSyntax("c#-test")] string text,
        bool trimDefaultAssignmentLines = false,
        bool disableIndentation = false)
    {
        EnsureNotDisposed();

        if (trimDefaultAssignmentLines)
        {
            text = TrimDefaultAssignmentLines(text);
        }

        AddIndentation();
        AppendSegment(text.AsSpan(), disableIndentation);
        WriteLine();
    }

    /// <summary>
    /// Appends a new line with the specified interpolated string.
    /// </summary>
    /// <param name="handler">The interpolated string handler.</param>
    public void WriteLine([InterpolatedStringHandlerArgument("")] ref WriteLineInterpolatedStringHandler handler)
    {
        AppendLine(); // All work is done in the handler methods, just need to append a new line.
    }

    /// <summary>
    /// Appends a new line to the source text.
    /// </summary>
    public void WriteLine()
    {
        EnsureNotDisposed();
        AppendLine();
    }

    /// <summary>
    /// Encodes the currently written source to a <see cref="SourceText"/> instance.
    /// </summary>
    public SourceText ToSourceText()
    {
        EnsureNotDisposed();
        return SourceText.From(new string(_buffer, 0, _length), Encoding.UTF8);
    }

    /// <summary>
    /// Renders the written text as a string.
    /// </summary>
    public override string ToString()
    {
        EnsureNotDisposed();
        return new string(_buffer, 0, _length);
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

        _buffer.AsSpan(0, _length).Clear();
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = null!;
        _length = 0;
        _disposed = true;
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

    private void AppendSegment(ReadOnlySpan<char> segment, bool disableIndentation = false)
    {
        if (disableIndentation)
        {
            AppendChars(segment);
            return;
        }

        while (true)
        {
            ReadOnlySpan<char> nextLine = GetNextLine(ref segment, out bool isFinalLine);
            AppendChars(nextLine);

            if (isFinalLine)
            {
                break;
            }

            AppendLine();
            AddIndentation();
        }
    }

    // Horizontal whitespace regex: apply double negation on \s to exclude \r and \n
    private const string HWSR = @"[^\S\r\n]*";
    // Instead of a broad regex that risks removing required member initializers, we implement
    // a lightweight line filter that skips only safe default assignments for non-required members.
    // Required members are listed explicitly below; if new required members are added in the model
    // layer they should also be added here to avoid accidental trimming.
    private static readonly HashSet<string> s_requiredMemberNames = new(StringComparer.Ordinal)
    {
        "IsRecordType","IsTupleType","Position","Rank","Tag","Index",
        "IsGetterPublic","IsSetterPublic","IsGetterNonNullable","IsSetterNonNullable",
        "IsRequired","IsNonNullable","IsPublic","IsVoidLike","IsAsync",
        "IsAsyncEnumerable","IsSetType","IsStatic","IsTagSpecified","Name"
    };

    private static string TrimDefaultAssignmentLines(string text)
    {
        // Fast path: if none of the target tokens appear, return original.
        if (text.IndexOf("= null", StringComparison.Ordinal) < 0 &&
            text.IndexOf("= false", StringComparison.Ordinal) < 0 &&
            text.IndexOf("= 0", StringComparison.Ordinal) < 0 &&
            text.IndexOf("= default", StringComparison.Ordinal) < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmedStart = line.TrimStart();
            int eqIndex = trimmedStart.IndexOf('=');
            if (eqIndex > 0)
            {
                string identifier = trimmedStart.Substring(0, eqIndex).TrimEnd();
                if (IsIdentifier(identifier) && !s_requiredMemberNames.Contains(identifier))
                {
                    string rhs = trimmedStart.Substring(eqIndex + 1).Trim();
                    // Remove trailing comma for value comparison.
                    if (rhs.EndsWith(",", StringComparison.Ordinal)) // specify comparison to satisfy analyzer
                    {
                        rhs = rhs.Substring(0, rhs.Length - 1).TrimEnd();
                    }

                    if (rhs is "null" or "false" or "0" or "default")
                    {
                        // Skip this line (trim it)
                        continue;
                    }
                }
            }

            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private static bool IsIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || !(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }
        for (int i = 1; i < value.Length; i++)
        {
            char c = value[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendChars(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return;
        }

        int newLength = checked(_length + span.Length);
        EnsureCapacity(newLength);
        span.CopyTo(_buffer.AsSpan(start: _length));
        _length = newLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendLine()
    {
        int length = _length;
        int newLength = length + s_newLine.Length;
        EnsureCapacity(newLength);
        s_newLine.AsSpan().CopyTo(_buffer.AsSpan(start: length));
        _length = newLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddIndentation()
    {
        if (_indentation == 0)
        {
            return;
        }

        int count = CharsPerIndentation * _indentation;
        int newLength = checked(_length + count);
        EnsureCapacity(newLength);
        _buffer.AsSpan(_length, count).Fill(IndentationChar);
        _length = newLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            Throw();
            static void Throw() => throw new ObjectDisposedException(nameof(SourceWriter));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int requiredCapacity)
    {
        Debug.Assert(requiredCapacity >= 0);
        if (requiredCapacity > _buffer.Length)
        {
            Grow(requiredCapacity);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int requiredCapacity)
    {
        Debug.Assert(requiredCapacity > _buffer.Length);

        int newCapacity = Math.Max(requiredCapacity, checked(_buffer.Length * 2));
        char[] newBuffer = ArrayPool<char>.Shared.Rent(newCapacity);

        Span<char> oldBuffer = _buffer.AsSpan(0, _length);
        oldBuffer.CopyTo(newBuffer);
        oldBuffer.Clear();

        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    /// <summary>
    /// Provides an interpolated string handler for <see cref="SourceWriter"/> WriteLine methods.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [InterpolatedStringHandler]
    public ref struct WriteLineInterpolatedStringHandler
    {
        private readonly SourceWriter _writer;

        /// <summary>
        /// Initializes a new instance of the <see cref="WriteLineInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="writer">The associated <see cref="SourceWriter"/> instance.</param>
        public WriteLineInterpolatedStringHandler(int literalLength, int formattedCount, SourceWriter writer)
        {
            writer.EnsureNotDisposed();
            writer.AddIndentation();
            _writer = writer;
        }

        /// <summary>
        /// Appends a string value to the handler.
        /// </summary>
        public void AppendLiteral(string value) => _writer.AppendSegment(value.AsSpan());

        /// <summary>
        /// Appends a formatted value to the handler.
        /// </summary>
        public void AppendFormatted<T>(T value) => _writer.AppendSegment((value?.ToString()).AsSpan());

        /// <summary>
        /// Appends a formatted value to the handler with a format string.
        /// </summary>
        public void AppendFormatted<T>(T value, string? format)
        {
            string? result = value is IFormattable formattable ? formattable.ToString(format, null) : value?.ToString();
            _writer.AppendSegment(result.AsSpan());
        }

        /// <summary>
        /// Appends a formatted span value to the handler.
        /// </summary>
        public void AppendFormatted(ReadOnlySpan<char> value) => _writer.AppendSegment(value);
    }
}
