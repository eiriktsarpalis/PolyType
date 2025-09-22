using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using PolyType.Abstractions;
using PolyType.Tests.FSharp;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

#pragma warning disable IDE0040 // Accessibility modifiers required
#pragma warning disable IDE0052 // Make field readonly
#pragma warning disable IDE0044 // Make field readonly
#pragma warning disable IDE1006 // Naming rule violation
#pragma warning disable IDE0300 // Collection initialization can be simplified
#pragma warning disable IDE0021 // Use block body from constructor
#pragma warning disable IDE0250 // Struct can be made readonly
#pragma warning disable IDE0251 // Member can be made readonly
#pragma warning disable IDE0032 // Use auto property

namespace PolyType.Tests;

/// <summary>
/// Defines all the test cases for use as member data in Xunit theories.
/// </summary>
public static class TestTypes
{
    /// <summary>
    /// Gets all the test cases defined by this project.
    /// </summary>
    /// <returns>An enumerable for use by Xunit theories.</returns>
    public static IEnumerable<object[]> GetTestCases() =>
        GetTestCasesWithExpandedValues()
        .Select(value => new object[] { value });

    /// <summary>
    /// Gets all the test cases defined by this project as pairs of equal but not the same values.
    /// </summary>
    /// <returns>An enumerable for use by Xunit theories.</returns>
    public static IEnumerable<object[]> GetEqualValuePairs() =>
        GetTestCasesWithExpandedValues()
        .Zip(GetTestCasesWithExpandedValues(), (l, r) => new object[] { l, r });

    /// <summary>
    /// The core method returning all test cases and expanded values defined by this project.
    /// </summary>
    /// <returns>An enumerable including all test cases and expanded values defined by this project.</returns>
    public static IEnumerable<ITestCase> GetTestCasesWithExpandedValues() =>
        GetTestCasesCore().SelectMany(testCase => testCase.ExpandCases());

    /// <summary>
    /// The core method returning all test cases defined by this project.
    /// </summary>
    /// <returns>An enumerable including all test cases defined by this project.</returns>
    public static IEnumerable<ITestCase> GetTestCasesCore()
    {
        Witness p = new();
        yield return TestCase.Create(new object(), p);
        yield return TestCase.Create(true, additionalValues: [true], provider: p);
        yield return TestCase.Create("stringValue", additionalValues: [""], provider: p);
        yield return TestCase.Create(sbyte.MinValue, p);
        yield return TestCase.Create(short.MinValue, p);
        yield return TestCase.Create(int.MinValue, p);
        yield return TestCase.Create(long.MinValue, p);
        yield return TestCase.Create(byte.MaxValue, p);
        yield return TestCase.Create(ushort.MaxValue, p);
        yield return TestCase.Create(uint.MaxValue, p);
        yield return TestCase.Create(ulong.MaxValue, p);
        yield return TestCase.Create(BigInteger.Parse("-170141183460469231731687303715884105728", CultureInfo.InvariantCulture), p);
        yield return TestCase.Create(3.14f, p);
        yield return TestCase.Create(3.14d, p);
        yield return TestCase.Create(3.14M, p);
        yield return TestCase.Create(Guid.Empty, p);
        yield return TestCase.Create(DateTime.MaxValue, p);
        yield return TestCase.Create(DateTimeOffset.MaxValue, p);
        yield return TestCase.Create(TimeSpan.MaxValue, p);
#if NET
        yield return TestCase.Create(Rune.GetRuneAt("🤯", 0), p);
        yield return TestCase.Create(Int128.MaxValue, p);
        yield return TestCase.Create(UInt128.MaxValue, p);
        yield return TestCase.Create((Half)3.14, p);
        yield return TestCase.Create(DateOnly.MaxValue, p);
        yield return TestCase.Create(TimeOnly.MaxValue, p);
#endif
        yield return TestCase.Create(new Uri("https://github.com"), p);
        yield return TestCase.Create(new Version("1.0.0.0"), p);
        yield return TestCase.Create(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, p);

        yield return TestCase.Create((bool?)false, p);
        yield return TestCase.Create((sbyte?)sbyte.MinValue, p);
        yield return TestCase.Create((short?)short.MinValue, p);
        yield return TestCase.Create((int?)int.MinValue, p);
        yield return TestCase.Create((long?)long.MinValue, p);
        yield return TestCase.Create((byte?)byte.MaxValue, p);
        yield return TestCase.Create((ushort?)ushort.MaxValue, p);
        yield return TestCase.Create((uint?)uint.MaxValue, p);
        yield return TestCase.Create((ulong?)ulong.MaxValue, p);
        yield return TestCase.Create((BigInteger?)BigInteger.Parse("-170141183460469231731687303715884105728", CultureInfo.InvariantCulture), p);
        yield return TestCase.Create((float?)3.14f, p);
        yield return TestCase.Create((double?)3.14d, p);
        yield return TestCase.Create((decimal?)3.14M, p);
        yield return TestCase.Create((Guid?)Guid.Empty, p);
        yield return TestCase.Create((DateTime?)DateTime.MaxValue, p);
        yield return TestCase.Create((DateTimeOffset?)DateTimeOffset.MaxValue, p);
        yield return TestCase.Create((TimeSpan?)TimeSpan.MaxValue, p);
        yield return TestCase.Create((BindingFlags?)BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, p);
#if NET
        yield return TestCase.Create((Rune?)Rune.GetRuneAt("🤯", 0), p);
        yield return TestCase.Create((Int128?)Int128.MaxValue, p);
        yield return TestCase.Create((UInt128?)UInt128.MaxValue, p);
        yield return TestCase.Create((Half?)3.14, p);
        yield return TestCase.Create((DateOnly?)DateOnly.MaxValue, p);
        yield return TestCase.Create((TimeOnly?)TimeOnly.MaxValue, p);
#endif

        yield return TestCase.Create((int[])[1, 2, 3], additionalValues: [new int[0]], provider: p);
        yield return TestCase.Create((int[][])[[1, 0, 0], [0, 1, 0], [0, 0, 1]], additionalValues: [[new int[0]]], provider: p);
        yield return TestCase.Create((byte[])[1, 2, 3], p);
        yield return TestCase.Create((Memory<int>)new int[] { 1, 2, 3 }, p);
        yield return TestCase.Create((ReadOnlyMemory<int>)new[] { 1, 2, 3 }, p);
        yield return TestCase.Create((List<string>)["1", "2", "3"], p);
        yield return TestCase.Create((List<byte>)[1, 2, 3], additionalValues: [[]], provider: p);
        yield return TestCase.Create(new LinkedList<byte>([1, 2, 3]), additionalValues: [[]], provider: p);
        yield return TestCase.Create(new Queue<int>([1, 2, 3]), p);
        yield return TestCase.Create(new Stack<int>([1, 2, 3]), isStack: true, provider: p);
        yield return TestCase.Create(new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 }, provider: p);
        yield return TestCase.Create((HashSet<string>)["apple", "orange", "banana"], isSet: true, provider: p);
        yield return TestCase.Create((SortedSet<string>)["apple", "orange", "banana"], isSet: true, provider: p);
        yield return TestCase.Create(new SortedDictionary<string, int> { ["key1"] = 42, ["key2"] = -1 }, provider: p);
        yield return TestCase.Create(new SortedList<int, string> { [42] = "forty-two", [32] = "thirty-two" }, provider: p);

        yield return TestCase.Create(new Hashtable { ["key1"] = 42 }, additionalValues: [[]], provider: p);
        yield return TestCase.Create(new ArrayList { 1, 2, 3 }, additionalValues: [[]], provider: p);

        yield return TestCase.Create(new int[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } }, p);
        yield return TestCase.Create(new int[,,] { { { 1 } } }, p);

        yield return TestCase.Create(new ConcurrentQueue<int>([1, 2, 3]), p);
        yield return TestCase.Create(new ConcurrentStack<int>([1, 2, 3]), isStack: true, provider: p);
        yield return TestCase.Create(new ConcurrentDictionary<string, string> { ["key"] = "value" }, p);

        yield return TestCase.Create((IEnumerable)new List<object> { 1, 2, 1, 3 }, p);
        yield return TestCase.Create((IList)new List<object> { 1, 2, 1, 3 }, p);
        yield return TestCase.Create((ICollection)new List<object> { 1, 2, 1, 3 }, p);
        yield return TestCase.Create((IDictionary)new Dictionary<object, object> { [42] = 42 }, p);
        yield return TestCase.Create((IEnumerable<int>)[1, 2, 1, 3], p);
        yield return TestCase.Create((ICollection<int>)[1, 2, 1, 3], p);
        yield return TestCase.Create((IList<int>)[1, 2, 1, 3], p);
        yield return TestCase.Create((IReadOnlyCollection<int>)[1, 2, 1, 3], p);
        yield return TestCase.Create((IReadOnlyList<int>)[1, 2, 1, 3], p);
        yield return TestCase.Create((ISet<int>)new HashSet<int> { 1, 2, 3 }, isSet: true, provider: p);
#if NET
        yield return TestCase.Create((IReadOnlySet<int>)new HashSet<int> { 1, 2, 3 }, isSet: true, provider: p);
#endif
        yield return TestCase.Create((IDictionary<int, int>)new Dictionary<int, int> { [42] = 42 }, p);
        yield return TestCase.Create((IReadOnlyDictionary<int, int>)new Dictionary<int, int> { [42] = 42 }, p);
        yield return TestCase.Create(CreateExpandoObject([new("x", 1), new("y", "str")]), p);

        yield return TestCase.Create(new DerivedList { 1, 2, 1, 3 });
        yield return TestCase.Create(new DerivedDictionary { ["key"] = "value" });

        yield return TestCase.Create(new StructList<int> { 1, 2, 1, 3 }, p);
        yield return TestCase.Create(new StructDictionary<string, string> { ["key"] = "value" }, p);
        yield return TestCase.Create(ExplicitlyImplementedList<int>.Create([1, 2, 1, 3]), p);
        yield return TestCase.Create(ExplicitlyImplementedDictionary<string, string>.Create([new("key", "value")]), p);
        yield return TestCase.Create(ExplicitlyImplementedIList.Create([null, false, 42, "value"]));
        yield return TestCase.Create(ExplicitlyImplementedIDictionary.Create([new("key", "value")]));
        yield return TestCase.Create(ExplicitlyImplementedIList.Create([null, true, 42, "string"]));
        yield return TestCase.Create(ExplicitlyImplementedIDictionary.Create([new("key", 42)]));
        yield return TestCase.Create<CollectionWithBuilderAttribute>([1, 2, 1, 3]);
        yield return TestCase.Create((GenericCollectionWithBuilderAttribute<int>)[1, 2, 1, 3], p);
        yield return TestCase.Create(new CollectionWithEnumerableCtor([1, 2, 1, 3]));
        yield return TestCase.Create(new DictionaryWithEnumerableCtor([new("key", 42)]));
        yield return TestCase.Create(new CollectionWithSpanCtor([1, 2, 1, 3]), usesSpanConstructor: true);
        yield return TestCase.Create(new DictionaryWithSpanCtor([new("key", 42)]), usesSpanConstructor: true);

        yield return TestCase.Create(new Collection<int> { 1, 2, 3 }, p);
        yield return TestCase.Create(new ObservableCollection<int> { 1, 2, 1, 3 }, p);
        yield return TestCase.Create(new MyKeyedCollection<int> { 1, 2, 1, 3 }, p);
        yield return TestCase.Create(new MyKeyedCollection<string> { "1", "2", "1", "3" }, p);
        yield return TestCase.Create(new ReadOnlyCollection<int>([1, 2, 1, 3]), p);
        yield return TestCase.Create(new ReadOnlyDictionary<int, int>(new Dictionary<int, int> { [1] = 1, [2] = 2 }), p);
#if NET9_0_OR_GREATER
        yield return TestCase.Create(new ReadOnlySet<int>(new HashSet<int> { 1, 2, 3 }), isSet: true, provider: p);
#endif

        yield return TestCase.Create(new EnumerableAsObject { Value = 42 });
        yield return TestCase.Create(new DictionaryAsEnumerable { new("key", "value") });
        yield return TestCase.Create(new ObjectAsNone { Name = "me", Age = 7 });

        yield return TestCase.Create(ImmutableArray.Create(1, 2, 1, 3), p);
        yield return TestCase.Create(ImmutableList.Create("1", "2", "1", "3"), p);
        yield return TestCase.Create(ImmutableList.Create("1", "2", null), p);
        yield return TestCase.Create(ImmutableQueue.Create(1, 2, 1, 3), p);
        yield return TestCase.Create(ImmutableStack.Create(1, 2, 1, 3), isStack: true, provider: p);
        yield return TestCase.Create(ImmutableHashSet.Create(1, 2, 1, 3), isSet: true, provider: p);
        yield return TestCase.Create(ImmutableSortedSet.Create(1, 2, 1, 3), isSet: true, provider: p);
        yield return TestCase.Create(ImmutableDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), p);
        yield return TestCase.Create(ImmutableDictionary.CreateRange(new Dictionary<string, string?> { ["key"] = null }), p);
        yield return TestCase.Create(ImmutableSortedDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), p);
        yield return TestCase.Create((IImmutableList<int>)[1, 2, 1, 3], p);
        yield return TestCase.Create((IImmutableQueue<int>)[1, 2, 1, 3], p);
        yield return TestCase.Create((IImmutableSet<int>)[1, 2, 1, 3], isSet: true, provider: p);
        yield return TestCase.Create((IImmutableStack<int>)[1, 2, 1, 3], isStack: true, provider: p);
        yield return TestCase.Create((IImmutableDictionary<string, string>)ImmutableSortedDictionary.CreateRange(new Dictionary<string, string> { ["key"] = "value" }), p);
        yield return TestCase.Create(Enumerable.Range(1, 5).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToFrozenDictionary(i => i, i => i), p);
        yield return TestCase.Create(Enumerable.Range(1, 5).ToFrozenSet(), isSet: true, provider: p);

        yield return TestCase.Create(new PocoWithListAndDictionaryProps(@string: "myString")
        {
            List = [1, 2, 3],
            Dict = new() { ["key1"] = 42, ["key2"] = -1 },
        });

        yield return TestCase.Create(new SimplePoco { Value = 42 });
        yield return TestCase.Create(new BaseClass { X = 1 });
        yield return TestCase.Create(new DerivedClass { X = 1, Y = 2 });
        yield return TestCase.Create(new DerivedClassWithVirtualProperties());

        var value = new DiamondImplementation { X = 1, Y = 2, Z = 3, W = 4, T = 5 };
        yield return TestCase.Create<IBaseInterface>(value);
        yield return TestCase.Create<IDerivedInterface>(value);
        yield return TestCase.Create<IDerived2Interface>(value);
        yield return TestCase.Create<IDerived3Interface>(value);
        yield return TestCase.Create<IDiamondInterface>(value);

        yield return TestCase.Create(new ParameterlessRecord());
        yield return TestCase.Create(new ParameterlessStructRecord());

        yield return TestCase.Create(new ClassWithNullabilityAttributes());
        yield return TestCase.Create(new ClassWithNullabilityAttributes<string>
        {
            NotNullField = "str",
            DisallowNullField = "str",
            DisallowNullProperty = "str",
            NotNullProperty = "str"
        }, p);

        yield return TestCase.Create(new ClassWithNotNullProperty<string> { Property = "Value" }, p);
        yield return TestCase.Create(new StructWithNullabilityAttributes());
        yield return TestCase.Create(new ClassWithInternalConstructor(42));
        yield return TestCase.Create(new NonNullStringRecord("str"));
        yield return TestCase.Create(new NullableStringRecord(null));
        yield return TestCase.Create(new NotNullGenericRecord<string>("str"), p);
        yield return TestCase.Create(new NotNullClassGenericRecord<string>("str"), p);
        yield return TestCase.Create(new NullClassGenericRecord<string>("str"), p);
        yield return TestCase.Create(new NullObliviousGenericRecord<string>("str"), p);

        yield return TestCase.Create(new SimpleRecord(42));
        yield return TestCase.Create(new GenericRecord<int>(42), p);
        yield return TestCase.Create(new GenericRecord<string>("str"), p);
        yield return TestCase.Create(new GenericRecord<GenericRecord<bool>>(new GenericRecord<bool>(true)), p);
        yield return TestCase.Create(new GenericRecordStruct<int>(42), p);
        yield return TestCase.Create(new GenericRecordStruct<string>("str"), p);
        yield return TestCase.Create(new GenericRecordStruct<GenericRecordStruct<bool>>(new GenericRecordStruct<bool>(true)), p);
        yield return TestCase.Create(new GenericRecordStruct<string>("str"), p);
        yield return TestCase.Create(new GenericRecordStruct<GenericRecordStruct<bool>>(new GenericRecordStruct<bool>(true)), p);

        yield return TestCase.Create(new ClassWithInitOnlyProperties { Value = 99, Values = [99] });
        yield return TestCase.Create(new StructWithInitOnlyProperties { IntProp = 42, StringProp = "string" });
        yield return TestCase.Create(new GenericStructWithInitOnlyProperty<int> { Value = 42 }, p);
        yield return TestCase.Create(new GenericStructWithInitOnlyProperty<GenericStructWithInitOnlyProperty<int>> { Value = new() { Value = 42 } }, p);

        if (!ReflectionHelpers.IsNetFrameworkProcessOnWindowsArm)
        {
            // PropertyInfo.SetMethod fails in ARM64 .NET framework with 'System.BadImageFormatException : Bad binary signature.'
            yield return TestCase.Create(new GenericStructWithInitOnlyProperty<string> { Value = "str" }, p);
            yield return TestCase.Create(new GenericStructWithInitOnlyProperty<GenericStructWithInitOnlyProperty<string>> { Value = new() { Value = "str" } }, p);
        }

        yield return TestCase.Create(new ComplexStruct { real = 0, im = 1 });
        yield return TestCase.Create(new ComplexStructWithProperties { Real = 0, Im = 1 });
        yield return TestCase.Create(new StructWithDefaultCtor());

        yield return TestCase.Create(new ValueTuple(), p);
        yield return TestCase.Create(new ValueTuple<int>(42), p);
        yield return TestCase.Create((42, "string"), p);
        yield return TestCase.Create((1, 2, 3, 4, 5, 6, 7), p);
        yield return TestCase.Create((IntValue: 42, StringValue: "string", BoolValue: true), p);
        yield return TestCase.Create((IntValue: 42, StringValue: "string", (1, 0)), p);
        yield return TestCase.Create((x1: 1, x2: 2, x3: 3, x4: 4, x5: 5, x6: 6, x7: 7, x8: 8, x9: 9), p);
        yield return TestCase.Create((x01: 01, x02: 02, x03: 03, x04: 04, x05: 05, x06: 06, x07: 07, x08: 08, x09: 09, x10: 10,
                             x11: 11, x12: 12, x13: 13, x14: 14, x15: 15, x16: 16, x17: 17, x18: 18, x19: 19, x20: 20,
                             x21: 21, x22: 22, x23: 23, x24: 24, x25: 25, x26: 26, x27: 27, x28: 28, x29: 29, x30: 30), p);

        yield return TestCase.Create(new Dictionary<int, (int, int)> { [0] = (1, 1) }, p);

        yield return TestCase.Create(new Tuple<int>(1), p);
        yield return TestCase.Create(new Tuple<int, int>(1, 2), p);
        yield return TestCase.Create(new Tuple<int, string, bool>(1, "str", true), p);
        yield return TestCase.Create(new Tuple<int, int, int, int, int, int, int>(1, 2, 3, 4, 5, 6, 7), p);
        yield return TestCase.Create(new Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10)), p);
        yield return TestCase.Create(new Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>(1, 2, 3, 4, 5, 6, 7, new(8, 9, 10, 11, 12, 13, 14, new(15))), p);

        yield return TestCase.Create(new ClassWithReadOnlyField());
        yield return TestCase.Create(new ClassWithRequiredField { x = 42 });
        yield return TestCase.Create(new StructWithRequiredField { x = 42 });
        yield return TestCase.Create(new ClassWithRequiredProperty { X = 42 });
        yield return TestCase.Create(new StructWithRequiredProperty { X = 42 });
        yield return TestCase.Create(new StructWithRequiredPropertyAndDefaultCtor { y = 2 });
        yield return TestCase.Create(new StructWithRequiredFieldAndDefaultCtor { y = 2 });

        yield return TestCase.Create(new ClassWithSetsRequiredMembersCtor(42));
        yield return TestCase.Create(new StructWithSetsRequiredMembersCtor(42));
        yield return TestCase.Create(new ClassWithSetsRequiredMembersDefaultCtor { Value = 42 });
        yield return TestCase.Create(new StructWithSetsRequiredMembersDefaultCtor { Value = 42 });

        yield return TestCase.Create(new ClassWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return TestCase.Create(new StructWithRequiredAndInitOnlyProperties
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return TestCase.Create(new ClassRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return TestCase.Create(new StructRecordWithRequiredAndInitOnlyProperties(1, 2, 3)
        {
            RequiredAndInitOnlyString = "str1",
            RequiredString = "str2",
            InitOnlyString = "str3",

            RequiredAndInitOnlyInt = 1,
            RequiredInt = 2,
            InitOnlyInt = 3,

            requiredField = true,
        });

        yield return TestCase.Create(new ClassWithDefaultConstructorAndSingleRequiredProperty { Value = 42 });
        yield return TestCase.Create(new ClassWithParameterizedConstructorAnd2OptionalSetters(42));
        yield return TestCase.Create(new ClassWithParameterizedConstructorAnd10OptionalSetters(42));
        yield return TestCase.Create(new ClassWithParameterizedConstructorAnd70Setters(42) { X03 = 3, X10 = 10, X47 = 47 });

        yield return TestCase.Create(new ClassRecord(0, 1, 2, 3));
        yield return TestCase.Create(new StructRecord(0, 1, 2, 3));
        yield return TestCase.Create(new LargeClassRecord());

        yield return TestCase.Create(new ClassWithIndexer());

        yield return TestCase.Create(new RecordWithDefaultParams());
        yield return TestCase.Create(new RecordWithDefaultParams2());

        yield return TestCase.Create(new RecordWithNullableDefaultParams());
        yield return TestCase.Create(new RecordWithNullableDefaultParams2());

        yield return TestCase.Create(new RecordWithSpecialValueDefaultParams(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        yield return TestCase.Create(new RecordWithEnumAndNullableParams(MyEnum.A, MyEnum.C));
        yield return TestCase.Create(new RecordWithNullableDefaultEnum());

        yield return TestCase.Create(new GenericContainer<string>.Inner { Value = "str" }, p);
        yield return TestCase.Create(new GenericContainer<string>.Inner<string> { Value1 = "str", Value2 = "str2" }, p);

        yield return TestCase.Create(new MyLinkedList<int>
        {
            Value = 1,
            Next = new()
            {
                Value = 2,
                Next = new()
                {
                    Value = 3,
                    Next = null,
                }
            }
        }, p);

        yield return TestCase.Create<RecursiveClassWithNonNullableOccurrence>(null!);
        yield return TestCase.Create(new RecursiveClassWithNonNullableOccurrences
        {
            Values = [],
        });

        DateTimeOffset today = DateTimeOffset.Parse("2023-12-07", CultureInfo.InvariantCulture);
        yield return TestCase.Create(new Todos(
            [ new (Id: 0, "Wash the dishes.", today, Status.Done),
              new (Id: 1, "Dry the dishes.", today, Status.Done),
              new (Id: 2, "Turn the dishes over.", today, Status.InProgress),
              new (Id: 3, "Walk the kangaroo.", today.AddDays(1), Status.NotStarted),
              new (Id: 4, "Call Grandma.", today.AddDays(1), Status.NotStarted)]));

        yield return TestCase.Create(new RecordWith21ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2"));

        yield return TestCase.Create(new RecordWith42ConstructorParameters(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2"));

        yield return TestCase.Create(new RecordWith42ConstructorParametersAndRequiredProperties(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2")
        {
            requiredField = 42,
            RequiredProperty = "str"
        });

        yield return TestCase.Create(new StructRecordWith42ConstructorParametersAndRequiredProperties(
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2",
            "str", 2, true, TimeSpan.MinValue, DateTime.MaxValue, 42, "str2")
        {
            requiredField = 42,
            RequiredProperty = "str"
        });

        yield return TestCase.Create(new ClassWith40RequiredMembers
        {
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09,
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29,
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        });

        yield return TestCase.Create(new StructWith40RequiredMembers
        {
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09,
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29,
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        });

        yield return TestCase.Create(new StructWith40RequiredMembersAndDefaultCtor
        {
            r00 = 00, r01 = 01, r02 = 02, r03 = 03, r04 = 04, r05 = 05, r06 = 06, r07 = 07, r08 = 08, r09 = 09,
            r10 = 10, r11 = 11, r12 = 12, r13 = 13, r14 = 14, r15 = 15, r16 = 16, r17 = 17, r18 = 18, r19 = 19,
            r20 = 20, r21 = 21, r22 = 22, r23 = 23, r24 = 24, r25 = 25, r26 = 26, r27 = 27, r28 = 28, r29 = 29,
            r30 = 30, r31 = 31, r32 = 32, r33 = 33, r34 = 34, r35 = 35, r36 = 36, r37 = 37, r38 = 38, r39 = 39,
        });

        yield return TestCase.Create(new ClassWithInternalMembers { X = 1, Y = 2, Z = 3, W = 4, internalField = 5 });
        yield return TestCase.Create(new ClassWithPropertyAnnotations { X = 1, Y = 2, Z = true });
        yield return TestCase.Create(new ClassWithConstructorAndAnnotations(1, 2, true));
        yield return TestCase.Create(new DerivedClassWithPropertyShapeAnnotations());

        yield return TestCase.Create(new WeatherForecastDTO
        {
            Id = "id",
            Date = DateTime.Parse("1975-01-01", CultureInfo.InvariantCulture),
            DatesAvailable = [DateTime.Parse("1975-01-01", CultureInfo.InvariantCulture), DateTime.Parse("1976-01-01", CultureInfo.InvariantCulture)],
            Summary = "Summary",
            SummaryField = "SummaryField",
            TemperatureCelsius = 42,
            SummaryWords = ["Summary", "Words"],
            TemperatureRanges = new()
            {
                ["Range1"] = new() { Low = 1, High = 2 },
                ["Range2"] = new() { Low = 3, High = 4 },
            }
        });

        yield return TestCase.Create(new DerivedClassWithShadowingMember { PropA = "propA", PropB = 2, FieldA = 1, FieldB = "fieldB" });
        yield return TestCase.Create(new ClassWithMultipleSelfReferences { First = new ClassWithMultipleSelfReferences() });
        yield return TestCase.Create(new ClassWithNullableTypeParameters());
        yield return TestCase.Create(new ClassWithNullableTypeParameters<int>(), p);
        yield return TestCase.Create(new ClassWithNullableTypeParameters<int?>(), p);
        yield return TestCase.Create(new ClassWithNullableTypeParameters<string>(), p);
        yield return TestCase.Create(new CollectionWithNullableElement<int>([(1, 1)]), p);
        yield return TestCase.Create(new CollectionWithNullableElement<int?>([(null, 1), (42, 1)]), p);
        yield return TestCase.Create(new CollectionWithNullableElement<string?>([(null, 1), ("str", 2)]), p);
        yield return TestCase.Create(new DictionaryWithNullableEntries<int>([new("key1", (1, 1))]), p);
        yield return TestCase.Create(new DictionaryWithNullableEntries<int?>([new("key1", (null, 1)), new("key2", (42, 1))]), p);
        yield return TestCase.Create(new DictionaryWithNullableEntries<string>([new("key1", (null, 1)), new("key2", ("str", 1))]), p);
        yield return TestCase.Create(new ClassWithNullableProperty<int>(), p);
        yield return TestCase.Create(new ClassWithNullableProperty<int?>(), p);
        yield return TestCase.Create(new ClassWithNullableProperty<string>(), p);

        yield return TestCase.Create(new PersonClass("John", 40));
        yield return TestCase.Create(new PersonStruct("John", 40));
        yield return TestCase.Create((PersonStruct?)new PersonStruct("John", 40), p);
        yield return TestCase.Create<IPersonInterface>(new IPersonInterface.Impl("John", 40));
        yield return TestCase.Create<PersonAbstractClass>(new PersonAbstractClass.Impl("John", 40));
        yield return TestCase.Create(new PersonRecord("John", 40));
        yield return TestCase.Create(new PersonRecordStruct("John", 40));
        yield return TestCase.Create((PersonRecordStruct?)new PersonRecordStruct("John", 40), p);
        yield return TestCase.Create(new ClassWithMultipleConstructors(z: 3) { X = 1, Y = 2 });
        yield return TestCase.Create(new ClassWithConflictingAnnotations
        {
            NonNullNullableString = new() { Value = "str" },
            NullableString = new() { Value = null },
        });

        yield return TestCase.Create(ClassWithRefConstructorParameter.Create(), hasRefConstructorParameters: true);
        yield return TestCase.Create(new ClassWithOutConstructorParameter(out _), hasRefConstructorParameters: true, hasOutConstructorParameters: true);
        yield return TestCase.Create(ClassWithMultipleRefConstructorParameters.Create(), hasRefConstructorParameters: true);
        yield return TestCase.Create(ClassWithRefConstructorParameterPrivate.Create(), hasRefConstructorParameters: true);
        yield return TestCase.Create(ClassWithMultipleRefConstructorParametersPrivate.Create(), hasRefConstructorParameters: true);
        yield return TestCase.Create(GenericClassWithMultipleRefConstructorParametersPrivate<int>.Create(42), hasRefConstructorParameters: true, provider: p);
        yield return TestCase.Create(GenericClassWithMultipleRefConstructorParametersPrivate<string>.Create("str"), hasRefConstructorParameters: true, provider: p);
        yield return TestCase.Create(GenericClassWithPrivateConstructor<int>.Create(42), p);
        yield return TestCase.Create(GenericClassWithPrivateConstructor<string>.Create("str"), p);
        yield return TestCase.Create(GenericClassWithPrivateField<int>.Create(42), p);
        yield return TestCase.Create(GenericClassWithPrivateField<string>.Create("str"), p);
        yield return TestCase.Create(GenericStructWithPrivateField<int>.Create(42), p);
        yield return TestCase.Create(GenericStructWithPrivateField<string>.Create("str"), p);
        yield return TestCase.Create(new ClassWithUnsupportedPropertyTypes());
        yield return TestCase.Create(new ClassWithIncludedPrivateMembers());
        yield return TestCase.Create(new StructWithIncludedPrivateMembers());
        yield return TestCase.Create(GenericStructWithPrivateIncludedMembers<int>.Create(1, 2), p);
        yield return TestCase.Create(GenericStructWithPrivateIncludedMembers<string>.Create("1", "2"), p);
        yield return TestCase.Create(new Vector3D(1, 2, 3));
        yield return TestCase.Create(new ClassWithAmbiguousCtors1(1, 2, 3));
        yield return TestCase.Create(new ClassWithAmbiguousCtors2(1, 2, 3));
        yield return TestCase.Create(new ClassWithAmbiguousCtors3(1, 2));
        yield return TestCase.Create(new Point(1, 2), p);
        yield return TestCase.Create(new @class(@string: "string", @__makeref: 42, @yield: true));
        yield return TestCase.Create(new TypeWithStringSurrogate("string"));
        yield return TestCase.Create(new TypeWithRecordSurrogate(42, "string"));
        yield return TestCase.Create(EnumWithRecordSurrogate.A, p);
        yield return TestCase.Create(new TypeWithGenericMarshaler<string>("str"), p);
        yield return TestCase.Create(new GenericDictionaryWithMarshaler<string, int>() { ["key"] = 42 }, p);

        // Union types
        yield return TestCase.Create(new PolymorphicClass(42),
            additionalValues: [
                new PolymorphicClass.DerivedClass(42, "str"),
                new PolymorphicClass.DerivedEnumerable { 42 },
                new PolymorphicClass.DerivedDictionary { ["key"] = 42 }],
            isUnion: true);
        yield return TestCase.Create<IPolymorphicInterface>(
            new IPolymorphicInterface.Derived { X = 1 },
            additionalValues: [
                new IPolymorphicInterface.IDerived1.Impl { X = 1, Y = 2 },
                new IPolymorphicInterface.IDerived2.Impl { X = 1, Z = 2 },
                new IPolymorphicInterface.IDerived1.IDerivedDerived1.Impl2 { X = 1, Y = 2, Z = 3 },
                new IPolymorphicInterface.IDerived2.IDerivedDerived2.Impl2 { X = 1, Y = 2, Z = 3 },
                new IPolymorphicInterface.IDiamond.Impl2 { X = 1 }],
            isUnion: true);

        yield return TestCase.Create(
            (PolymorphicClassWithGenericDerivedType)new PolymorphicClassWithGenericDerivedType.Derived<int>(42),
            additionalValues: [new PolymorphicClassWithGenericDerivedType.Derived<string>("str")],
            isUnion: true);

        yield return TestCase.Create<Tree>(new Tree.Node(42, new Tree.Leaf(), new Tree.Leaf()), additionalValues: [new Tree.Leaf()], isUnion: true);
        yield return TestCase.Create((GenericTree<string>)new GenericTree<string>.Node("str", new GenericTree<string>.Leaf(), new GenericTree<string>.Leaf()), additionalValues: [new GenericTree<string>.Leaf()], isUnion: true, provider: p);
        yield return TestCase.Create((GenericTree<int>)new GenericTree<int>.Node(42, new GenericTree<int>.Leaf(), new GenericTree<int>.Leaf()), additionalValues: [new GenericTree<int>.Leaf()], isUnion: true, provider: p);

        yield return TestCase.Create(new RecordWithoutNamespace(42));
        yield return TestCase.Create(new GenericRecordWithoutNamespace<int>(42), p);
        yield return TestCase.Create(new GenericContainerWithoutNamespace<int>.Record<string>(42, "str"), p);

        yield return TestCase.Create(new AsyncEnumerableClass([1, 1, 2, 3, 5, 8]));
        yield return TestCase.Create((IAsyncEnumerable<int>)new AsyncEnumerableClass([1, 1, 2, 3, 5, 8]), p);

        // IsRequired on attributes
        yield return TestCase.Create(new PropertyRequiredByAttribute { AttributeRequiredProperty = true });
        yield return TestCase.Create(new PropertyNotRequiredByAttribute { AttributeNotRequiredProperty = true });
        yield return TestCase.Create(new CtorParameterRequiredByAttribute(true));
        yield return TestCase.Create(new CtorParameterNotRequiredByAttribute(true));

        // F# types
        yield return TestCase.Create(FSharpValues.unit, p);
        yield return TestCase.Create(new FSharpRecord(42, "str", true), p);
        yield return TestCase.Create(new FSharpStructRecord(42, "str", true), p);
        yield return TestCase.Create(new GenericFSharpRecord<string>("str"), p);
        yield return TestCase.Create(new GenericFSharpStructRecord<string>("str"), p);
        yield return TestCase.Create(new FSharpClass("str", 42), p);
        yield return TestCase.Create(new FSharpStruct("str", 42), p);
        yield return TestCase.Create(new GenericFSharpClass<string>("str"), p);
        yield return TestCase.Create(new GenericFSharpStruct<string>("str"), p);
        yield return TestCase.Create(new FSharpOption<int>(42), additionalValues: [FSharpOption<int>.None], provider: p);
        yield return TestCase.Create(FSharpOption<string>.Some("str"), additionalValues: [FSharpOption<string>.None], provider: p);
        yield return TestCase.Create(FSharpValueOption<int>.Some(42), additionalValues: [FSharpValueOption<int>.None], provider: p);
        yield return TestCase.Create(FSharpValueOption<string>.Some("str"), additionalValues: [FSharpValueOption<string>.None], provider: p);
        yield return TestCase.Create(ListModule.OfSeq([1, 2, 1, 3]), p);
        yield return TestCase.Create(SetModule.OfSeq([1, 2, 3]), p);
        yield return TestCase.Create(MapModule.OfSeq<string, int>([new("key1", 1), new("key2", 2)]), p);
        yield return TestCase.Create(FSharpRecordWithCollections.Create(), p);
        yield return TestCase.Create(FSharpUnion.NewA("str", 42), additionalValues: [FSharpUnion.B, FSharpUnion.NewC(42)], isUnion: true, provider: p);
        yield return TestCase.Create(FSharpEnumUnion.A, additionalValues: [FSharpEnumUnion.B, FSharpEnumUnion.C], isUnion: true, provider: p);
        yield return TestCase.Create(FSharpSingleCaseUnion.NewCase(42), isUnion: true, provider: p);
        yield return TestCase.Create(GenericFSharpUnion<string>.NewA("str"), additionalValues: [GenericFSharpUnion<string>.B, GenericFSharpUnion<string>.NewC(42)], isUnion: true, provider: p);
        yield return TestCase.Create(FSharpResult<string, int>.NewOk("ok"), additionalValues: [FSharpResult<string, int>.NewError(-1)], isUnion: true, provider: p);
        yield return TestCase.Create(FSharpStructUnion.NewA("str", 42), additionalValues: [FSharpStructUnion.B, FSharpStructUnion.NewC(42)], isUnion: true, provider: p);
        yield return TestCase.Create(FSharpEnumStructUnion.A, additionalValues: [FSharpEnumStructUnion.B, FSharpEnumStructUnion.C], isUnion: true, provider: p);
        yield return TestCase.Create(FSharpSingleCaseStructUnion.NewCase(42), isUnion: true, provider: p);
        yield return TestCase.Create(GenericFSharpStructUnion<string>.NewA("str"), additionalValues: [GenericFSharpStructUnion<string>.B, GenericFSharpStructUnion<string>.NewC(42)], isUnion: true, provider: p);
        yield return TestCase.Create(FSharpExpr.True, additionalValues: [FSharpExpr.False, FSharpExpr.Y], isUnion: true, provider: p);
        yield return TestCase.Create(NullaryUnion.A, additionalValues: [NullaryUnion.NewB(42)], isUnion: true, provider: p);

        // Delegate types
        yield return TestCase.Create(new Action(() => { }), p);
        yield return TestCase.Create(new Action<int>(_ => { }), p);
        yield return TestCase.Create(new Action<int, int>((_, _) => { }), p);
        yield return TestCase.Create(new Func<int>(() => 42), p);
        yield return TestCase.Create(new Func<int, int>(x => x), p);
        yield return TestCase.Create(new Func<int, int, int>((x, y) => x + y), p);
        yield return TestCase.Create(new Func<int, int, int, int, int, int, int>((x1, x2, x3, x4, x5, x6) => x1 + x2 + x3 + x4 + x5 + x6), p);
        yield return TestCase.Create(new Func<int, Task>(_ => Task.CompletedTask), p);
        yield return TestCase.Create(new Func<int, Task<int>>(x => Task.FromResult(x)), p);
        yield return TestCase.Create(new Func<int, int, Task<int>>((x, y) => Task.FromResult(x + y)), p);
        yield return TestCase.Create(new Func<int, ValueTask>(_ => default), p);
        yield return TestCase.Create(new Func<int, ValueTask<int>>(x => new(x)), p);
        yield return TestCase.Create(new Func<int, int, ValueTask<int>>((x, y) => new(x + y)), p);
        yield return TestCase.Create(new Func<int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, ValueTask<int>>(
            (x1, x2, x3, x4, x5, x6, x7, x8, x9, x10, x11, x12, x13, x14, x15, x16) => new(x1 + x2 + x3 + x4 + x5 + x6 + x7 + x8 + x9 + x10 + x11 + x12 + x13 + x14 + x15 + x16)), p);

        yield return TestCase.Create(new Getter<int, int>((ref int x) => x), p);
        yield return TestCase.Create(new Setter<int, int>((ref int x, int value) => { x += value; }), p);
        yield return TestCase.Create(new EventHandler((sender, args) => { }), p);
        yield return TestCase.Create(new CustomDelegate((ref string? x, int y) => x?.Length ?? 0 + y), p);
        yield return TestCase.Create(new LargeDelegate(
            (p01, p02, p03, p04, p05, p06, p07, p08, p09, p10,
             p11, p12, p13, p14, p15, p16, p17, p18, p19, p20,
             p21, p22, p23, p24, p25, p26, p27, p28, p29, p30,
             p31, p32, p33, p34, p35, p36, p37, p38, p39, p40,
             p41, p42, p43, p44, p45, p46, p47, p48, p49, p50,
             p51, p52, p53, p54, p55, p56, p57, p58, p59, p60,
             p61, p62, p63, p64, p65, p66, p67, p68, p69, p70) =>
            p01 + p02 + p03 + p04 + p05 + p06 + p07 + p08 + p09 + p10 +
            p11 + p12 + p13 + p14 + p15 + p16 + p17 + p18 + p19 + p20 +
            p21 + p22 + p23 + p24 + p25 + p26 + p27 + p28 + p29 + p30 +
            p31 + p32 + p33 + p34 + p35 + p36 + p37 + p38 + p39 + p40 +
            p41 + p42 + p43 + p44 + p45 + p46 + p47 + p48 + p49 + p50 +
            p51 + p52 + p53 + p54 + p55 + p56 + p57 + p58 + p59 + p60 +
            p61 + p62 + p63 + p64 + p65 + p66 + p67 + p68 + p69 + p70), p);
        yield return TestCase.Create(new LargeAsyncDelegate(
            (p01, p02, p03, p04, p05, p06, p07, p08, p09, p10,
             p11, p12, p13, p14, p15, p16, p17, p18, p19, p20,
             p21, p22, p23, p24, p25, p26, p27, p28, p29, p30,
             p31, p32, p33, p34, p35, p36, p37, p38, p39, p40,
             p41, p42, p43, p44, p45, p46, p47, p48, p49, p50,
             p51, p52, p53, p54, p55, p56, p57, p58, p59, p60,
             p61, p62, p63, p64, p65, p66, p67, p68, p69, p70) =>
           Task.FromResult(
            p01 + p02 + p03 + p04 + p05 + p06 + p07 + p08 + p09 + p10 +
            p11 + p12 + p13 + p14 + p15 + p16 + p17 + p18 + p19 + p20 +
            p21 + p22 + p23 + p24 + p25 + p26 + p27 + p28 + p29 + p30 +
            p31 + p32 + p33 + p34 + p35 + p36 + p37 + p38 + p39 + p40 +
            p41 + p42 + p43 + p44 + p45 + p46 + p47 + p48 + p49 + p50 +
            p51 + p52 + p53 + p54 + p55 + p56 + p57 + p58 + p59 + p60 +
            p61 + p62 + p63 + p64 + p65 + p66 + p67 + p68 + p69 + p70)), p);

        yield return TestCase.Create(FSharpFunctions.simpleFunc, p);
        yield return TestCase.Create(FSharpFunctions.curriedFunc, p);
        yield return TestCase.Create(FSharpFunctions.unitAcceptingFunc, p);
        yield return TestCase.Create(FSharpFunctions.curriedUnitAcceptingFunc, p);
        yield return TestCase.Create(FSharpFunctions.unitReturningFunc, p);
        yield return TestCase.Create(FSharpFunctions.curriedUnitReturningFunc, p);
        yield return TestCase.Create(FSharpFunctions.tupleAcceptingFunc, p);
        yield return TestCase.Create(FSharpFunctions.taskFunc, p);
        yield return TestCase.Create(FSharpFunctions.curriedTaskFunc, p);

        // RPC types
        yield return TestCase.Create(new ClassWithMethodShapes());
        yield return TestCase.Create(new StructWithMethodShapes());
        yield return TestCase.Create<InterfaceWithMethodShapes>(new ClassWithMethodShapes(), additionalValues: [new StructWithMethodShapes()]);
        yield return TestCase.Create(new BaseClassWithMethodShapes(), additionalValues: [new ClassWithMethodShapes()]);
        yield return TestCase.Create(new RpcService());
        yield return TestCase.Create<InterfaceWithDiamondMethodShapes>(new InterfaceWithDiamondMethodShapes.Impl());

        // Type with events
        yield return TestCase.Create(new ClassWithEvent());
        yield return TestCase.Create(new StructWithEvent());
        yield return TestCase.Create(new DerivedClassWithEvent());
        yield return TestCase.Create(new ClassWithStaticEvent());
        yield return TestCase.Create(new ClassWithPrivateEvent());
        yield return TestCase.Create(new ClassWithPrivateStaticEvent());
    }

    private static ExpandoObject CreateExpandoObject(IEnumerable<KeyValuePair<string, object?>> values)
    {
        ExpandoObject obj = new();
        IDictionary<string, object?> dictView = obj;
        foreach (var kvp in values)
        {
            dictView[kvp.Key] = kvp.Value;
        }
        return obj;
    }
}

[GenerateShape]
public partial class DerivedList : List<int>;

[GenerateShape]
public partial class DerivedDictionary : Dictionary<string, string>;

public readonly struct StructList<T> : IList<T>
{
    private readonly List<T> _values;
    public StructList() => _values = new();
    public T this[int index] { get => _values[index]; set => _values[index] = value; }
    public int Count => _values.Count;
    public bool IsReadOnly => false;
    public void Add(T item) => _values.Add(item);
    public void Clear() => _values.Clear();
    public bool Contains(T item) => _values.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);
    public int IndexOf(T item) => _values.IndexOf(item);
    public void Insert(int index, T item) => _values.Insert(index, item);
    public bool Remove(T item) => _values.Remove(item);
    public void RemoveAt(int index) => _values.RemoveAt(index);
    public IEnumerator<T> GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
}

public readonly struct StructDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dictionary;
    public StructDictionary() => _dictionary = new();
    public TValue this[TKey key] { get => _dictionary[key]; set => _dictionary[key] = value; }
    public ICollection<TKey> Keys => _dictionary.Keys;
    public ICollection<TValue> Values => _dictionary.Values;
    public int Count => _dictionary.Count;
    public bool IsReadOnly => false;
    public void Add(TKey key, TValue value) => _dictionary.Add(key, value);
    public void Add(KeyValuePair<TKey, TValue> item) => _dictionary.Add(item.Key, item.Value);
    public void Clear() => _dictionary.Clear();
    public bool Contains(KeyValuePair<TKey, TValue> item) => _dictionary.Contains(item);
    public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((IDictionary<TKey, TValue>)_dictionary).CopyTo(array, arrayIndex);
    public bool Remove(TKey key) => _dictionary.Remove(key);
    public bool Remove(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_dictionary).Remove(item);
    public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();
}

public class ExplicitlyImplementedList<T> : IList<T>
{
    public static ExplicitlyImplementedList<T> Create(IEnumerable<T> values)
    {
        ExplicitlyImplementedList<T> list = new();
        list._values.AddRange(values);
        return list;
    }

    private readonly List<T> _values;
    public ExplicitlyImplementedList() => _values = new();
    T IList<T>.this[int index] { get => _values[index]; set => _values[index] = value; }
    int ICollection<T>.Count => _values.Count;
    bool ICollection<T>.IsReadOnly => false;
    void ICollection<T>.Add(T item) => _values.Add(item);
    void ICollection<T>.Clear() => _values.Clear();
    bool ICollection<T>.Contains(T item) => _values.Contains(item);
    void ICollection<T>.CopyTo(T[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);
    int IList<T>.IndexOf(T item) => _values.IndexOf(item);
    void IList<T>.Insert(int index, T item) => _values.Insert(index, item);
    bool ICollection<T>.Remove(T item) => _values.Remove(item);
    void IList<T>.RemoveAt(int index) => _values.RemoveAt(index);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
}

public sealed class ExplicitlyImplementedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    where TKey : notnull
{
    public static ExplicitlyImplementedDictionary<TKey, TValue> Create(IEnumerable<KeyValuePair<TKey, TValue>> values)
    {
        ExplicitlyImplementedDictionary<TKey, TValue> dictionary = new();
        foreach (var kvp in values)
        {
            dictionary._dictionary.Add(kvp.Key, kvp.Value);
        }
        return dictionary;
    }

    private readonly Dictionary<TKey, TValue> _dictionary;
    public ExplicitlyImplementedDictionary() => _dictionary = new();
    TValue IDictionary<TKey, TValue>.this[TKey key] { get => _dictionary[key]; set => _dictionary[key] = value; }
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => _dictionary.Keys;
    ICollection<TValue> IDictionary<TKey, TValue>.Values => _dictionary.Values;
    int ICollection<KeyValuePair<TKey, TValue>>.Count => _dictionary.Count;
    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;
    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => _dictionary.Add(key, value);
    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => _dictionary.Add(item.Key, item.Value);
    void ICollection<KeyValuePair<TKey, TValue>>.Clear() => _dictionary.Clear();
    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => _dictionary.Contains(item);
    bool IDictionary<TKey, TValue>.ContainsKey(TKey key) => _dictionary.ContainsKey(key);
    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((IDictionary<TKey, TValue>)_dictionary).CopyTo(array, arrayIndex);
    bool IDictionary<TKey, TValue>.Remove(TKey key) => _dictionary.Remove(key);
    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_dictionary).Remove(item);
    bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value!);
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => _dictionary.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();
}

[GenerateShape]
public sealed partial class ExplicitlyImplementedIList : IList
{
    public static ExplicitlyImplementedIList Create(IEnumerable<object?> values)
    {
        ExplicitlyImplementedIList list = new();
        foreach (var value in values)
        {
            list._values.Add(value);
        }
        return list;
    }
    private readonly List<object?> _values;
    public ExplicitlyImplementedIList() => _values = new();
    object? IList.this[int index] { get => _values[index]; set => _values[index] = value; }
    int ICollection.Count => _values.Count;
    bool IList.IsFixedSize => false;
    bool IList.IsReadOnly => false;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => _values;
    int IList.Add(object? value)
    {
        _values.Add(value);
        return _values.Count - 1;
    }

    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
    void IList.Clear() => _values.Clear();
    bool IList.Contains(object? value) => throw new NotImplementedException();
    void ICollection.CopyTo(Array array, int index) => throw new NotImplementedException();
    int IList.IndexOf(object? value) => throw new NotImplementedException();
    void IList.Insert(int index, object? value) => throw new NotImplementedException();
    void IList.Remove(object? value) => throw new NotImplementedException();
    void IList.RemoveAt(int index) => throw new NotImplementedException();
}

[GenerateShape]
public sealed partial class ExplicitlyImplementedIDictionary : IDictionary
{
    public static ExplicitlyImplementedIDictionary Create(IEnumerable<KeyValuePair<object, object?>> values)
    {
        ExplicitlyImplementedIDictionary dictionary = new();
        foreach (var kvp in values)
        {
            dictionary._dictionary.Add(kvp.Key, kvp.Value);
        }
        return dictionary;
    }

    private readonly Dictionary<object, object?> _dictionary;
    public ExplicitlyImplementedIDictionary() => _dictionary = new();
    object? IDictionary.this[object key] { get => _dictionary[key]; set => _dictionary[key] = value; }
    void IDictionary.Add(object key, object? value) => _dictionary.Add(key, value);
    IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();
    IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)_dictionary).GetEnumerator();
    void IDictionary.Clear() => _dictionary.Clear();
    int ICollection.Count => _dictionary.Count;
    bool IDictionary.Contains(object key) => _dictionary.ContainsKey(key);
    bool IDictionary.IsFixedSize => throw new NotImplementedException();
    bool IDictionary.IsReadOnly => throw new NotImplementedException();
    bool ICollection.IsSynchronized => throw new NotImplementedException();
    object ICollection.SyncRoot => throw new NotImplementedException();
    ICollection IDictionary.Keys => throw new NotImplementedException();
    ICollection IDictionary.Values => throw new NotImplementedException();
    void ICollection.CopyTo(Array array, int index) => throw new NotImplementedException();
    void IDictionary.Remove(object? key) => throw new NotImplementedException();
}

[GenerateShape]
public partial class SimplePoco
{
    public int Value { get; set; }
}

[GenerateShape]
public partial class PocoWithListAndDictionaryProps
{
    public PocoWithListAndDictionaryProps(bool @bool = true, string @string = "str")
    {
        Bool = @bool;
        String = @string;
    }

    public bool Bool { get; }
    public string String { get; }
    public List<int>? List { get; set; }
    public Dictionary<string, int>? Dict { get; set; }
}

public class MyLinkedList<T>
{
    public T? Value { get; set; }
    public MyLinkedList<T>? Next { get; set; }
}

[GenerateShape]
internal partial class RecursiveClassWithNonNullableOccurrence
{
    public required RecursiveClassWithNonNullableOccurrence Value { get; init; }
}

[GenerateShape]
internal partial class RecursiveClassWithNonNullableOccurrences
{
    public required RecursiveClassWithNonNullableOccurrences[] Values { get; init; }
}

[GenerateShape]
public partial struct ComplexStruct
{
    public double real;
    public double im;
}

[GenerateShape]
public partial struct ComplexStructWithProperties
{
    public double Real { get; set; }
    public double Im { get; set; }
}

[GenerateShape]
public partial struct StructWithDefaultCtor
{
    public int Value;
    public StructWithDefaultCtor()
    {
        Value = 42;
    }
}

[GenerateShape]
public partial class BaseClass : IEquatable<BaseClass>
{
    public int X { get; set; }

    public bool Equals(BaseClass? other) => other is not null && X == other.X;

    public override bool Equals(object? obj) => obj is BaseClass other && Equals(other);

    public override int GetHashCode() => X;
}

[GenerateShape]
public partial class DerivedClass : BaseClass, IEquatable<DerivedClass>
{
    public int Y { get; set; }

    public bool Equals(DerivedClass? other) => base.Equals(other) && Y == other.Y;

    public override bool Equals(object? obj) => obj is DerivedClass other && Equals(other);

    public override int GetHashCode() => (base.GetHashCode(), Y).GetHashCode();
}

[GenerateShape]
public abstract partial class BaseClassWithVirtualProperties
{
    public virtual int X { get; set; }
    public abstract string Y { get; set; }
    public virtual int Z { get; set; }
    public virtual int W { get; set; }
}

[GenerateShape]
public partial class DerivedClassWithVirtualProperties : BaseClassWithVirtualProperties
{
    private int? _x;
    private string? _y;

    public override int X
    {
        get => _x ?? 42;
        set
        {
            if (_x != null)
            {
                throw new InvalidOperationException("Value has already been set once");
            }

            _x = value;
        }
    }

    public override string Y
    {
        get => _y ?? "str";
        set
        {
            if (_y != null)
            {
                throw new InvalidOperationException("Value has already been set once");
            }

            _y = value;
        }
    }

    public override int Z => 42;
    public override int W { set => base.W = value; }
}

[GenerateShape]
public partial interface IBaseInterface
{
    public int X { get; set; }
}

[GenerateShape]
public partial interface IDerivedInterface : IBaseInterface
{
    public int Y { get; set; }
}

[GenerateShape]
public partial interface IDerived2Interface : IBaseInterface
{
    public int Z { get; set; }
}

[GenerateShape]
public partial interface IDerived3Interface : IBaseInterface
{
    public int W { get; set; }
}

[GenerateShape]
public partial interface IDiamondInterface : IDerivedInterface, IDerived2Interface, IDerived3Interface
{
    public int T { get; set; }
}

[GenerateShape]
public partial class DiamondImplementation : IDiamondInterface
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int W { get; set; }
    public int T { get; set; }
}

[GenerateShape]
public partial class ClassWithRequiredField
{
    public required int x;
}

[GenerateShape]
public partial struct StructWithRequiredField
{
    public required int x;
}

[GenerateShape]
public partial class ClassWithRequiredProperty
{
    public required int X { get; set; }
}

[GenerateShape]
public partial struct StructWithRequiredProperty
{
    public required int X { get; set; }
}

[GenerateShape]
public partial class ClassWithReadOnlyField
{
    public readonly int field = 42;
}

[GenerateShape]
public partial struct StructWithRequiredPropertyAndDefaultCtor
{
    public StructWithRequiredPropertyAndDefaultCtor() { }
    public required int y { get; set; }
}

[GenerateShape]
public partial struct StructWithRequiredFieldAndDefaultCtor
{
    public StructWithRequiredFieldAndDefaultCtor() { }
    public required int y;
}

[GenerateShape]
public partial class ClassWithRequiredAndInitOnlyProperties
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;

}

[GenerateShape]
public partial struct StructWithRequiredAndInitOnlyProperties
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;
}

[GenerateShape]
public partial class ClassWithSetsRequiredMembersCtor
{
    private int _value;

    [SetsRequiredMembers]
    public ClassWithSetsRequiredMembersCtor(int value)
    {
        _value = value;
    }

    public required int Value
    {
        get => _value;
        init => throw new NotSupportedException();
    }
}

[GenerateShape]
public partial struct StructWithSetsRequiredMembersCtor
{
    private int _value;

    [SetsRequiredMembers]
    public StructWithSetsRequiredMembersCtor(int value)
    {
        _value = value;
    }

    public required int Value
    {
        get => _value;
        init => _value = -1;
    }
}

[GenerateShape]
public partial class ClassWithSetsRequiredMembersDefaultCtor
{
    [SetsRequiredMembers]
    public ClassWithSetsRequiredMembersDefaultCtor() { }

    public required int Value { get; set; }
}

[GenerateShape]
public partial struct StructWithSetsRequiredMembersDefaultCtor
{
    [SetsRequiredMembers]
    public StructWithSetsRequiredMembersDefaultCtor() { }

    public required int Value { get; set; }
}

[GenerateShape]
readonly partial struct StructWithInitOnlyProperties
{
    public int IntProp { get; init; }
    public string StringProp { get; init; }
}

public readonly struct GenericStructWithInitOnlyProperty<T>
{
    public T Value { get; init; }
}

[GenerateShape]
public partial class ClassWithInitOnlyProperties
{
    public int Value { get; init; } = 42;
    public List<int> Values { get; init; } = [42];
}

[GenerateShape]
public partial class ClassWithIndexer
{
    public string this[int i]
    {
        get => i.ToString(CultureInfo.InvariantCulture);
        set { }
    }
}

[GenerateShape]
public partial record ClassRecordWithRequiredAndInitOnlyProperties(int x, int y, int z)
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;
}

[GenerateShape]
public partial record struct StructRecordWithRequiredAndInitOnlyProperties(int x, int y, int z)
{
    public required string RequiredAndInitOnlyString { get; init; }
    public required string RequiredString { get; set; }
    public string? InitOnlyString { get; init; }

    public required int RequiredAndInitOnlyInt { get; init; }
    public required int RequiredInt { get; set; }
    public int InitOnlyInt { get; init; }

    public required bool requiredField;
}

[GenerateShape]
public partial class ClassWithDefaultConstructorAndSingleRequiredProperty
{
    public required int Value { get; set; }
}

[GenerateShape]
public partial class ClassWithParameterizedConstructorAnd2OptionalSetters(int x1)
{
    public int X1 { get; set; } = x1;
    public int X2 { get; set; }
}

[GenerateShape]
public partial class ClassWithParameterizedConstructorAnd10OptionalSetters(int x01)
{
    public int X01 { get; set; } = x01;
    public int X02 { get; set; } = x01;
    public int X03 { get; set; } = x01;
    public int X04 { get; set; } = x01;
    public int X05 { get; set; } = x01;
    public int X06 { get; set; } = x01;
    public int X07 { get; set; } = x01;
    public int X08 { get; set; } = x01;
    public int X09 { get; set; } = x01;
    public int X10 { get; set; } = x01;
}

[GenerateShape]
public partial class ClassWithParameterizedConstructorAnd70Setters(int x01)
{
    public int X01 { get; set; } = x01;
    public int X02 { get; set; } = x01;
    public required int X03 { get; set; } = x01;
    public int X04 { get; set; } = x01;
    public int X05 { get; set; } = x01;
    public int X06 { get; set; } = x01;
    public int X07 { get; set; } = x01;
    public int X08 { get; set; } = x01;
    public int X09 { get; set; } = x01;
    public required int X10 { get; set; } = x01;
    public int X11 { get; set; } = x01;
    public int X12 { get; set; } = x01;
    public int X13 { get; set; } = x01;
    public int X14 { get; set; } = x01;
    public int X15 { get; set; } = x01;
    public int X16 { get; set; } = x01;
    public int X17 { get; set; } = x01;
    public int X18 { get; set; } = x01;
    public int X19 { get; set; } = x01;
    public int X20 { get; set; } = x01;
    public int X21 { get; set; } = x01;
    public int X22 { get; set; } = x01;
    public int X23 { get; set; } = x01;
    public int X24 { get; set; } = x01;
    public int X25 { get; set; } = x01;
    public int X26 { get; set; } = x01;
    public int X27 { get; set; } = x01;
    public int X28 { get; set; } = x01;
    public int X29 { get; set; } = x01;
    public int X30 { get; set; } = x01;
    public int X31 { get; set; } = x01;
    public int X32 { get; set; } = x01;
    public int X33 { get; set; } = x01;
    public int X34 { get; set; } = x01;
    public int X35 { get; set; } = x01;
    public int X36 { get; set; } = x01;
    public int X37 { get; set; } = x01;
    public int X38 { get; set; } = x01;
    public int X39 { get; set; } = x01;
    public int X40 { get; set; } = x01;
    public int X41 { get; set; } = x01;
    public int X42 { get; set; } = x01;
    public int X43 { get; set; } = x01;
    public int X44 { get; set; } = x01;
    public int X45 { get; set; } = x01;
    public int X46 { get; set; } = x01;
    public required int X47 { get; set; } = x01;
    public int X48 { get; set; } = x01;
    public int X49 { get; set; } = x01;
    public int X50 { get; set; } = x01;
    public int X51 { get; set; } = x01;
    public int X52 { get; set; } = x01;
    public int X53 { get; set; } = x01;
    public int X54 { get; set; } = x01;
    public int X55 { get; set; } = x01;
    public int X56 { get; set; } = x01;
    public int X57 { get; set; } = x01;
    public int X58 { get; set; } = x01;
    public int X59 { get; set; } = x01;
    public int X60 { get; set; } = x01;
    public int X61 { get; set; } = x01;
    public int X62 { get; set; } = x01;
    public int X63 { get; set; } = x01;
    public int X64 { get; set; } = x01;
    public int X65 { get; set; } = x01;
    public int X66 { get; set; } = x01;
    public int X67 { get; set; } = x01;
    public int X68 { get; set; } = x01;
    public int X69 { get; set; } = x01;
    public int X70 { get; set; } = x01;
}

public class GenericContainer<T>
{
    public class Inner
    {
        public T? Value { get; set; }
    }

    public class Inner<U>
    {
        public T? Value1 { get; set; }
        public U? Value2 { get; set; }
    }
}

[GenerateShape]
public partial class ClassWithNullabilityAttributes
{
    private string? _maybeNull = "str";
    private string? _allowNull = "str";
    private string? _notNull = "str";
    private string? _disallowNull = "str";

    public ClassWithNullabilityAttributes() { }

    public ClassWithNullabilityAttributes([AllowNull] string allowNull, [DisallowNull] string? disallowNull)
    {
        _allowNull = allowNull;
        _disallowNull = disallowNull;
    }

    [MaybeNull]
    public string MaybeNull
    {
        get => _maybeNull;
        set => _maybeNull = value;
    }

    [AllowNull]
    public string AllowNull
    {
        get => _allowNull ?? "str";
        set => _allowNull = value;
    }

    [NotNull]
    public string? NotNull
    {
        get => _notNull ?? "str";
        set => _notNull = value;
    }

    [DisallowNull]
    public string? DisallowNull
    {
        get => _disallowNull;
        set => _disallowNull = value;
    }

    [MaybeNull]
    public string MaybeNullField = "str";
    [AllowNull]
    public string AllowNullField = "str";
    [NotNull]
    public string? NotNullField = "str";
    [DisallowNull]
    public string? DisallowNullField = "str";
}

public class ClassWithNullabilityAttributes<T>
{
    [NotNull]
    public T? NotNullProperty { get; set; }

    [DisallowNull]
    public T? DisallowNullProperty { get; set; }

    [NotNull]
    public required T NotNullField;

    [DisallowNull]
    public T? DisallowNullField;
}

public class ClassWithNotNullProperty<T> where T : notnull
{
    public required T Property { get; set; }
}

[GenerateShape]
public partial struct StructWithNullabilityAttributes
{
    private int? _maybeNull = 0;
    private int? _allowNull = 0;
    private int? _notNull = 0;
    private int? _disallowNull = 0;

    public StructWithNullabilityAttributes() { }

    public StructWithNullabilityAttributes([AllowNull] int? allowNull, [DisallowNull] int? disallowNull)
    {
        _allowNull = allowNull;
        _disallowNull = disallowNull;
    }

    [MaybeNull]
    public int? MaybeNull
    {
        get => _maybeNull;
        set => _maybeNull = value;
    }

    [AllowNull]
    public int? AllowNull
    {
        get => _allowNull ?? 0;
        set => _allowNull = value;
    }

    [NotNull]
    public int? NotNullProperty
    {
        get => _notNull ?? 0;
        set => _notNull = value;
    }

    [DisallowNull]
    public int? DisallowNull
    {
        get => _disallowNull;
        set => _disallowNull = value;
    }

    [MaybeNull]
    public int MaybeNullField = 0;
    [AllowNull]
    public int AllowNullField = 0;
    [NotNull]
    public int? NotNullField = 0;
    [DisallowNull]
    public int? DisallowNullField = 0;
}

[GenerateShape]
public partial class ClassWithInternalConstructor
{
    [JsonConstructor, ConstructorShape]
    internal ClassWithInternalConstructor(int value) => Value = value;

    public int Value { get; }
}

[GenerateShape]
public partial record ParameterlessRecord();
[GenerateShape]
public partial record struct ParameterlessStructRecord();
[GenerateShape]
public partial record SimpleRecord(int value);
[GenerateShape]
public partial record NonNullStringRecord(string value);
[GenerateShape]
public partial record NullableStringRecord(string? value);
public record GenericRecord<T>(T value);
public readonly record struct GenericRecordStruct<T>(T value);
public record NotNullGenericRecord<T>(T value) where T : notnull;
public record NotNullClassGenericRecord<T>(T value) where T : class;
public record NullClassGenericRecord<T>(T value) where T : class?;
#nullable disable
public record NullObliviousGenericRecord<T>(T value);
#nullable restore

[GenerateShape]
public partial record ClassRecord(int x, int? y, int z, int w);
[GenerateShape]
public partial record struct StructRecord(int x, int y, int z, int w);

[GenerateShape]
public partial record RecordWithDefaultParams(bool x1 = true, byte x2 = 10, sbyte x3 = 10, char x4 = 'x', ushort x5 = 10, short x6 = 10, long x7 = 10);

[GenerateShape]
public partial record RecordWithDefaultParams2(ulong x1 = 10, float x2 = 3.1f, double x3 = 3.1d, decimal x4 = -3.1415926m, string x5 = "str", string? x6 = null, object? x7 = null);

[GenerateShape]
public partial record RecordWithNullableDefaultParams(bool? x1 = true, byte? x2 = 10, sbyte? x3 = 10, char? x4 = 'x', ushort? x5 = 10, short? x6 = 10, long? x7 = 10);

[GenerateShape]
public partial record RecordWithNullableDefaultParams2(ulong? x1 = 10, float? x2 = 3.1f, double? x3 = 3.1d, decimal? x4 = -3.1415926m, string? x5 = "str", string? x6 = null, object? x7 = null);

[GenerateShape]
public partial record RecordWithSpecialValueDefaultParams(
    double d1 = double.PositiveInfinity, double d2 = double.NegativeInfinity, double d3 = double.NaN,
    double? dn1 = double.PositiveInfinity, double? dn2 = double.NegativeInfinity, double? dn3 = double.NaN,
    float f1 = float.PositiveInfinity, float f2 = float.NegativeInfinity, float f3 = float.NaN,
    float? fn1 = float.PositiveInfinity, float? fn2 = float.NegativeInfinity, float? fn3 = float.NaN,
    Guid g1 = default, Guid? g2 = default, StructWithDefaultCtor s1 = default, StructWithDefaultCtor? s2 = default,
    string s = "\"😀葛🀄\r\n🤯𐐀𐐨\"", char c = '\'');

[Flags]
public enum MyEnum { A = 1, B = 2, C = 4, D = 8, E = 16, F = 32, G = 64, H = 128 }

[GenerateShape]
public partial record RecordWithEnumAndNullableParams(MyEnum flags1, MyEnum? flags2, MyEnum flags3 = MyEnum.A, MyEnum? flags4 = null);

[GenerateShape]
public partial record RecordWithNullableDefaultEnum(MyEnum? flags = MyEnum.A | MyEnum.B);

[GenerateShape]
public partial record LargeClassRecord(
    int x0 = 0, int x1 = 1, int x2 = 2, int x3 = 3, int x4 = 4, int x5 = 5, int x6 = 5,
    int x7 = 7, int x8 = 8, string x9 = "str", LargeClassRecord? nested = null);

[GenerateShape]
public partial record RecordWith21ConstructorParameters(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21);

[GenerateShape]
public partial record RecordWith42ConstructorParameters(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21,
    string x22, int x23, bool x24, TimeSpan x25, DateTime x26, int x27, string x28,
    string x29, int x30, bool x31, TimeSpan x32, DateTime x33, int x34, string x35,
    string x36, int x37, bool x38, TimeSpan x39, DateTime x40, int x41, string x42);

[GenerateShape]
public partial record RecordWith42ConstructorParametersAndRequiredProperties(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21,
    string x22, int x23, bool x24, TimeSpan x25, DateTime x26, int x27, string x28,
    string x29, int x30, bool x31, TimeSpan x32, DateTime x33, int x34, string x35,
    string x36, int x37, bool x38, TimeSpan x39, DateTime x40, int x41, string x42)
{
    public required int requiredField;
    public required string RequiredProperty { get; set; }
}

[GenerateShape]
public partial record StructRecordWith42ConstructorParametersAndRequiredProperties(
    string x01, int x02, bool x03, TimeSpan x04, DateTime x05, int x06, string x07,
    string x08, int x09, bool x10, TimeSpan x11, DateTime x12, int x13, string x14,
    string x15, int x16, bool x17, TimeSpan x18, DateTime x19, int x20, string x21,
    string x22, int x23, bool x24, TimeSpan x25, DateTime x26, int x27, string x28,
    string x29, int x30, bool x31, TimeSpan x32, DateTime x33, int x34, string x35,
    string x36, int x37, bool x38, TimeSpan x39, DateTime x40, int x41, string x42)
{
    public required int requiredField;
    public required string RequiredProperty { get; set; }
}

[GenerateShape]
public partial struct ClassWith40RequiredMembers
{
    public required int r00; public required int r01; public required int r02; public required int r03; public required int r04; public required int r05; public required int r06; public required int r07; public required int r08; public required int r09;
    public required int r10; public required int r11; public required int r12; public required int r13; public required int r14; public required int r15; public required int r16; public required int r17; public required int r18; public required int r19;
    public required int r20; public required int r21; public required int r22; public required int r23; public required int r24; public required int r25; public required int r26; public required int r27; public required int r28; public required int r29;
    public required int r30; public required int r31; public required int r32; public required int r33; public required int r34; public required int r35; public required int r36; public required int r37; public required int r38; public required int r39;
}

[GenerateShape]
public partial struct StructWith40RequiredMembers
{
    public required int r00; public required int r01; public required int r02; public required int r03; public required int r04; public required int r05; public required int r06; public required int r07; public required int r08; public required int r09;
    public required int r10; public required int r11; public required int r12; public required int r13; public required int r14; public required int r15; public required int r16; public required int r17; public required int r18; public required int r19;
    public required int r20; public required int r21; public required int r22; public required int r23; public required int r24; public required int r25; public required int r26; public required int r27; public required int r28; public required int r29;
    public required int r30; public required int r31; public required int r32; public required int r33; public required int r34; public required int r35; public required int r36; public required int r37; public required int r38; public required int r39;
}

[GenerateShape]
public partial struct StructWith40RequiredMembersAndDefaultCtor
{
    public StructWith40RequiredMembersAndDefaultCtor() { }
    public required int r00; public required int r01; public required int r02; public required int r03; public required int r04; public required int r05; public required int r06; public required int r07; public required int r08; public required int r09;
    public required int r10; public required int r11; public required int r12; public required int r13; public required int r14; public required int r15; public required int r16; public required int r17; public required int r18; public required int r19;
    public required int r20; public required int r21; public required int r22; public required int r23; public required int r24; public required int r25; public required int r26; public required int r27; public required int r28; public required int r29;
    public required int r30; public required int r31; public required int r32; public required int r33; public required int r34; public required int r35; public required int r36; public required int r37; public required int r38; public required int r39;
}

[GenerateShape]
public partial class ClassWithInternalMembers
{
    public int X { get; set; }

    [PropertyShape(Ignore = false), JsonInclude]
    internal int Y { get; set; }
    [PropertyShape, JsonInclude]
    public int Z { internal get; set; }
    [PropertyShape, JsonInclude]
    public int W { get; internal set; }

    [PropertyShape, JsonInclude]
    internal int internalField;
}

[GenerateShape]
public partial class ClassWithPropertyAnnotations
{
    [PropertyShape(Name = "AltName", Order = 5)]
    [JsonPropertyName("AltName"), JsonPropertyOrder(5)]
    public int X { get; set; }

    [PropertyShape(Name = "AltName2", Order = -1)]
    [JsonPropertyName("AltName2"), JsonPropertyOrder(-1)]
    public int Y;

    [PropertyShape(Name = "Name\t\f\b with\r\nescaping\'\"", Order = 2)]
    [JsonPropertyName("Name\t\f\b with\r\nescaping\'\""), JsonPropertyOrder(2)]
    public bool Z;
}

[GenerateShape]
public partial class ClassWithConstructorAndAnnotations
{
    public ClassWithConstructorAndAnnotations(int x, [ParameterShape(Name = "AltName2")] int y, bool z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    [PropertyShape(Name = "AltName", Order = 5)]
    [JsonPropertyName("AltName"), JsonPropertyOrder(5)]
    public int X { get; }

    [PropertyShape(Name = "AltName2", Order = -1)]
    [JsonPropertyName("AltName2"), JsonPropertyOrder(-1)]
    public int Y { get; }

    [PropertyShape(Name = "Name\twith\r\nescaping", Order = 2)]
    [JsonPropertyName("Name\twith\r\nescaping"), JsonPropertyOrder(2)]
    public bool Z { get; }
}

[GenerateShape]
public abstract partial class BaseClassWithPropertyShapeAnnotations
{
    // JsonIgnore added because of a bug in the STJ baseline
    // cf. https://github.com/dotnet/runtime/issues/92780

    [PropertyShape(Name = "BaseX")]
    [JsonIgnore]
    public abstract int X { get; }

    [PropertyShape(Name = "BaseY")]
    [JsonIgnore]
    public virtual int Y { get; }

    [PropertyShape(Name = "BaseZ")]
    [JsonIgnore]
    public int Z { get; }
}

[GenerateShape]
public partial class DerivedClassWithPropertyShapeAnnotations : BaseClassWithPropertyShapeAnnotations
{
    [PropertyShape(Name = "DerivedX")]
    [JsonPropertyName("DerivedX")] // Expected name
    public override int X => 1;

    [JsonPropertyName("BaseY")] // Expected name
    public override int Y => 2;

    [PropertyShape(Name = "DerivedZ")]
    [JsonPropertyName("DerivedZ")] // Expected name
    public new int Z { get; } = 3;
}

[GenerateShape]
public partial class PersonClass(string name, int age)
{
    public string Name { get; } = name;
    public int Age { get; } = age;
}

[GenerateShape]
public partial struct PersonStruct(string name, int age)
{
    public string Name { get; } = name;
    public int Age { get; } = age;
}

[GenerateShape]
public partial interface IPersonInterface
{
    public string Name { get; }
    public int Age { get; }

    public class Impl(string name, int age) : IPersonInterface
    {
        public string Name { get; } = name;
        public int Age { get; } = age;
    }
}

[GenerateShape]
public abstract partial class PersonAbstractClass(string name, int age)
{
    public string Name { get; } = name;
    public int Age { get; } = age;

    public class Impl(string name, int age) : PersonAbstractClass(name, age);
}

[GenerateShape]
public partial record PersonRecord(string name, int age);

[GenerateShape]
public partial record struct PersonRecordStruct(string name, int age);

[GenerateShape]
[CollectionBuilder(typeof(CollectionWithBuilderAttribute), nameof(Create))]
public partial class CollectionWithBuilderAttribute : List<int>
{
    private CollectionWithBuilderAttribute() { }

    public static CollectionWithBuilderAttribute Create(ReadOnlySpan<int> values)
    {
        var result = new CollectionWithBuilderAttribute();
        foreach (var value in values)
        {
            result.Add(value);
        }
        return result;
    }
}

[CollectionBuilder(typeof(GenericCollectionWithBuilderAttribute), nameof(GenericCollectionWithBuilderAttribute.Create))]
public partial class GenericCollectionWithBuilderAttribute<T> : List<T>
{
    private GenericCollectionWithBuilderAttribute() { }

    public static GenericCollectionWithBuilderAttribute<T> CreateEmpty() => new();
}

public static class GenericCollectionWithBuilderAttribute
{
    public static GenericCollectionWithBuilderAttribute<T> Create<T>(ReadOnlySpan<T> values)
    {
        var result = GenericCollectionWithBuilderAttribute<T>.CreateEmpty();
        foreach (var value in values)
        {
            result.Add(value);
        }
        return result;
    }
}

[GenerateShape]
public partial class CollectionWithEnumerableCtor : List<int>
{
    public CollectionWithEnumerableCtor(IEnumerable<int> values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }
}

[GenerateShape]
public partial class DictionaryWithEnumerableCtor : Dictionary<string, int>
{
    public DictionaryWithEnumerableCtor(IEnumerable<KeyValuePair<string, int>> values)
    {
        foreach (var value in values)
        {
            this[value.Key] = value.Value;
        }
    }
}

[GenerateShape]
public partial class CollectionWithSpanCtor : List<int>
{
    public CollectionWithSpanCtor(ReadOnlySpan<int> values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }
}

[GenerateShape]
public partial class DictionaryWithSpanCtor : Dictionary<string, int>
{
    public DictionaryWithSpanCtor(ReadOnlySpan<KeyValuePair<string, int>> values)
    {
        foreach (var value in values)
        {
            this[value.Key] = value.Value;
        }
    }
}

public class MyKeyedCollection<T> : KeyedCollection<int, T>
{
    private int _count;
    protected override int GetKeyForItem(T key) => _count++;
}

[GenerateShape]
public partial record Todos(Todo[] Items);

[GenerateShape]
public partial record Todo(int Id, string? Title, DateTimeOffset? DueBy, Status Status);

public enum Status { NotStarted, InProgress, Done }

[GenerateShape]
public partial class WeatherForecastDTO
{
    public required string Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public int TemperatureCelsius { get; set; }
    public string? Summary { get; set; }
    public string? SummaryField;
    public List<DateTimeOffset>? DatesAvailable { get; set; }
    public Dictionary<string, HighLowTempsDTO>? TemperatureRanges { get; set; }
    public string[]? SummaryWords { get; set; }
}

public class HighLowTempsDTO
{
    public int High { get; set; }
    public int Low { get; set; }
}

[GenerateShape]
public partial class WeatherForecast
{
    public DateTimeOffset Date { get; init; }
    public int TemperatureCelsius { get; init; }
    public IReadOnlyList<DateTimeOffset>? DatesAvailable { get; init; }
    public IReadOnlyDictionary<string, HighLowTemps>? TemperatureRanges { get; init; }
    public IReadOnlyList<string>? SummaryWords { get; init; }
    public string? UnmatchedProperty { get; init; }
}

public record HighLowTemps
{
    public int High { get; init; }
}

[GenerateShape]
public partial record BaseClassWithShadowingMembers
{
    public string? PropA { get; init; }
    public string? PropB { get; init; }
    public int FieldA;
    public int FieldB;
}

[GenerateShape]
public partial record DerivedClassWithShadowingMember : BaseClassWithShadowingMembers
{
    public new string? PropA { get; init; }
    public new required int PropB { get; init; }
    public new int FieldA;
    public new required string FieldB;
}

[GenerateShape]
public partial class ClassWithMultipleSelfReferences
{
    public long Id { get; set; }
    public ClassWithMultipleSelfReferences? First { get; set; }
    public ClassWithMultipleSelfReferences[] FirstArray { get; set; } = [];
}

[GenerateShape]
public partial class ClassWithNullableTypeParameters
{
    public string?[] DataArray { get; set; } = [null, "str"];
    public List<string?> DataList { get; set; } = [null, "str"];
    public List<string?> InitOnlyDataList { get; init; } = [null, "str"];
    public Dictionary<int, string?[]> DataDict { get; set; } = new() { [0] = [null, "str"] };
}

public class ClassWithNullableTypeParameters<T>
{
    public T?[] DataArray { get; set; } = [default];
    public List<T?> DataList { get; set; } = [default];
    public List<T?> InitOnlyDataList { get; init; } = [default];
    public Dictionary<int, T?[]> DataDict { get; set; } = new() { [0] = [default] };
}

public class CollectionWithNullableElement<T>(IEnumerable<(T?, int)> values) : IEnumerable<(T?, int)>
{
    private readonly (T?, int)[] _values = values.ToArray();
    public IEnumerator<(T?, int)> GetEnumerator() => ((IEnumerable<(T?, int)>)_values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
}

public class DictionaryWithNullableEntries<T>(IEnumerable<KeyValuePair<string, (T?, int)>> values) : IReadOnlyDictionary<string, (T?, int)>
{
    private readonly Dictionary<string, (T?, int)> _source = values.ToDictionary(e => e.Key, e => e.Value);
    public IEnumerator<KeyValuePair<string, (T?, int)>> GetEnumerator() => _source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _source.GetEnumerator();
    public int Count => _source.Count;
    public bool ContainsKey(string key) => _source.ContainsKey(key);
    public bool TryGetValue(string key, out (T?, int) value) => _source.TryGetValue(key, out value);
    public (T?, int) this[string key] => _source[key];
    public IEnumerable<string> Keys => _source.Keys;
    public IEnumerable<(T?, int)> Values => _source.Values;
}

public class ClassWithNullableProperty<T>
{
    public (int, T?)? Value { get; set; }
}

[GenerateShape]
partial class ClassWithMultipleConstructors
{
    public ClassWithMultipleConstructors(int x, int y)
    {
        X = x;
        Y = y;
    }

    [JsonConstructor]
    public ClassWithMultipleConstructors(int z)
    {
        // PolyType should automatically pick this ctor
        // as it maximizes the possible properties that get initialized.

        Z = z;
    }

    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; }
}

[GenerateShape]
public partial class ClassWithConflictingAnnotations
{
    public required GenericClass<string?> NullableString { get; set; }
    public required GenericClass<string> NonNullNullableString { get; set; }

    public class GenericClass<T>
    {
        public required T Value { get; set; }
    }
}

[GenerateShape]
public partial class ClassWithRefConstructorParameter(ref int value)
{
    public int Value { get; } = value;

    public static ClassWithRefConstructorParameter Create()
    {
        int value = 42;
        return new ClassWithRefConstructorParameter(ref value);
    }
}

[GenerateShape]
public partial class ClassWithOutConstructorParameter
{
    public ClassWithOutConstructorParameter(out int value)
    {
        Value = value = 42;
    }

    public int Value { get; }
}

[GenerateShape]
public partial class ClassWithMultipleRefConstructorParameters
{
    public ClassWithMultipleRefConstructorParameters(ref int intValue, in bool boolValue, ref readonly DateTime dateValue)
    {
        IntValue = intValue;
        BoolValue = boolValue;
        DateValue = dateValue;
    }

    public int IntValue { get; }
    public bool BoolValue { get; }
    public DateTime DateValue { get; }

    public static ClassWithMultipleRefConstructorParameters Create()
    {
        int intValue = 42;
        bool boolValue = true;
        DateTime dateValue = DateTime.MaxValue;
        return new ClassWithMultipleRefConstructorParameters(ref intValue, in boolValue, ref dateValue);
    }
}

[GenerateShape]
public partial class ClassWithRefConstructorParameterPrivate
{
    [JsonConstructor, ConstructorShape]
    private ClassWithRefConstructorParameterPrivate(ref int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static ClassWithRefConstructorParameterPrivate Create()
    {
        int value = 42;
        return new ClassWithRefConstructorParameterPrivate(ref value);
    }
}

[GenerateShape]
public partial class ClassWithMultipleRefConstructorParametersPrivate
{
    [JsonConstructor, ConstructorShape]
    private ClassWithMultipleRefConstructorParametersPrivate(ref int intValue, in bool boolValue, ref readonly DateTime dateValue)
    {
        IntValue = intValue;
        BoolValue = boolValue;
        DateValue = dateValue;
    }

    public int IntValue { get; }
    public bool BoolValue { get; }
    public DateTime DateValue { get; }

    public static ClassWithMultipleRefConstructorParametersPrivate Create()
    {
        int intValue = 42;
        bool boolValue = true;
        DateTime dateValue = DateTime.MaxValue;
        return new ClassWithMultipleRefConstructorParametersPrivate(ref intValue, in boolValue, ref dateValue);
    }
}

public partial class GenericClassWithPrivateConstructor<T>
{
    [JsonConstructor, ConstructorShape]
    private GenericClassWithPrivateConstructor(T value)
    {
        Value = value;
    }

    public T Value { get; }

    public static GenericClassWithPrivateConstructor<T> Create(T value)
    {
        return new(value);
    }
}

public partial class GenericClassWithPrivateField<T>
{
    [JsonInclude, PropertyShape]
    private T? Value;

    public static GenericClassWithPrivateField<T> Create(T value)
    {
        var obj = new GenericClassWithPrivateField<T> { Value = value };
        _ = obj.Value;
        return obj;
    }
}

public partial struct GenericStructWithPrivateField<T>
{
    [JsonInclude, PropertyShape]
    private T? Value;

    public static GenericStructWithPrivateField<T> Create(T value)
    {
        var obj = new GenericStructWithPrivateField<T> { Value = value };
        _ = obj.Value;
        return obj;
    }
}

public partial class GenericClassWithMultipleRefConstructorParametersPrivate<T>
{
    [JsonConstructor, ConstructorShape]
    private GenericClassWithMultipleRefConstructorParametersPrivate(ref T refValue, in T inValue, ref readonly T refReadOnlyValue)
    {
        RefValue = refValue;
        InValue = inValue;
        RefReadOnlyValue = refReadOnlyValue;
    }

    public T RefValue { get; }
    public T InValue { get; }
    public T RefReadOnlyValue { get; }

    public static GenericClassWithMultipleRefConstructorParametersPrivate<T> Create(T value)
    {
        return new(ref value, in value, ref value);
    }
}

[GenerateShape]
public partial class ClassWithUnsupportedPropertyTypes
{
    public Exception? Exception { get; }
    public Func<int, int>? Delegate { get; }
    public Type? Type { get; }
    public Task<int>? Task { get; }
    [JsonIgnore]
    public ReadOnlySpan<char> Span => "str".AsSpan();
}

[GenerateShape]
public partial class ClassWithIncludedPrivateMembers
{
    [SetsRequiredMembers]
    public ClassWithIncludedPrivateMembers() : this(1, 2, 3, 4)
    {
        RequiredProperty = 5;
        RequiredField = 6;
        OptionalProperty = 7;
        OptionalField = 8;
    }

    [JsonConstructor, ConstructorShape]
    private ClassWithIncludedPrivateMembers(int privateProperty, int privateField, int privateGetter, int privateSetter)
    {
        PrivateProperty = privateProperty;
        PrivateField = privateField;
        PrivateGetter = privateGetter;
        PrivateSetter = privateSetter;
        _ = OptionalField;
    }

    [JsonInclude, PropertyShape]
    private int PrivateProperty { get; set; }
    [JsonInclude, PropertyShape]
    private int PrivateField;
    [JsonInclude, PropertyShape]
    public int PrivateGetter { private get; set; }
    [JsonInclude, PropertyShape]
    public int PrivateSetter { get; private set; }

    public required int RequiredProperty { get; set; }
    public required int RequiredField;

    [JsonInclude, JsonPropertyOrder(1), PropertyShape(Order = 1)]
    private int? OptionalProperty { get; init; }
    [JsonInclude, JsonPropertyOrder(1), PropertyShape(Order = 1)]
    private int? OptionalField;
}

[GenerateShape]
public partial struct StructWithIncludedPrivateMembers
{
    [SetsRequiredMembers]
    public StructWithIncludedPrivateMembers() : this(1, 2, 3, 4)
    {
        RequiredProperty = 5;
        RequiredField = 6;
        OptionalProperty = 7;
        OptionalField = 8;
    }

    [JsonConstructor, ConstructorShape]
    private StructWithIncludedPrivateMembers(int privateProperty, int privateField, int privateGetter, int privateSetter)
    {
        PrivateProperty = privateProperty;
        PrivateField = privateField;
        PrivateGetter = privateGetter;
        PrivateSetter = privateSetter;
        _ = OptionalField;
    }

    [JsonInclude, PropertyShape]
    private int PrivateProperty { get; set; }
    [JsonInclude, PropertyShape]
    private int PrivateField;
    [JsonInclude, PropertyShape]
    public int PrivateGetter { private get; set; }
    [JsonInclude, PropertyShape]
    public int PrivateSetter { get; private set; }

    public required int RequiredProperty { get; set; }
    public required int RequiredField;

    [JsonInclude, JsonPropertyOrder(1), PropertyShape(Order = 1)]
    private int? OptionalProperty { get; init; }
    [JsonInclude, JsonPropertyOrder(1), PropertyShape(Order = 1)]
    private int? OptionalField;
}

struct GenericStructWithPrivateIncludedMembers<T>
{
    [JsonInclude, PropertyShape]
#pragma warning disable IDE0051 // Remove unused private members
    private T Property { get; set; }
    
    [JsonInclude, PropertyShape]
    private T Field;
#pragma warning restore IDE0051 // Remove unused private members

    public static GenericStructWithPrivateIncludedMembers<T> Create(T property, T field)
    {
        GenericStructWithPrivateIncludedMembers<T> value = new() { Property = property, Field = field };
        _ = value.Field;
        return value;
    }
}

// Repro for https://github.com/eiriktsarpalis/PolyType/issues/238
[GenerateShape]
public partial struct Vector3D
{
    public float X;
    public float Y;
    public float Z;

    public Vector3D(float value) => throw new NotSupportedException();

    [JsonConstructor] // PolyType should automatically pick this ctor
    public Vector3D(float x, float y, float z) => (X, Y, Z) = (x, y, z);
}

[GenerateShape]
public partial class ClassWithAmbiguousCtors1
{
    // PolyType should pick this constructor
    [JsonConstructor]
    public ClassWithAmbiguousCtors1(int x, int y, int z) => (X, Y, Z) = (x, y, z);

    public ClassWithAmbiguousCtors1(int x, int y, int z, int unmatched) => throw new NotSupportedException();

    public int X { get; }
    public int Y { get; }
    public int Z { get; }
}

[GenerateShape]
public partial class ClassWithAmbiguousCtors2
{
    // optional unmatched constructors take precedence over constructors with unmatched required parameters
    [JsonConstructor]
    public ClassWithAmbiguousCtors2(int x, int y, int z, string? unmatched = null) => (X, Y, Z) = (x, y, z);

    public ClassWithAmbiguousCtors2(int x, int y, int z, bool unmatched) => throw new NotSupportedException();

    public int X { get; }
    public int Y { get; }
    public int Z { get; }
}

[GenerateShape]
public partial class ClassWithAmbiguousCtors3
{
    // PolyType should pick this constructor
    [JsonConstructor]
    public ClassWithAmbiguousCtors3(int x, int y) => (X, Y) = (x, y);

    public ClassWithAmbiguousCtors3(int x, int y, int z, int unmatched) => throw new NotSupportedException();

    public int X { get; }
    public int Y { get; }
    public int Z { get; }
}

// A type using escaped keywords as its identifiers
[GenerateShape]
partial class @class
{
    public @class(string @string, int @__makeref, bool @yield)
    {
        this.@string = @string;
        this.@__makeref = @__makeref;
        this.yield = yield;
    }

    public string @string { get; set; }
    public int @__makeref { get; set; }
    public bool @yield { get; set; }
}

[GenerateShape, TypeShape(Marshaler = typeof(Marshaler))]
public partial record TypeWithStringSurrogate(string Value)
{
    public class Marshaler : IMarshaler<TypeWithStringSurrogate, string>
    {
        public string? Marshal(TypeWithStringSurrogate? value) => value?.Value;
        public TypeWithStringSurrogate? Unmarshal(string? surrogate) => surrogate is null ? null : new(surrogate);
    }
}

[GenerateShape, TypeShape(Marshaler = typeof(Marshaler))]
public partial class TypeWithRecordSurrogate(int value1, string value2)
{
    private readonly int _value1 = value1;
    private readonly string _value2 = value2;
    
    public record Surrogate(int Value1, string Value2);

    public sealed class Marshaler : IMarshaler<TypeWithRecordSurrogate, Surrogate>
    {
        public Surrogate? Marshal(TypeWithRecordSurrogate? value) =>
            value is null ? null : new(value._value1, value._value2);

        public TypeWithRecordSurrogate? Unmarshal(Surrogate? surrogate) =>
            surrogate is null ? null : new(surrogate.Value1, surrogate.Value2);
    }
}

[TypeShape(Marshaler = typeof(EnumMarshaler))]
public enum EnumWithRecordSurrogate { A, B, C }
public record EnumSurrogate(int Value);
public sealed class EnumMarshaler : IMarshaler<EnumWithRecordSurrogate, EnumSurrogate>
{
    public EnumSurrogate Marshal(EnumWithRecordSurrogate value) => new((int)value);
    public EnumWithRecordSurrogate Unmarshal(EnumSurrogate? surrogate) => (EnumWithRecordSurrogate)(surrogate?.Value ?? 0);
}

[TypeShape(Marshaler = typeof(GenericMarshaler<>))]
public record TypeWithGenericMarshaler<T>(T Value);

public sealed class GenericMarshaler<T> : IMarshaler<TypeWithGenericMarshaler<T>, T>
{
    public T? Marshal(TypeWithGenericMarshaler<T>? value) => value is null ? default : value.Value;
    public TypeWithGenericMarshaler<T>? Unmarshal(T? value) => EqualityComparer<T>.Default.Equals(value!, default!) ? null : new(value!);
}

[TypeShape(Marshaler = typeof(GenericDictionaryWithMarshaler<,>.Marshaler))]
public class GenericDictionaryWithMarshaler<TKey, TValue> : Dictionary<TKey, TValue>
    where TKey : notnull
{
    public sealed class Marshaler : IMarshaler<GenericDictionaryWithMarshaler<TKey, TValue>, KeyValuePair<TKey, TValue>[]>
    {
        public KeyValuePair<TKey, TValue>[]? Marshal(GenericDictionaryWithMarshaler<TKey, TValue>? value) =>
            value?.ToArray();

        public GenericDictionaryWithMarshaler<TKey, TValue>? Unmarshal(KeyValuePair<TKey, TValue>[]? surrogate)
        {
            if (surrogate is null)
            {
                return null;
            }

            GenericDictionaryWithMarshaler<TKey, TValue> result = new();
            foreach (var pair in surrogate)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }
    }
}

[GenerateShape, TypeShape(Kind = TypeShapeKind.Object)]
public partial class EnumerableAsObject : IEnumerable<int>
{
    public int Value { get; set; }

    public IEnumerator<int> GetEnumerator() => Enumerable.Repeat(Value, 10).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[GenerateShape, TypeShape(Kind = TypeShapeKind.Enumerable)]
public partial class DictionaryAsEnumerable : List<KeyValuePair<string, string>>, IReadOnlyDictionary<string, string>
{
    string IReadOnlyDictionary<string, string>.this[string key] => throw new NotImplementedException();
    IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => throw new NotImplementedException();
    IEnumerable<string> IReadOnlyDictionary<string, string>.Values => throw new NotImplementedException();
    bool IReadOnlyDictionary<string, string>.ContainsKey(string key) => throw new NotImplementedException();
    bool IReadOnlyDictionary<string, string>.TryGetValue(string key, out string value) => throw new NotImplementedException();
}

[GenerateShape, TypeShape(Kind = TypeShapeKind.None)]
public partial class ObjectAsNone
{
    public required string Name { get; init; }
    public required int Age { get; init; }
}

[GenerateShape]
[DerivedTypeShape(typeof(PolymorphicClass))]
[DerivedTypeShape(typeof(DerivedClass))]
[DerivedTypeShape(typeof(DerivedEnumerable))]
[DerivedTypeShape(typeof(DerivedDictionary))]
[JsonDerivedType(typeof(PolymorphicClass), nameof(PolymorphicClass))]
[JsonDerivedType(typeof(DerivedClass), nameof(DerivedClass))]
[JsonDerivedType(typeof(DerivedEnumerable), nameof(DerivedEnumerable))]
[JsonDerivedType(typeof(DerivedDictionary), nameof(DerivedDictionary))]
public partial record PolymorphicClass(int Int)
{
    public record DerivedClass(int Int, string String) : PolymorphicClass(Int);

    public record DerivedEnumerable() : PolymorphicClass(0), IList<int>
    {
        private readonly List<int> _list = [];
        public void Add(int item) => _list.Add(item);
        int ICollection<int>.Count => _list.Count;
        bool ICollection<int>.IsReadOnly => false;
        public IEnumerator<int> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
        int IList<int>.this[int index] { get => _list[index]; set => _list[index] = value; }
        void ICollection<int>.Clear() => _list.Clear();
        bool ICollection<int>.Contains(int item) => _list.Contains(item);
        void ICollection<int>.CopyTo(int[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
        int IList<int>.IndexOf(int item) => _list.IndexOf(item);
        void IList<int>.Insert(int index, int item) => _list.Insert(index, item);
        bool ICollection<int>.Remove(int item) => _list.Remove(item);
        void IList<int>.RemoveAt(int index) => _list.RemoveAt(index);
    }

    public record DerivedDictionary() : PolymorphicClass(42), IDictionary<string, int>
    {
        private readonly Dictionary<string, int> _dict = new();
        public int this[string key] { get => _dict[key]; set => _dict[key] = value; }
        ICollection<string> IDictionary<string, int>.Keys => _dict.Keys;
        ICollection<int> IDictionary<string, int>.Values => _dict.Values;
        int ICollection<KeyValuePair<string, int>>.Count => _dict.Count;
        bool ICollection<KeyValuePair<string, int>>.IsReadOnly => false;
        void IDictionary<string, int>.Add(string key, int value) => _dict.Add(key, value);
        void ICollection<KeyValuePair<string, int>>.Add(KeyValuePair<string, int> item) => _dict.Add(item.Key, item.Value);
        void ICollection<KeyValuePair<string, int>>.Clear() => _dict.Clear();
        bool ICollection<KeyValuePair<string, int>>.Contains(KeyValuePair<string, int> item) => _dict.ContainsKey(item.Key);
        bool IDictionary<string, int>.ContainsKey(string key) => _dict.ContainsKey(key);
        IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator() => _dict.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _dict.GetEnumerator();
        bool IDictionary<string, int>.Remove(string key) => _dict.Remove(key);
        bool ICollection<KeyValuePair<string, int>>.Remove(KeyValuePair<string, int> item) => _dict.Remove(item.Key);
        bool IDictionary<string, int>.TryGetValue(string key, out int value) => _dict.TryGetValue(key, out value);
        void ICollection<KeyValuePair<string, int>>.CopyTo(KeyValuePair<string, int>[] array, int arrayIndex) => 
            ((ICollection<KeyValuePair<string, int>>)_dict).CopyTo(array, arrayIndex);
    }
}


[GenerateShape]
[DerivedTypeShape(typeof(IDerived1), Name = "derived1")]
[DerivedTypeShape(typeof(IDerived2), Name = "derived2")]
[DerivedTypeShape(typeof(IDerived1.IDerivedDerived1), Name = "derivedderived1")]
[DerivedTypeShape(typeof(IDerived2.IDerivedDerived2), Name = "derivedderived2")]
[DerivedTypeShape(typeof(IDiamond), Name = "diamond")]
public partial interface IPolymorphicInterface
{
    int X { get; set; }

    public class Derived : IPolymorphicInterface
    {
        public int X { get; set; }
    }

    public interface IDerived1 : IPolymorphicInterface
    {
        int Y { get; set; }

        public class Impl : IDerived1
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public interface IDerivedDerived1 : IDerived1
        {
            int Z { get; set; }

            public class Impl2 : IDerivedDerived1
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
            }
        }
    }

    public interface IDerived2 : IPolymorphicInterface
    {
        int Z { get; set; }

        public class Impl : IDerived2
        {
            public int X { get; set; }
            public int Z { get; set; }
        }

        public interface IDerivedDerived2 : IDerived2
        {
            int Y { get; set; }
            public class Impl2 : IDerivedDerived2
            {
                public int X { get; set; }
                public int Z { get; set; }
                public int Y { get; set; }
            }
        }
    }

    public interface IDiamond : IDerived1, IDerived2
    {
        public class Impl2 : IDiamond
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
        }
    }
}

[GenerateShape]
[DerivedTypeShape(typeof(Derived<int>), Name = "DerivedInt")]
[DerivedTypeShape(typeof(Derived<string>), Name = "DerivedString")]
[JsonDerivedType(typeof(Derived<int>), "DerivedInt")]
[JsonDerivedType(typeof(Derived<string>), "DerivedString")]
public partial record PolymorphicClassWithGenericDerivedType
{
    public record Derived<T>(T Value) : PolymorphicClassWithGenericDerivedType;
}

[GenerateShape]
[DerivedTypeShape(typeof(Leaf), Name = "leaf", Tag = 10)]
[DerivedTypeShape(typeof(Node), Name = "node", Tag = 1000)]
[JsonDerivedType(typeof(Leaf), "leaf")]
[JsonDerivedType(typeof(Node), "node")]
public partial record Tree
{
    public record Leaf : Tree;
    public record Node(int Value, Tree Left, Tree Right) : Tree;
}

[DerivedTypeShape(typeof(GenericTree<>.Leaf), Name = "leaf", Tag = 10)]
[DerivedTypeShape(typeof(GenericTree<>.Node), Name = "node", Tag = 1000)]
public partial record GenericTree<T>
{
    public record Leaf : GenericTree<T>;
    public record Node(T Value, GenericTree<T> Left, GenericTree<T> Right) : GenericTree<T>;
}

[GenerateShape]
public partial record PropertyRequiredByAttribute
{
    [PropertyShape(IsRequired = true)]
    public bool AttributeRequiredProperty { get; set; }
}

[GenerateShape]
public partial record PropertyNotRequiredByAttribute
{
    [PropertyShape(IsRequired = false)]
    public required bool AttributeNotRequiredProperty { get; set; }
}

[GenerateShape]
public partial record CtorParameterRequiredByAttribute
{
    public CtorParameterRequiredByAttribute([ParameterShape(IsRequired = true)] bool p = false)
    {
        P = p;
    }

    public bool P { get; }
}

[GenerateShape]
public partial record CtorParameterNotRequiredByAttribute
{
    public CtorParameterNotRequiredByAttribute([ParameterShape(IsRequired = false)] bool p)
    {
        P = p;
    }

    public bool P { get; }
}

[GenerateShape]
public partial class AsyncEnumerableClass(IEnumerable<int> values) : IAsyncEnumerable<int>
{
    public IEnumerable<int> Values { get; } = values;

    async IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        await Task.Yield();
        foreach (int value in Values)
        {
            yield return value;
        }
    }
}

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicStatic | MethodShapeFlags.PublicInstance)]
public partial class BaseClassWithMethodShapes
{
    public int BaseMethod(int x, int y) => x + y;
}

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
public partial class ClassWithMethodShapes : BaseClassWithMethodShapes, InterfaceWithMethodShapes, IDisposable
{
    public static ThreadLocal<StrongBox<int>> LastVoidResultBox { get; } = new(static () => new());

    public int UnusedProperty { get; set; }

    public int SyncInstanceMethod(int x, int y)
    {
        return x + y;
    }

    public static int SyncStaticMethod(int x, int y)
    {
        return x + y;
    }

    public async Task<int> AsyncInstanceMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    public static async Task<int> AsyncStaticMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    public async ValueTask<int> ValueTaskInstanceMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    public static async ValueTask<int> ValueTaskStaticMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape(Name = "custom method name")]
    public void InstanceVoidMethod(int x, int y)
    {
        LastVoidResultBox.Value!.Value = x + y;
    }

    public void StaticVoidMethod(int x, [ParameterShape(IsRequired = true)] int y = -1)
    {
        LastVoidResultBox.Value!.Value = x + y;
    }

    public async Task InstanceTaskMethod(int x, [ParameterShape(Name = "y")] int z)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + z;
    }

    public static async Task StaticTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    public async ValueTask InstanceValueTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    public static async ValueTask StaticValueTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape]
    private ref int PrivateInstanceMethod(ref int x, in int y)
    {
        x += y;
        return ref x;
    }

    [MethodShape]
    private static ref readonly int PrivateStaticMethod(ref int x, ref readonly int y)
    {
        x += y;
        return ref x;
    }

    [MethodShape]
    private async Task<int> PrivateTaskMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape]
    private static async Task<int> PrivateStaticTaskMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape]
    private async ValueTask<int> PrivateValueTaskMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape]
    private static async ValueTask<int> PrivateStaticValueTaskMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape]
    private void PrivateInstanceVoidMethod(int x, int y)
    {
        LastVoidResultBox.Value!.Value = x + y;
    }

    [MethodShape]
    private static void PrivateStaticVoidMethod(int x, int y)
    {
        LastVoidResultBox.Value!.Value = x + y;
    }

    [MethodShape]
    private async Task PrivateInstanceTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape]
    private static async Task PrivateStaticVoidTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape]
    private async ValueTask PrivateInstanceVoidValueTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape]
    private static async ValueTask PrivateStaticVoidValueTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape(Ignore = true)]
    public void MethodWithOutParameterType(out int x) { x = 42; }

    [MethodShape(Ignore = true)]
    public int MethodWithInvalidParameterType(ReadOnlySpan<byte> values) => values.Length;

    [MethodShape(Ignore = true)]
    public ReadOnlySpan<byte> MethodWithInvalidReturnType() => [1,2,3];

    void IDisposable.Dispose() => GC.SuppressFinalize(this);
}

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance | MethodShapeFlags.PublicStatic)]
public partial struct StructWithMethodShapes : InterfaceWithMethodShapes, IDisposable
{
    public static ThreadLocal<StrongBox<int>> LastVoidResultBox { get; } = new(static () => new());

    public int UnusedProperty { get; set; }

    public int BaseMethod(int x, int y) => x + y;

    public int SyncInstanceMethod(int x, int y)
    {
        return x + y;
    }

    public static int SyncStaticMethod(int x, int y)
    {
        return x + y;
    }

    public async Task<int> AsyncInstanceMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    public static async Task<int> AsyncStaticMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    public async ValueTask<int> ValueTaskInstanceMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    public static async ValueTask<int> ValueTaskStaticMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape(Name = "custom method name")]
    public void InstanceVoidMethod(int x, int y)
    {
        LastVoidResultBox.Value!.Value = x + y;
    }

    public void StaticVoidMethod(int x, [ParameterShape(IsRequired = true)] int y = -1)
    {
        LastVoidResultBox.Value!.Value = x + y;
    }

    public async Task InstanceTaskMethod(int x, [ParameterShape(Name = "y")] int z)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + z;
    }

    public static async Task StaticTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    public async ValueTask InstanceValueTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    public static async ValueTask StaticValueTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape]
    private ref int PrivateInstanceMethod(ref int x, in int y)
    {
        x += y;
        return ref x;
    }

    [MethodShape]
    private static ref readonly int PrivateStaticMethod(ref int x, ref readonly int y)
    {
        x += y;
        return ref x;
    }

    [MethodShape]
    private async Task<int> PrivateTaskMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape]
    private static async Task<int> PrivateStaticTaskMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape]
    private async ValueTask<int> PrivateValueTaskMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape]
    private static async ValueTask<int> PrivateStaticValueTaskMethod(int x, int y)
    {
        await Task.Yield();
        return x + y;
    }

    [MethodShape]
    private void PrivateInstanceVoidMethod(int x, int y)
    {
        LastVoidResultBox.Value!.Value = x + y;
    }

    [MethodShape]
    private static void PrivateStaticVoidMethod(int x, int y)
    {
        LastVoidResultBox.Value!.Value = x + y;
    }

    [MethodShape]
    private async Task PrivateInstanceTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape]
    private static async Task PrivateStaticVoidTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape]
    private async ValueTask PrivateInstanceVoidValueTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape]
    private static async ValueTask PrivateStaticVoidValueTaskMethod(int x, int y)
    {
        var currentVoidResultBox = LastVoidResultBox.Value!;
        await Task.Yield();
        currentVoidResultBox.Value = x + y;
    }

    [MethodShape(Ignore = true)]
    public void MethodWithOutParameterType(out int x) { x = 42; }

    [MethodShape(Ignore = true)]
    public int MethodWithInvalidParameterType(ReadOnlySpan<byte> values) => values.Length;

    [MethodShape(Ignore = true)]
    public ReadOnlySpan<byte> MethodWithInvalidReturnType() => [1, 2, 3];

    void IDisposable.Dispose() { }
}

public partial interface BaseInterfaceWithMethodShapes
{
    int BaseMethod(int x, int y);
}

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance | MethodShapeFlags.PublicStatic)]
public partial interface InterfaceWithMethodShapes : BaseInterfaceWithMethodShapes
{
    int UnusedProperty { get; set; }
    int SyncInstanceMethod(int x, int y);
    Task<int> AsyncInstanceMethod(int x, int y);
    ValueTask<int> ValueTaskInstanceMethod(int x, int y);
    [MethodShape(Name = "custom method name")]
    void InstanceVoidMethod(int x, [ParameterShape(IsRequired = true)] int y = -1);
    Task InstanceTaskMethod(int x, [ParameterShape(Name = "x")] int z);
    ValueTask InstanceValueTaskMethod(int x, int y);

    [MethodShape(Ignore = true)]
    void MethodWithOutParameterType(out int x);

    [MethodShape(Ignore = true)]
    int MethodWithInvalidParameterType(ReadOnlySpan<byte> values);

    [MethodShape(Ignore = true)]
    ReadOnlySpan<byte> MethodWithInvalidReturnType();
}

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial class RpcService
{
    private int _totalEvents;
    public event EventHandler<DateTimeOffset>? OnMethodCalled;
    public event AsyncEventHandler<DateTimeOffset>? OnMethodCalledAsync;

    public async IAsyncEnumerable<Event> GetEventsAsync(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await TriggerEvents(cancellationToken).ConfigureAwait(false);

        if (count < 0)
        {
            throw new ArgumentException("Negative counts are not permitted.");
        }

        for (int i = 0; i < count; i++)
        {
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            yield return new Event(_totalEvents++);
        }
    }

    [MethodShape(Name = "Private greet function")]
    private async ValueTask<string> GreetAsync(string name = "stranger")
    {
        await TriggerEvents().ConfigureAwait(false);
        return $"Hello, {name}!";
    }

    public async ValueTask ResetAsync()
    {
        await TriggerEvents().ConfigureAwait(false);
        await Task.CompletedTask.ConfigureAwait(false);
        _totalEvents = 0;
    }

    private async ValueTask TriggerEvents(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        OnMethodCalled?.Invoke(this, now);
        if (OnMethodCalledAsync is not null)
        {
            await OnMethodCalledAsync(this, now, cancellationToken).ConfigureAwait(false);
        }
    }

    public record Event(int id);

    public delegate ValueTask AsyncEventHandler<T>(object? sender, T e, CancellationToken cancellationToken);
}

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface InterfaceWithDiamondMethodShapes : IBase1WithMethod, IBase2WithMethod
{
    int Add3(int x, int y);

    public sealed class Impl : InterfaceWithDiamondMethodShapes
    {
        public int Add(int x, int y) => x + y;
        public int Add3(int x, int y) => x + y;
    }
}

public interface IBase1WithMethod
{
    int Add(int x, int y);
}

public interface IBase2WithMethod
{
    int Add(int x, int y);
}

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial class ClassWithEvent : ITriggerable
{
    public event Action<int>? OnChange;
    protected virtual void Trigger(int x) => OnChange?.Invoke(x);
    void ITriggerable.Trigger(int x) => Trigger(x);
}

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial class DerivedClassWithEvent : ClassWithEvent, ITriggerable
{
    public event Action<int>? OnChange2;
    protected override void Trigger(int x)
    {
        base.Trigger(x);
        OnChange2?.Invoke(x);
    }
}

[GenerateShape]
public partial struct StructWithEvent : ITriggerable
{
    [EventShape]
    public event Action<int>? OnChange;
    void ITriggerable.Trigger(int x) => OnChange?.Invoke(x);
}

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicStatic)]
public partial class ClassWithStaticEvent : ITriggerable
{
    public static event Action<int>? OnChange;

    void ITriggerable.Trigger(int x) => OnChange?.Invoke(x);
}

[GenerateShape]
public partial class ClassWithPrivateEvent : ITriggerable
{
    [EventShape(Name = "PrivateEvent")]
    private event Action<int>? OnChange;
    void ITriggerable.Trigger(int x) => OnChange?.Invoke(x);
}

[GenerateShape]
public partial class ClassWithPrivateStaticEvent : ITriggerable
{
    [EventShape(Name = "PrivateStaticEvent")]
    private static event Action<int>? OnChange;
    void ITriggerable.Trigger(int x) => OnChange?.Invoke(x);
}

public interface ITriggerable
{
    void Trigger(int x);
}

public delegate int CustomDelegate([ParameterShape(Name = "First", IsRequired = false)]ref string? x, [ParameterShape(Name = "Second")]int y = 42);

public delegate int LargeDelegate(
    int p01, int p02, int p03, int p04, int p05, int p06, int p07, int p08, int p09, int p10,
    int p11, int p12, int p13, int p14, int p15, int p16, int p17, int p18, int p19, int p20,
    int p21, int p22, int p23, int p24, int p25, int p26, int p27, int p28, int p29, int p30,
    int p31, int p32, int p33, int p34, int p35, int p36, int p37, int p38, int p39, int p40,
    int p41, int p42, int p43, int p44, int p45, int p46, int p47, int p48, int p49, int p50,
    int p51, int p52, int p53, int p54, int p55, int p56, int p57, int p58, int p59, int p60,
    int p61, int p62, int p63, int p64, int p65, int p66, int p67, int p68, int p69, int p70);

public delegate Task<int> LargeAsyncDelegate(
    int p01, int p02, int p03, int p04, int p05, int p06, int p07, int p08, int p09, int p10,
    int p11, int p12, int p13, int p14, int p15, int p16, int p17, int p18, int p19, int p20,
    int p21, int p22, int p23, int p24, int p25, int p26, int p27, int p28, int p29, int p30,
    int p31, int p32, int p33, int p34, int p35, int p36, int p37, int p38, int p39, int p40,
    int p41, int p42, int p43, int p44, int p45, int p46, int p47, int p48, int p49, int p50,
    int p51, int p52, int p53, int p54, int p55, int p56, int p57, int p58, int p59, int p60,
    int p61, int p62, int p63, int p64, int p65, int p66, int p67, int p68, int p69, int p70);

[GenerateShapeFor<object>]
[GenerateShapeFor<bool>]
[GenerateShapeFor<char>]
[GenerateShapeFor<string>]
[GenerateShapeFor<sbyte>]
[GenerateShapeFor<short>]
[GenerateShapeFor<int>]
[GenerateShapeFor<long>]
[GenerateShapeFor<byte>]
[GenerateShapeFor<ushort>]
[GenerateShapeFor<uint>]
[GenerateShapeFor<ulong>]
[GenerateShapeFor<float>]
[GenerateShapeFor<double>]
[GenerateShapeFor<decimal>]
[GenerateShapeFor<Guid>]
[GenerateShapeFor<DateTime>]
[GenerateShapeFor<DateTimeOffset>]
[GenerateShapeFor<TimeSpan>]
[GenerateShapeFor<BigInteger>]
[GenerateShapeFor<BindingFlags>]
[GenerateShapeFor<MyEnum>]
[GenerateShapeFor<bool?>]
[GenerateShapeFor<sbyte?>]
[GenerateShapeFor<short?>]
[GenerateShapeFor<int?>]
[GenerateShapeFor<long?>]
[GenerateShapeFor<byte?>]
[GenerateShapeFor<ushort?>]
[GenerateShapeFor<uint?>]
[GenerateShapeFor<ulong?>]
[GenerateShapeFor<float?>]
[GenerateShapeFor<double?>]
[GenerateShapeFor<decimal?>]
#if NET
[GenerateShapeFor<Half?>]
[GenerateShapeFor<Int128?>]
[GenerateShapeFor<UInt128?>]
[GenerateShapeFor<Rune?>]
[GenerateShapeFor<DateOnly?>]
[GenerateShapeFor<TimeOnly?>]
[GenerateShapeFor<Half>]
[GenerateShapeFor<Int128>]
[GenerateShapeFor<UInt128>]
[GenerateShapeFor<Rune>]
[GenerateShapeFor<DateOnly>]
[GenerateShapeFor<TimeOnly>]
[GenerateShapeFor<IReadOnlySet<int>>]
#endif
[GenerateShapeFor<Guid?>]
[GenerateShapeFor<DateTime?>]
[GenerateShapeFor<DateTimeOffset?>]
[GenerateShapeFor<TimeSpan?>]
[GenerateShapeFor<BigInteger?>]
[GenerateShapeFor<System.Drawing.Point>]
[GenerateShapeFor<BindingFlags?>]
[GenerateShapeFor<Uri>]
[GenerateShapeFor<Version>]
[GenerateShapeFor<string[]>]
[GenerateShapeFor<byte[]>]
[GenerateShapeFor<int[]>]
[GenerateShapeFor<int[][]>]
[GenerateShapeFor<int[,]>]
[GenerateShapeFor<int[,,]>]
[GenerateShapeFor<int[,,,,,]>]
[GenerateShapeFor<Memory<int>>]
[GenerateShapeFor<ReadOnlyMemory<int>>]
[GenerateShapeFor<List<string>>]
[GenerateShapeFor<List<byte>>]
[GenerateShapeFor<List<int>>]
[GenerateShapeFor<LinkedList<byte>>]
[GenerateShapeFor<Stack<int>>]
[GenerateShapeFor<Queue<int>>]
[GenerateShapeFor<Dictionary<string, int>>]
[GenerateShapeFor<Dictionary<string, string>>]
[GenerateShapeFor<Dictionary<SimpleRecord, string>>]
[GenerateShapeFor<Dictionary<string, SimpleRecord>>]
[GenerateShapeFor<SortedSet<string>>]
[GenerateShapeFor<SortedDictionary<string, int>>]
[GenerateShapeFor<SortedList<int, string>>]
[GenerateShapeFor<ConcurrentStack<int>>]
[GenerateShapeFor<ConcurrentQueue<int>>]
[GenerateShapeFor<ConcurrentDictionary<string, string>>]
[GenerateShapeFor<HashSet<string>>]
[GenerateShapeFor<Hashtable>]
[GenerateShapeFor<ArrayList>]
[GenerateShapeFor<StructList<int>>]
[GenerateShapeFor<StructDictionary<string, string>>]
[GenerateShapeFor<ExplicitlyImplementedList<int>>]
[GenerateShapeFor<ExplicitlyImplementedDictionary<string, string>>]
[GenerateShapeFor<GenericRecord<int>>]
[GenerateShapeFor<GenericRecord<string>>]
[GenerateShapeFor<GenericRecord<GenericRecord<bool>>>]
[GenerateShapeFor<GenericRecord<GenericRecord<int>>>]
[GenerateShapeFor<GenericRecordStruct<int>>]
[GenerateShapeFor<GenericRecordStruct<string>>]
[GenerateShapeFor<GenericRecordStruct<GenericRecordStruct<bool>>>]
[GenerateShapeFor<GenericRecordStruct<GenericRecordStruct<int>>>]
[GenerateShapeFor<GenericStructWithInitOnlyProperty<int>>]
[GenerateShapeFor<GenericStructWithInitOnlyProperty<string>>]
[GenerateShapeFor<GenericStructWithInitOnlyProperty<GenericStructWithInitOnlyProperty<int>>>]
[GenerateShapeFor<GenericStructWithInitOnlyProperty<GenericStructWithInitOnlyProperty<string>>>]
[GenerateShapeFor<ImmutableArray<int>>]
[GenerateShapeFor<ImmutableList<string>>]
[GenerateShapeFor<ImmutableQueue<int>>]
[GenerateShapeFor<ImmutableStack<int>>]
[GenerateShapeFor<ImmutableHashSet<int>>]
[GenerateShapeFor<ImmutableSortedSet<int>>]
[GenerateShapeFor<ImmutableDictionary<string, string>>]
[GenerateShapeFor<ImmutableSortedDictionary<string, string>>]
[GenerateShapeFor<IImmutableList<int>>]
[GenerateShapeFor<IImmutableQueue<int>>]
[GenerateShapeFor<IImmutableSet<int>>]
[GenerateShapeFor<IImmutableStack<int>>]
[GenerateShapeFor<IImmutableDictionary<string, string>>]
[GenerateShapeFor<FrozenDictionary<string, string>>]
[GenerateShapeFor<FrozenSet<int>>]
[GenerateShapeFor<IEnumerable>]
[GenerateShapeFor<IList>]
[GenerateShapeFor<ICollection>]
[GenerateShapeFor<IDictionary>]
[GenerateShapeFor<IEnumerable<int>>]
[GenerateShapeFor<ICollection<int>>]
[GenerateShapeFor<IList<int>>]
[GenerateShapeFor<IReadOnlyCollection<int>>]
[GenerateShapeFor<IReadOnlyList<int>>]
[GenerateShapeFor<ISet<int>>]
[GenerateShapeFor<IDictionary<int, int>>]
[GenerateShapeFor<IReadOnlyDictionary<int, int>>]
[GenerateShapeFor<NotNullGenericRecord<string>>]
[GenerateShapeFor<NotNullClassGenericRecord<string>>]
[GenerateShapeFor<NullClassGenericRecord<string>>]
[GenerateShapeFor<NullObliviousGenericRecord<string>>]
[GenerateShapeFor<MyLinkedList<int>>]
[GenerateShapeFor<RecursiveClassWithNonNullableOccurrence>]
[GenerateShapeFor<RecursiveClassWithNonNullableOccurrences>]
[GenerateShapeFor<GenericContainer<string>.Inner>]
[GenerateShapeFor<GenericContainer<string>.Inner<string>>]
[GenerateShapeFor<ValueTuple>]
[GenerateShapeFor<ValueTuple<int>>]
[GenerateShapeFor<ValueTuple<int, string>>]
[GenerateShapeFor<ValueTuple<int, int, int, int, int, int, int, int>>]
[GenerateShapeFor<(int, string)>]
[GenerateShapeFor<(int, string, bool)>]
[GenerateShapeFor<(int, string, (int, int))>]
[GenerateShapeFor<(int, int, int, int, int, int, int)>]
[GenerateShapeFor<(int, int, int, int, int, int, int, int, int)>]
[GenerateShapeFor<(int, int, int, int, int, int, int, int, int, int,
    int, int, int, int, int, int, int, int, int, int,
    int, int, int, int, int, int, int, int, int, int)>]
[GenerateShapeFor<Dictionary<int, (int, int)>>]
[GenerateShapeFor<Tuple<int>>]
[GenerateShapeFor<Tuple<int, int>>]
[GenerateShapeFor<Tuple<int, string, bool>>]
[GenerateShapeFor<Tuple<int, int, int, int, int, int, int>>]
[GenerateShapeFor<Tuple<int, int, int, int, int, int, int, int>>]
[GenerateShapeFor<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int>>>]
[GenerateShapeFor<Tuple<int, int, int, int, int, int, int, Tuple<int, int, int, int, int, int, int, Tuple<int>>>>]
[GenerateShapeFor<MyLinkedList<SimpleRecord>>]
[GenerateShapeFor<PersonStruct?>]
[GenerateShapeFor<PersonRecordStruct?>]
[GenerateShapeFor<GenericCollectionWithBuilderAttribute<int>>]
[GenerateShapeFor<ReadOnlyCollection<int>>]
[GenerateShapeFor<Collection<int>>]
[GenerateShapeFor<ReadOnlyCollection<int>>]
[GenerateShapeFor<ReadOnlyDictionary<int, int>>]
#if NET9_0_OR_GREATER
[GenerateShapeFor<ReadOnlySet<int>>]
#endif
[GenerateShapeFor<ObservableCollection<int>>]
[GenerateShapeFor<MyKeyedCollection<int>>]
[GenerateShapeFor<MyKeyedCollection<string>>]
[GenerateShapeFor<ClassWithNullableTypeParameters<int>>]
[GenerateShapeFor<ClassWithNullableTypeParameters<int?>>]
[GenerateShapeFor<ClassWithNullableTypeParameters<string>>]
[GenerateShapeFor<CollectionWithNullableElement<int>>]
[GenerateShapeFor<CollectionWithNullableElement<int?>>]
[GenerateShapeFor<CollectionWithNullableElement<string>>]
[GenerateShapeFor<DictionaryWithNullableEntries<int>>]
[GenerateShapeFor<DictionaryWithNullableEntries<int?>>]
[GenerateShapeFor<DictionaryWithNullableEntries<string>>]
[GenerateShapeFor<ClassWithNullableProperty<int>>]
[GenerateShapeFor<ClassWithNullableProperty<int?>>]
[GenerateShapeFor<ClassWithNullableProperty<string>>]
[GenerateShapeFor<ClassWithNullabilityAttributes<string>>]
[GenerateShapeFor<ClassWithNotNullProperty<string>>]
[GenerateShapeFor<GenericClassWithPrivateConstructor<int>>]
[GenerateShapeFor<GenericClassWithPrivateConstructor<string>>]
[GenerateShapeFor<GenericClassWithPrivateField<int>>]
[GenerateShapeFor<GenericClassWithPrivateField<string>>]
[GenerateShapeFor<GenericStructWithPrivateField<int>>]
[GenerateShapeFor<GenericStructWithPrivateField<string>>]
[GenerateShapeFor<GenericClassWithMultipleRefConstructorParametersPrivate<int>>]
[GenerateShapeFor<GenericClassWithMultipleRefConstructorParametersPrivate<string>>]
[GenerateShapeFor<GenericStructWithPrivateIncludedMembers<int>>]
[GenerateShapeFor<GenericStructWithPrivateIncludedMembers<string>>]
[GenerateShapeFor<EnumWithRecordSurrogate>]
[GenerateShapeFor<TypeWithGenericMarshaler<int>>]
[GenerateShapeFor<TypeWithGenericMarshaler<string>>]
[GenerateShapeFor<GenericDictionaryWithMarshaler<string, int>>]
[GenerateShapeFor<GenericTree<string>>]
[GenerateShapeFor<GenericTree<int>>]
[GenerateShapeFor<GenericRecordWithoutNamespace<int>>]
[GenerateShapeFor<GenericContainerWithoutNamespace<int>.Record<string>>]
[GenerateShapeFor<IAsyncEnumerable<int>>]
[GenerateShapeFor<FSharpRecord>]
[GenerateShapeFor<FSharpStructRecord>]
[GenerateShapeFor<GenericFSharpRecord<string>>]
[GenerateShapeFor<GenericFSharpStructRecord<string>>]
[GenerateShapeFor<FSharpClass>]
[GenerateShapeFor<FSharpStruct>]
[GenerateShapeFor<GenericFSharpClass<string>>]
[GenerateShapeFor<GenericFSharpStruct<string>>]
[GenerateShapeFor<Microsoft.FSharp.Core.Unit>]
[GenerateShapeFor<FSharpList<int>>]
[GenerateShapeFor<FSharpMap<string, int>>]
[GenerateShapeFor<FSharpSet<int>>]
[GenerateShapeFor<FSharpRecordWithCollections>]
[GenerateShapeFor<FSharpOption<int>>]
[GenerateShapeFor<FSharpOption<string>>]
[GenerateShapeFor<FSharpValueOption<int>>]
[GenerateShapeFor<FSharpValueOption<string>>]
[GenerateShapeFor<FSharpUnion>]
[GenerateShapeFor<FSharpSingleCaseUnion>]
[GenerateShapeFor<FSharpEnumUnion>]
[GenerateShapeFor<GenericFSharpUnion<string>>]
[GenerateShapeFor<FSharpStructUnion>]
[GenerateShapeFor<FSharpSingleCaseStructUnion>]
[GenerateShapeFor<FSharpEnumStructUnion>]
[GenerateShapeFor<GenericFSharpStructUnion<string>>]
[GenerateShapeFor<FSharpResult<string, int>>]
[GenerateShapeFor<FSharpExpr>]
[GenerateShapeFor<NullaryUnion>]
[GenerateShapeFor<ExpandoObject>]
[GenerateShapeFor<Action>]
[GenerateShapeFor<Action<int>>]
[GenerateShapeFor<Action<int, int>>]
[GenerateShapeFor<Func<int>>]
[GenerateShapeFor<Func<int, int>>]
[GenerateShapeFor<Func<int, int, int>>]
[GenerateShapeFor<Func<int, int, int, int, int, int, int>>]
[GenerateShapeFor<Func<int, Task>>]
[GenerateShapeFor<Func<int, Task<int>>>]
[GenerateShapeFor<Func<int, int, Task<int>>>]
[GenerateShapeFor<Func<int, ValueTask>>]
[GenerateShapeFor<Func<int, ValueTask<int>>>]
[GenerateShapeFor<Func<int, int, ValueTask<int>>>]
[GenerateShapeFor<Func<int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, ValueTask<int>>>]
[GenerateShapeFor<Getter<int, int>>]
[GenerateShapeFor<Setter<int, int>>]
[GenerateShapeFor<EventHandler>]
[GenerateShapeFor<CustomDelegate>]
[GenerateShapeFor<LargeDelegate>]
[GenerateShapeFor<LargeAsyncDelegate>]
[GenerateShapeFor<FSharpFunc<int, int>>]
[GenerateShapeFor<FSharpFunc<int, FSharpFunc<int, int>>>]
[GenerateShapeFor<FSharpFunc<Microsoft.FSharp.Core.Unit, int>>]
[GenerateShapeFor<FSharpFunc<Microsoft.FSharp.Core.Unit, FSharpFunc<int, FSharpFunc<Microsoft.FSharp.Core.Unit, int>>>>]
[GenerateShapeFor<FSharpFunc<int, Microsoft.FSharp.Core.Unit>>]
[GenerateShapeFor<FSharpFunc<int, FSharpFunc<int, Microsoft.FSharp.Core.Unit>>>]
[GenerateShapeFor<FSharpFunc<Tuple<int, int>, int>>]
[GenerateShapeFor<FSharpFunc<int, Task<int>>>]
[GenerateShapeFor<FSharpFunc<int, FSharpFunc<int, FSharpFunc<int, Task<int>>>>>]
public partial class Witness;

// Test interfaces for issue #295: IEventShape.AttributeProvider is null for events inherited from an interface
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IServer
{
    event EventHandler InterfaceEvent;

    event EventHandler ExplicitInterfaceImplementation_Event;
}

[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IServerDerived : IServer
{
    event EventHandler DerivedInterfaceEvent;
}