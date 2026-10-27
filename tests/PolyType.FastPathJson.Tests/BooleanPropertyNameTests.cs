using System.Buffers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PolyType.FastPathJson.Tests;

public static class BooleanPropertyNameTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("tRuE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    [InlineData("fAlSe", false)]
    [InlineData(@"\u0074rue", true)]
    [InlineData(@"\u0074\u0072\u0075\u0065", true)]
    [InlineData(@"\u0054\u0052\u0055\u0045", true)]
    [InlineData(@"\u0066alse", false)]
    [InlineData(@"\u0066\u0061\u006c\u0073\u0065", false)]
    [InlineData(@"\u0046\u0041\u004c\u0053\u0045", false)]
    public static void BooleanKeysPreserveEscapingCaseAndSegmentedInput(string name, bool key)
    {
        string json = "{\"" + name + "\":42}";
        string expected = key ? """{"True":42}""" : """{"False":42}""";
        Dictionary<bool, int>?[] values =
        [
            FastPathJsonSerializer.Deserialize<Dictionary<bool, int>, ScalarKeyWitness>(json.AsSpan()),
            FastPathJsonSerializer.Deserialize<Dictionary<bool, int>, ScalarKeyWitness>(Encoding.UTF8.GetBytes(json)),
            ReadSegmented<ScalarKeyWitness>(json),
        ];

        foreach (Dictionary<bool, int>? value in values)
        {
            Assert.NotNull(value);
            Assert.Single(value);
            Assert.Equal(42, value[key]);
            Assert.Equal(expected, FastPathJsonSerializer.Serialize<Dictionary<bool, int>, ScalarKeyWitness>(value));
        }
    }

    public static IEnumerable<object[]> InvalidNames()
    {
        foreach (string name in new[]
        {
            "", "tru", "fals", "truex", "falsex", " true", "false ",
            @"\u0074\u0072\u0075\u0065x",
            @"\u0066\u0061\u006c\u0073\u0065x",
            @"\u0066\u0061\u006c\u0073\u0065\u0078",
            new string('x', 30), new string('x', 31), new string('x', 257), new string('x', 4096),
        })
        {
            yield return [name];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public static void InvalidBooleanKeysPreserveFormatErrors(string name)
    {
        string json = "{\"" + name + "\":42}";
        Assert.Throws<FormatException>(() =>
            FastPathJsonSerializer.Deserialize<Dictionary<bool, int>, ScalarKeyWitness>(json.AsSpan()));
        Assert.Throws<FormatException>(() =>
            FastPathJsonSerializer.Deserialize<Dictionary<bool, int>, ScalarKeyWitness>(Encoding.UTF8.GetBytes(json)));
        Assert.Throws<FormatException>(() => ReadSegmented<ScalarKeyWitness>(json));
    }

    private static Dictionary<bool, int>? ReadSegmented<TWitness>(string json)
        where TWitness : IJsonSerializable<Dictionary<bool, int>>
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Segment first = new(bytes.AsMemory(0, 1));
        Segment last = first;
        for (int i = 1; i < bytes.Length; i++)
        {
            last = last.Append(bytes.AsMemory(i, 1));
        }

        Utf8JsonReader reader = new(new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length));
        Assert.True(reader.Read());
        Dictionary<bool, int>? value = TWitness.Read(ref reader);
        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
        Assert.False(reader.Read());

        return value;
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            Segment next = new(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;

            return next;
        }
    }
}
