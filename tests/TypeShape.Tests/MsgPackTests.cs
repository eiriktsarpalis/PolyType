using System.Numerics;
using MessagePack;
using Nerdbank.Streams;
using TypeShape.Examples.MsgPackSerializer;
using Xunit;
using Xunit.Abstractions;

namespace TypeShape.Tests;

public partial class MsgPackSerializerTests(ITestOutputHelper logger)
{
    [Fact]
    public void ClassWithPropertySetters() => AssertRoundtrip(new DerivedClass { X = 42, Y = 84 });

    [Fact]
    public void StructWithFields() => AssertRoundtrip(new ComplexStruct { im = 1.2, real = 3.5 });

    ////[Fact]
    ////public void JustAnEnum() => AssertRoundtrip(MyEnum.G);

    protected void AssertRoundtrip<T>(T? value)
        where T : IShapeable<T>
    {
        Assert.Equal(value, Roundtrip(value));
    }

    protected T? Roundtrip<T>(T? value)
        where T : IShapeable<T>
    {
        Sequence<byte> writer = new();
        MsgPackSerializer.Serialize(writer, value, MessagePackSerializerOptions.Standard);
        logger.WriteLine(MessagePackSerializer.ConvertToJson(writer));
        return MsgPackSerializer.Deserialize<T>(writer, MessagePackSerializerOptions.Standard);
    }
}
