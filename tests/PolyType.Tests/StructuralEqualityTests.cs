using PolyType.Examples.StructuralEquality;
using Xunit;

namespace PolyType.Tests;

public abstract partial class StructuralEqualityTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    public void DictionaryComparison_IgnoresStorageComparersButDetectsDifferentEntries(int leftComparerKind, int rightComparerKind)
    {
        IEqualityComparer<string> leftComparer = leftComparerKind == 0 ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        IEqualityComparer<string> rightComparer = rightComparerKind == 0 ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var left = new Dictionary<string, int>(leftComparer) { ["key"] = 42 };
        var right = new Dictionary<string, int>(rightComparer) { ["key"] = 42 };
        var comparer = StructuralEqualityComparer.Create<Dictionary<string, int>>(providerUnderTest.Provider);
        Assert.True(comparer.Equals(left, right));
        Assert.True(comparer.Equals(right, left));
        Assert.Equal(comparer.GetHashCode(left), comparer.GetHashCode(right));
        right["key"] = 43;
        Assert.False(comparer.Equals(left, right));
        right.Clear();
        right["other"] = 42;
        Assert.False(comparer.Equals(left, right));
        right["key"] = 42;
        Assert.False(comparer.Equals(left, right));
        Assert.False(comparer.Equals(left, null!));
        Assert.False(comparer.Equals(null!, right));
    }

#if NET
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    public void ShapeableEntryPoints_UseStructuralEqualityAndHashing(int value)
    {
        SimplePoco left = new() { Value = value };
        SimplePoco right = new() { Value = value };
        var comparer = StructuralEqualityComparer.Create<SimplePoco>();
        var providerComparer = StructuralEqualityComparer.Create<SimplePoco>(providerUnderTest.Provider);
        Assert.True(comparer.Equals(left, right));
        Assert.True(providerComparer.Equals(left, right));
        Assert.True(StructuralEqualityComparer.Equals(left, right));
        Assert.Equal(comparer.GetHashCode(left), StructuralEqualityComparer.GetHashCode(right));
        right.Value++;
        Assert.False(StructuralEqualityComparer.Equals(left, right));

        var externalComparer = StructuralEqualityComparer.Create<int, Witness>();
        Assert.True(StructuralEqualityComparer.Equals<int, Witness>(value, value));
        Assert.False(StructuralEqualityComparer.Equals<int, Witness>(value, value + 1));
        Assert.Equal(externalComparer.GetHashCode(value), StructuralEqualityComparer.GetHashCode<int, Witness>(value));
    }
#endif

    [Theory]
    [MemberData(nameof(TestTypes.GetEqualValuePairs), MemberType = typeof(TestTypes))]
    public void EqualityComparer_EqualValues<T>(TestCase<T> left, TestCase<T> right)
    {
        if (!typeof(T).IsValueType && typeof(T) != typeof(string))
        {
            // ensure we're not using reference equality
            if (left.Value is null)
            {
                Assert.Null(right.Value);
            }
            else if (!left.IsUnion && !left.IsFunctionType) // Some union cases or function types can be singletons
            {
                Assert.NotSame((object?)left.Value, (object?)right.Value);
            }
        }

        IEqualityComparer<T> cmp = GetEqualityComparerUnderTest<T>();

        if (left.Value is not null)
        {
            Assert.Equal(cmp.GetHashCode(left.Value!), cmp.GetHashCode(right.Value!));
        }

        Assert.Equal(left.Value, right.Value, cmp!);
        Assert.Equal(right.Value, left.Value, cmp!);
    }

    [Theory]
    [MemberData(nameof(GetNotEqualValues))]
    public void EqualityComparer_NotEqualValues<T>(T left, T right)
    {
        IEqualityComparer<T> cmp = GetEqualityComparerUnderTest<T>();
        
        Assert.NotEqual(left, right, cmp!);
        Assert.NotEqual(right, left, cmp!);
    }

    public static IEnumerable<object?[]> GetNotEqualValues()
    {
        yield return NotEqual(false, true);
        yield return NotEqual(null, "");
        yield return NotEqual(-1, 4);
        yield return NotEqual(3.14, -7.5);
        yield return NotEqual(DateTime.MinValue, DateTime.MaxValue);
        yield return NotEqual((int[])[1, 2, 3], []);
        yield return NotEqual((int[])[1, 2, 3], [1, 2, 0]);
        yield return NotEqual<int[][]>(
            [[1, 0, 0], [0, 1, 0], [0, 0, 1]],
            [[1, 0, 0], [0, 0, 0], [0, 0, 1]]);

        yield return NotEqual(
            new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 },
            new Dictionary<string, int> { ["key1"] = 42, ["key2"] = 1 });

        yield return NotEqual(
            new Dictionary<string, int> { ["key1"] = 42, ["key5"] = -1 },
            new Dictionary<string, int> { ["key1"] = 42, ["key2"] = -1 });

        yield return NotEqual(
            new DerivedClass { X = 1, Y = 2 },
            new DerivedClass { X = 1, Y = -1 });

        yield return NotEqual(
            new MyLinkedList<int>
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
            },
            new MyLinkedList<int>
            {
                Value = 1,
                Next = new()
                {
                    Value = 2,
                    Next = null
                }
            });

        static object?[] NotEqual<T>(T left, T right) => [left, right];
    }

    private IEqualityComparer<T> GetEqualityComparerUnderTest<T>() =>
        StructuralEqualityComparer.Create<T>(providerUnderTest.Provider);
}

public sealed class StructuralEqualityTests_Reflection() : StructuralEqualityTests(ReflectionProviderUnderTest.NoEmit);
public sealed class StructuralEqualityTests_ReflectionEmit() : StructuralEqualityTests(ReflectionProviderUnderTest.Emit);
public sealed class StructuralEqualityTests_SourceGen() : StructuralEqualityTests(SourceGenProviderUnderTest.Default);