using PolyType.FastPathJson;
using PolyType.Tests;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace PolyType.FastPathJson.Tests;

[GenerateJsonFor<bool>]
[GenerateJsonFor<sbyte>]
[GenerateJsonFor<short>]
[GenerateJsonFor<int>]
[GenerateJsonFor<long>]
[GenerateJsonFor<byte>]
[GenerateJsonFor<ushort>]
[GenerateJsonFor<uint>]
[GenerateJsonFor<ulong>]
[GenerateJsonFor<float>]
[GenerateJsonFor<double>]
[GenerateJsonFor<decimal>]
[GenerateJsonFor<char>]
[GenerateJsonFor<string>]
[GenerateJsonFor<byte[]>]
[GenerateJsonFor<Guid>]
[GenerateJsonFor<DateTime>]
[GenerateJsonFor<DateTimeOffset>]
[GenerateJsonFor<TimeSpan>]
[GenerateJsonFor<DateOnly>]
[GenerateJsonFor<TimeOnly>]
[GenerateJsonFor<Int128>]
[GenerateJsonFor<UInt128>]
[GenerateJsonFor<Half>]
[GenerateJsonFor<Uri>]
[GenerateJsonFor<Version>]
[GenerateJsonFor<BigInteger>]
[GenerateJsonFor<Rune>]
[GenerateJsonFor<BindingFlags>]
[GenerateJsonFor<int?>]
[GenerateJsonFor<BindingFlags?>]
[GenerateJsonFor<int[]>]
[GenerateJsonFor<int[][]>]
[GenerateJsonFor<List<string>>]
[GenerateJsonFor<List<byte>>]
[GenerateJsonFor<Dictionary<string, int>>]
[GenerateJsonFor<Queue<int>>]
[GenerateJsonFor<HashSet<string>>]
[GenerateJsonFor<SortedDictionary<string, int>>]
[GenerateJsonFor<SortedList<int, string>>]
[GenerateJsonFor<DerivedList>]
[GenerateJsonFor<DerivedDictionary>]
[GenerateJsonFor<CollectionWithEnumerableCtor>]
[GenerateJsonFor<CollectionWithSpanCtor>]
[GenerateJsonFor<DictionaryWithEnumerableCtor>]
[GenerateJsonFor<SimplePoco>]
[GenerateJsonFor<PocoWithListAndDictionaryProps>]
[GenerateJsonFor<MyLinkedList<int>>]
[GenerateJsonFor<ComplexStruct>]
[GenerateJsonFor<ComplexStructWithProperties>]
[GenerateJsonFor<StructWithDefaultCtor>]
[GenerateJsonFor<SimpleRecord>]
[GenerateJsonFor<GenericRecord<int>>]
[GenerateJsonFor<GenericRecord<string>>]
[GenerateJsonFor<GenericRecordStruct<int>>]
[GenerateJsonFor<ClassWithDefaultConstructorAndSingleRequiredProperty>]
[GenerateJsonFor<NonNullStringRecord>]
[GenerateJsonFor<NullableStringRecord>]
[GenerateJsonFor<PropertyRequiredByAttribute>]
[GenerateJsonFor<CtorParameterRequiredByAttribute>]
[GenerateJsonFor<CtorParameterNotRequiredByAttribute>]
[GenerateJsonFor<ClassWithConstructorAndAnnotations>]
[GenerateJsonFor<(int, string)>]
[GenerateJsonFor<(int, int, int, int, int, int, int, int, int)>]
[GenerateJsonFor<Tuple<int, string>>]
[GenerateJsonFor<int[,]>]
[GenerateJsonFor<int[,,]>]
[GenerateJsonFor(typeof(Tree))]
public sealed partial class TestCasesWitness;

[GenerateJson]
public partial record Receipt(int Number, string Customer)
{
    [PropertyShape(Name = "lineItems", Order = -1)]
    [JsonPropertyName("lineItems"), JsonPropertyOrder(-1)]
    public List<ReceiptLine>? Lines { get; set; }

    [PropertyShape(Ignore = true)]
    [JsonIgnore]
    public Action? Notification { get; set; }
}

public record ReceiptLine(string Product, decimal Price);

[GenerateJsonFor<List<List<List<int>>>>]
[GenerateJsonFor<Dictionary<string, (Receipt, double[])>>]
public sealed partial class FoldingWitness;
