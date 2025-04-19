using PolyType.Examples.CborSerializer;
using System.Formats.Cbor;

namespace PolyType.Tests;

public abstract partial class CborCustomConverterTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    [Trait("AssociatedTypes", "true")]
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

    [CborConverter(typeof(CustomConverter<>), RequiredShapes = [typeof(CustomConverterHelper<>)])]
    public record ClassWithCustomConverter<T>(int Age);

    public record CustomConverterHelper<T>(T Value);

    [GenerateShape<RootWithCustomConvertedMember>]
    protected partial class Witness;

    public class CustomConverter<T> : CborConverter<ClassWithCustomConverter<T>>
    {
        public override ClassWithCustomConverter<T>? Read(CborReader reader)
        {
            if (TypeShape is null)
            {
                throw new InvalidOperationException();
            }

            if (reader.PeekState() is CborReaderState.Null)
            {
                reader.ReadNull();
                return default;
            }

            reader.ReadStartArray();
            int age = reader.ReadInt32();

            ITypeShape helperShape = TypeShape.GetAssociatedTypeShape(typeof(CustomConverterHelper<>)) ?? throw new InvalidOperationException("Associated shape unavailable.");
            var helperConverter = CborSerializer.CreateConverter((ITypeShape<CustomConverterHelper<T>>)helperShape);
            helperConverter.Read(reader);
            
            reader.ReadEndArray();

            return new ClassWithCustomConverter<T>(age + 2);
        }

        public override void Write(CborWriter writer, ClassWithCustomConverter<T>? value)
        {
            if (TypeShape is null)
            {
                throw new InvalidOperationException();
            }

            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray(2);
            writer.WriteInt32(value.Age + 1);

            ITypeShape helperShape = TypeShape.GetAssociatedTypeShape(typeof(CustomConverterHelper<>)) ?? throw new InvalidOperationException("Associated shape unavailable.");
            var helperConverter = CborSerializer.CreateConverter((ITypeShape<CustomConverterHelper<T>>)helperShape);
            helperConverter.Write(writer, null);

            writer.WriteEndArray();
        }
    }

    private CborConverter<T> GetConverterUnderTest<T>() =>
        CborSerializer.CreateConverter((ITypeShape<T>?)providerUnderTest.Provider.GetShape(typeof(T)) ?? throw new InvalidOperationException("Shape missing."));

    public sealed class Reflection() : CborCustomConverterTests(RefectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : CborCustomConverterTests(RefectionProviderUnderTest.Emit);
    public sealed class SourceGen() : CborCustomConverterTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
