using PolyType.Examples.CborSerializer;
using System.Formats.Cbor;

namespace PolyType.Tests;
public abstract partial class CborCustomConverterTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void GraphWithCustomConverter()
    {
        CborConverter<RootWithCustomConvertedMember> converter = GetConverterUnderTest<RootWithCustomConvertedMember>();
        RootWithCustomConvertedMember root = new(new(42));
        byte[] cbor = converter.Encode(root);
        RootWithCustomConvertedMember? deserialized = converter.Decode(cbor);
        Assert.NotNull(deserialized);

        // We expect the round-trip to add 3 to the Age, as evidence that the custom converter was used.
        Assert.Equal(root.CustomMember.Age + 3, deserialized.CustomMember.Age);
    }

    public record RootWithCustomConvertedMember(ClassWithCustomConverter<int> CustomMember);

    [CborConverter(typeof(CustomConverter<>))]
    public record ClassWithCustomConverter<T>(int Age);

    [GenerateShape<RootWithCustomConvertedMember>]
    protected partial class Witness;

    public class CustomConverter<T> : CborConverter<ClassWithCustomConverter<T>>
    {
        public override ClassWithCustomConverter<T>? Read(CborReader reader)
        {
            if (reader.PeekState() is CborReaderState.Null)
            {
                reader.ReadNull();
                return default;
            }

            int age = reader.ReadInt32();
            return new ClassWithCustomConverter<T>(age + 2);
        }

        public override void Write(CborWriter writer, ClassWithCustomConverter<T>? value)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteInt32(value.Age + 1);
        }
    }

    private CborConverter<T> GetConverterUnderTest<T>() =>
        CborSerializer.CreateConverter((ITypeShape<T>?)providerUnderTest.Provider.GetShape(typeof(T)) ?? throw new InvalidOperationException("Shape missing."));

    public sealed class Reflection() : CborCustomConverterTests(RefectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : CborCustomConverterTests(RefectionProviderUnderTest.Emit);
    public sealed class SourceGen() : CborCustomConverterTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
