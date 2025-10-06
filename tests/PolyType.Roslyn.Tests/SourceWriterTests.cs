using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace PolyType.Roslyn.Tests;

public static class SourceWriterTests
{
    [Theory]
    [InlineData('\t', 1)]
    [InlineData(' ', 2)]
    [InlineData('\n', 42)]
    public static void Constructor_CustomizedIndentation(char indentationChar, int charsPerIndentation)
    {
        using SourceWriter writer = new(indentationChar, charsPerIndentation);
        Assert.Equal(indentationChar, writer.IndentationChar);
        Assert.Equal(charsPerIndentation, writer.CharsPerIndentation);

        writer.Indentation = 2;
        writer.WriteLine("hi");
        writer.Indentation = 0;

        SourceText text = writer.ToSourceText();
        Assert.Equal(charsPerIndentation * 2 + 2 + Environment.NewLine.Length, text.Length);
    }
    
    [Theory]
    [InlineData('a', 1)]
    [InlineData('?', 1)]
    [InlineData('.', 1)]
    [InlineData(' ', 0)]
    [InlineData(' ', -1)]
    public static void Constructor_InvalidArguments(char indentationChar, int charsPerIndentation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceWriter(indentationChar, charsPerIndentation));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public static void Indentation_ValidArgument(int indentation)
    {
        using SourceWriter writer = new();
        writer.Indentation = indentation;
        Assert.Equal(indentation, writer.Indentation);
    }
    
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public static void Indentation_InvalidArgument(int indentation)
    {
        using SourceWriter writer = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.Indentation = indentation);
        Assert.Equal(0, writer.Indentation);
    }
    
    [Fact]
    public static void WriteLine_MultiLineString_PreservesIndentation()
    {
        using SourceWriter writer = new();
        writer.Indentation = 2;
        writer.WriteLine("""
            line1
            line2
            line3
            """);
        
        string result = writer.ToString();
        string expected = $"        line1{Environment.NewLine}        line2{Environment.NewLine}        line3{Environment.NewLine}";
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public static void WriteLine_MultiLineInterpolatedString_PreservesIndentation()
    {
        using SourceWriter writer = new();
        writer.Indentation = 1;
        string value1 = "hello";
        int value2 = 42;
        writer.WriteLine($"""
            First: {value1}
            Second: {value2}
            End
            """);
        
        string result = writer.ToString();
        string expected = $"    First: hello{Environment.NewLine}    Second: 42{Environment.NewLine}    End{Environment.NewLine}";
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public static void WriteLine_InterpolatedString_WithIFormattableSegments()
    {
        using SourceWriter writer = new();
        writer.Indentation = 1;
        
        int intValue = 42;
        double doubleValue = 3.14159;
        DateTime dateValue = new DateTime(2024, 1, 15, 14, 30, 0);
        decimal decimalValue = 1234.5678m;
        
        writer.WriteLine($"Integer: {intValue:X8}");
        writer.WriteLine($"Double: {doubleValue:F2}");
        writer.WriteLine($"Date: {dateValue:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Decimal: {decimalValue:C}");
        
        string result = writer.ToString();
        string expected = 
            $"    Integer: 0000002A{Environment.NewLine}" +
            $"    Double: 3.14{Environment.NewLine}" +
            $"    Date: 2024-01-15 14:30:00{Environment.NewLine}" +
            $"    Decimal: {decimalValue:C}{Environment.NewLine}";
        
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public static void Dispose_CanBeCalledMultipleTimes()
    {
        SourceWriter writer = new();
        writer.WriteLine("test");
        
        writer.Dispose();
        writer.Dispose(); // Should not throw
        writer.Dispose(); // Should not throw
    }
    
    [Fact]
    public static void Dispose_MethodsThrowObjectDisposedException()
    {
        SourceWriter writer = new();
        writer.Dispose();
        
        // Verify all write methods throw after disposal
        Assert.Throws<ObjectDisposedException>(() => writer.Indentation = 42);
        Assert.Throws<ObjectDisposedException>(() => writer.WriteLine('c'));
        Assert.Throws<ObjectDisposedException>(() => writer.WriteLine("test"));
        Assert.Throws<ObjectDisposedException>(() => writer.WriteLine());
        Assert.Throws<ObjectDisposedException>(() => writer.ToString());
        Assert.Throws<ObjectDisposedException>(() => writer.ToSourceText());
    }
    
    [Fact]
    public static void Dispose_PropertiesRemainAccessible()
    {
        SourceWriter writer = new('\t', 2);
        writer.Indentation = 5;
        writer.WriteLine("test");
        writer.Dispose();
        
        // Properties should remain accessible after dispose
        Assert.Equal('\t', writer.IndentationChar);
        Assert.Equal(2, writer.CharsPerIndentation);
        Assert.Equal(0, writer.Length);
        Assert.Equal(5, writer.Indentation);
    }
}