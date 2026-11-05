namespace PolyType.Roslyn.Tests;

using Xunit;
using System.Collections;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public static class ImmutableEquatableCollectionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public static void ImmutableArray_EmptyRepresentationsAreDefaultSafe(int representation)
    {
        ImmutableEquatableArray<int> array = representation switch
        {
            0 => default,
            1 => ImmutableEquatableArray<int>.Empty,
            2 => [],
            3 => Array.Empty<int>().ToImmutableEquatableArray(),
            _ => Enumerable.Range(0, 0).Where(_ => true).ToImmutableEquatableArray(),
        };

        AssertNormalizedEmpty(array);
        Assert.True(array.Equals(default));
        Assert.True(((IEquatable<ImmutableEquatableArray<int>>)array).Equals(default));
        Assert.Equal(0, array.Length);
        Assert.False(array.GetEnumerator().MoveNext());
        Assert.Empty(array);
        Assert.Empty((IEnumerable)array);

        IReadOnlyList<int> readOnly = array;
        IList<int> generic = array;
        IList list = array;
        Assert.True(readOnly.Count is 0);
        Assert.True(generic.Count is 0);
        Assert.True(list.Count is 0);
        Assert.True(generic.IsReadOnly);
        Assert.True(list.IsReadOnly);
        Assert.True(list.IsFixedSize);
        Assert.True(list.IsSynchronized);
        Assert.Throws<NotSupportedException>(() => list.SyncRoot);
        Assert.False(generic.Contains(0));
        Assert.False(list.Contains(0));
        Assert.Equal(-1, generic.IndexOf(0));
        Assert.Equal(-1, list.IndexOf(0));
        generic.CopyTo([], 0);
        list.CopyTo(Array.Empty<int>(), 0);
        Assert.Throws<IndexOutOfRangeException>(() => array[0]);
        Assert.Throws<IndexOutOfRangeException>(() => readOnly[0]);
        Assert.Throws<IndexOutOfRangeException>(() => generic[0]);
        Assert.Throws<IndexOutOfRangeException>(() => list[0]);
        Assert.Throws<ArgumentNullException>(() => generic.CopyTo(null!, 0));
        Assert.Throws<ArgumentNullException>(() => list.CopyTo(null!, 0));
        Assert.Throws<InvalidOperationException>(() => generic.Add(0));
        Assert.Throws<InvalidOperationException>(() => list.Add(0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public static void ImmutableSet_EmptyRepresentationsAreDefaultSafe(int representation)
    {
        ImmutableEquatableSet<int> set = representation switch
        {
            0 => default,
            1 => ImmutableEquatableSet<int>.Empty,
            2 => [],
            3 => Array.Empty<int>().ToImmutableEquatableSet(),
            _ => Enumerable.Range(0, 0).Where(_ => true).ToImmutableEquatableSet(),
        };

        AssertNormalizedEmpty(set);
        Assert.True(set.Equals(default));
        Assert.True(((IEquatable<ImmutableEquatableSet<int>>)set).Equals(default));
        Assert.True(set.Count is 0);
        Assert.DoesNotContain(0, set);
        Assert.False(set.GetEnumerator().MoveNext());
        Assert.Empty(set);
        Assert.Empty((IEnumerable)set);

        ISet<int> generic = set;
        ICollection collection = set;
        Assert.True(generic.IsReadOnly);
        Assert.True(collection.Count is 0);
        Assert.True(collection.IsSynchronized);
        Assert.Throws<NotSupportedException>(() => collection.SyncRoot);
        generic.CopyTo([], 0);
        Assert.True(generic.IsSubsetOf([]));
        Assert.True(generic.IsSupersetOf([]));
        Assert.False(generic.IsProperSubsetOf([]));
        Assert.False(generic.IsProperSupersetOf([]));
        Assert.True(generic.SetEquals([]));
        Assert.False(generic.Overlaps([]));
        Assert.True(generic.IsSubsetOf([1]));
        Assert.True(generic.IsProperSubsetOf([1]));
        Assert.False(generic.IsSupersetOf([1]));
        Assert.False(generic.IsProperSupersetOf([1]));
        Assert.False(generic.SetEquals([1]));
        Assert.False(generic.Overlaps([1]));
        Assert.Throws<ArgumentNullException>(() => generic.CopyTo(null!, 0));
        Assert.Throws<ArgumentNullException>(() => generic.IsSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => generic.IsSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => generic.IsProperSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => generic.IsProperSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => generic.SetEquals(null!));
        Assert.Throws<ArgumentNullException>(() => generic.Overlaps(null!));
        Assert.Throws<InvalidOperationException>(() => generic.Add(0));
        Assert.Throws<InvalidOperationException>(() => collection.CopyTo(Array.Empty<int>(), 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public static void ImmutableDictionary_EmptyRepresentationsAreDefaultSafe(int representation)
    {
        ImmutableEquatableDictionary<int, int> dictionary = representation switch
        {
            0 => default,
            1 => ImmutableEquatableDictionary<int, int>.Empty,
            2 => ImmutableEquatableDictionary.Empty<int, int>(),
            3 => Array.Empty<int>().ToImmutableEquatableDictionary(i => i),
            4 => Enumerable.Range(0, 0).Where(_ => true).ToImmutableEquatableDictionary(i => i, i => i),
            5 => new Dictionary<int, int>().ToImmutableEquatableDictionary(),
            _ => Enumerable.Range(0, 0).Select(i => new KeyValuePair<int, int>(i, i)).ToImmutableEquatableDictionary(),
        };

        AssertNormalizedEmpty(dictionary);
        Assert.True(dictionary.Equals(default));
        Assert.True(((IEquatable<ImmutableEquatableDictionary<int, int>>)dictionary).Equals(default));
        Assert.True(dictionary.Count is 0);
        Assert.False(dictionary.ContainsKey(0));
        Assert.False(dictionary.TryGetValue(0, out int value));
        Assert.Equal(0, value);
        Assert.False(dictionary.GetEnumerator().MoveNext());
        Assert.Empty(dictionary);
        Assert.Empty((IEnumerable)dictionary);
        Assert.Empty(dictionary.Keys);
        Assert.Empty(dictionary.Values);
        Assert.Throws<KeyNotFoundException>(() => dictionary[0]);

        IReadOnlyDictionary<int, int> readOnly = dictionary;
        IDictionary<int, int> generic = dictionary;
        IDictionary nongeneric = dictionary;
        Assert.Empty(readOnly.Keys);
        Assert.Empty(readOnly.Values);
        Assert.Empty(generic.Keys);
        Assert.Empty(generic.Values);
        Assert.Empty(nongeneric.Keys);
        Assert.Empty(nongeneric.Values);
        Assert.True(readOnly.Count is 0);
        Assert.True(generic.Count is 0);
        Assert.True(nongeneric.Count is 0);
        Assert.False(nongeneric.GetEnumerator().MoveNext());
        Assert.False(nongeneric.Contains(0));
        Assert.Null(nongeneric[0]);
        Assert.True(generic.IsReadOnly);
        Assert.True(nongeneric.IsReadOnly);
        Assert.True(nongeneric.IsFixedSize);
        Assert.True(nongeneric.IsSynchronized);
        Assert.Throws<NotSupportedException>(() => nongeneric.SyncRoot);
        Assert.False(generic.Contains(new KeyValuePair<int, int>(0, 0)));
        generic.CopyTo([], 0);
        nongeneric.CopyTo(Array.Empty<KeyValuePair<int, int>>(), 0);
        Assert.Throws<KeyNotFoundException>(() => generic[0]);
        Assert.Throws<KeyNotFoundException>(() => readOnly[0]);
        Assert.Throws<ArgumentNullException>(() => generic.CopyTo(null!, 0));
        Assert.Throws<ArgumentNullException>(() => nongeneric.CopyTo(null!, 0));
        Assert.Throws<ArgumentNullException>(() => nongeneric.Contains(null!));
        Assert.Throws<InvalidOperationException>(() => generic.Add(0, 0));
        Assert.Throws<InvalidOperationException>(() => nongeneric.Add(0, 0));
    }

    private static void AssertNormalizedEmpty<T>(T collection) where T : struct
    {
        Assert.Equal(0, collection.GetHashCode());
        Assert.Equal(default, collection);
        FieldInfo storage = typeof(T).GetField("_values", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(storage.GetValue(collection));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public static void ImmutableCollections_StructCopiesPreserveOwnedStorage(int count)
    {
        int[] source = Enumerable.Range(0, count).ToArray();
        var array = source.ToImmutableEquatableArray();
        var set = source.ToImmutableEquatableSet();
        var dictionary = source.ToImmutableEquatableDictionary(i => i);
        var arrayCopy = array;
        var setCopy = set;
        var dictionaryCopy = dictionary;
        Array.Clear(source, 0, source.Length);
        Assert.Equal(Enumerable.Range(0, count), arrayCopy);
        Assert.Equal(Enumerable.Range(0, count), setCopy.OrderBy(i => i));
        Assert.Equal(Enumerable.Range(0, count), dictionaryCopy.Keys.OrderBy(i => i));
        Assert.Equal(array, arrayCopy);
        Assert.Equal(set, setCopy);
        Assert.Equal(dictionary, dictionaryCopy);
        Assert.True(array == Enumerable.Range(0, count).ToImmutableEquatableArray());
        Assert.True(set == Enumerable.Range(0, count).ToImmutableEquatableSet());
        Assert.True(dictionary == Enumerable.Range(0, count).ToImmutableEquatableDictionary(i => i));
        Assert.False(array != arrayCopy);
        Assert.False(set != setCopy);
        Assert.False(dictionary != dictionaryCopy);
        Assert.True(array != Enumerable.Range(0, count + 1).ToImmutableEquatableArray());
        Assert.True(set != Enumerable.Range(0, count + 1).ToImmutableEquatableSet());
        Assert.True(dictionary != Enumerable.Range(0, count + 1).ToImmutableEquatableDictionary(i => i));

        ImmutableEquatableArray<int>? optionalArray = array;
        ImmutableEquatableSet<int>? optionalSet = set;
        ImmutableEquatableDictionary<int, int>? optionalDictionary = dictionary;
        Assert.True(optionalArray.HasValue);
        Assert.True(optionalSet.HasValue);
        Assert.True(optionalDictionary.HasValue);
        Assert.NotEqual(default(ImmutableEquatableArray<int>?), optionalArray);
        Assert.NotEqual(default(ImmutableEquatableSet<int>?), optionalSet);
        Assert.NotEqual(default(ImmutableEquatableDictionary<int, int>?), optionalDictionary);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static void ImmutableCollections_ObjectEqualityRejectsNull(int count)
    {
        var array = Enumerable.Range(0, count).ToImmutableEquatableArray();
        var set = Enumerable.Range(0, count).ToImmutableEquatableSet();
        var dictionary = Enumerable.Range(0, count).ToImmutableEquatableDictionary(i => i);

        Assert.False(array.Equals(null));
        Assert.False(set.Equals(null));
        Assert.False(dictionary.Equals(null));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, "value", false)]
    [InlineData("value", null, false)]
    [InlineData("value", "value", true)]
    public static void ImmutableDictionaryEquality_SupportsNullValues(string? leftValue, string? rightValue, bool expected)
    {
        var left = new[] { new KeyValuePair<int, string>(0, leftValue!) }.ToImmutableEquatableDictionary();
        var right = new[] { new KeyValuePair<int, string>(0, rightValue!) }.ToImmutableEquatableDictionary();

        Assert.Equal(expected, left.Equals(right));
        Assert.Equal(expected, right.Equals(left));
        if (expected)
        {
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("readonly ", false)]
    public static void ImmutableArrayEnumerator_OnlyAllowsReadOnlyReferences(string modifier, bool expectsError)
    {
        CSharpCompilation compilation = CreateConsumerCompilation($$"""
            public static class Consumer
            {
                public static int Read(PolyType.Roslyn.ImmutableEquatableArray<int>.Enumerator enumerator)
                {
                    ref {{modifier}}int item = ref enumerator.Current;
                    return item;
                }
            }
            """);

        Diagnostic[] errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity is DiagnosticSeverity.Error).ToArray();
        if (expectsError)
        {
            Assert.Equal("CS8329", Assert.Single(errors).Id);
        }
        else
        {
            Assert.Empty(errors);
        }
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("!", true)]
    public static void ImmutableDictionaryTryGetValue_OnlyGuaranteesNonNullOnSuccess(string negation, bool expectsWarning)
    {
        CSharpCompilation compilation = CreateConsumerCompilation($$"""
            public static class Consumer
            {
                public static int Read(PolyType.Roslyn.ImmutableEquatableDictionary<int, string> dictionary)
                {
                    if ({{negation}}dictionary.TryGetValue(0, out var value))
                    {
                        return value.Length;
                    }

                    return 0;
                }
            }
            """);

        Diagnostic[] diagnostics = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .ToArray();
        if (expectsWarning)
        {
            Assert.Equal("CS8602", Assert.Single(diagnostics).Id);
        }
        else
        {
            Assert.Empty(diagnostics);
        }
    }

    private static CSharpCompilation CreateConsumerCompilation(string source)
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ImmutableEquatableArray<>).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("netstandard")).Location),
        };
#if NET
        references.Add(MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Runtime")).Location));
#endif
        return CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static void ImmutableDictionaryEquality_DistinguishesKeysAndCounts(int count)
    {
        var left = Enumerable.Range(0, count).ToImmutableEquatableDictionary(i => i, i => i);
        var right = Enumerable.Range(0, count).ToImmutableEquatableDictionary(i => i, i => i);
        Assert.True(left.Equals(right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.True(left.Equals(left));
        Assert.False(left.Equals((object?)null));
        var differentKeys = Enumerable.Range(1, count).ToImmutableEquatableDictionary(i => i, i => i);
        Assert.Equal(count == 0, left.Equals(differentKeys));
        var differentCount = Enumerable.Range(0, count + 1).ToImmutableEquatableDictionary(i => i, i => i);
        Assert.False(left.Equals(differentCount));
        Assert.False(left.Equals(new object()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static void ImmutableSetEquality_RejectsNullAndDifferentCounts(int count)
    {
        var set = Enumerable.Range(0, count).ToImmutableEquatableSet();
        Assert.False(set.Equals((object?)null));
        Assert.False(set.Equals(Enumerable.Range(0, count + 1).ToImmutableEquatableSet()));
        Assert.False(set.Equals(new object()));
    }

    [Fact]
    public static void ImmutableEquatableArray_EqualsSequenceEqualValue()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        ImmutableEquatableArray<int> array2 = [1, 2, 3, 4, 5];
        Assert.Equal(array1.GetHashCode(), array2.GetHashCode());
        Assert.Equal(array1, array2);
        Assert.True(array1.Equals((object)array2));
    }
    
    [Fact]
    public static void ImmutableEquatableArray_DoesNotEqualNonEqualValues()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        ImmutableEquatableArray<int> array2 = [1, 2, 3, 4, 7];
        Assert.NotEqual(array1, array2);
        Assert.False(array1.Equals((object)array2));
    }
    
    [Fact]
    public static void ImmutableEquatableArray_IReadOnlyListOfTView()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        IReadOnlyList<int> list = array1;

        Assert.Equal([1, 2, 3, 4, 5], list);
        Assert.Equal(1, list[0]);
        Assert.Equal(5, list.Count);
    }
    
    [Fact]
    public static void ImmutableEquatableArray_IListOfTView()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        IList<int> list = array1;

        Assert.Equal([1, 2, 3, 4, 5], list);
        Assert.Equal(1, list[0]);
        Assert.Equal(5, list.Count);
        Assert.True(list.IsReadOnly);
        Assert.True(list.Contains(1));
        Assert.Equal(3, list.IndexOf(4));
        
        int[] buffer = new int[5];
        list.CopyTo(buffer, 0);
        Assert.Equal(list, buffer);
        
        Assert.Throws<InvalidOperationException>(() => list.RemoveAt(2));
        Assert.Throws<InvalidOperationException>(() => list.Remove(1));
        Assert.Throws<InvalidOperationException>(() => list.Insert(0, 5));
        Assert.Throws<InvalidOperationException>(() => list.Add(5));
        Assert.Throws<InvalidOperationException>(() => list.Clear());
        Assert.Throws<InvalidOperationException>(() => list[0] = -1);
    }

    [Fact]
    public static void ImmutableEquatableArray_IListView()
    {
        ImmutableEquatableArray<int> array1 = [1, 2, 3, 4, 5];
        IList list = array1;
        
        Assert.True(list.IsReadOnly);
        Assert.True(list.IsFixedSize);
        Assert.Equal(1, list[0]);
        Assert.Equal(5, list.Count);
        Assert.Equal(3, list.IndexOf(4));
        Assert.True(list.Contains(1));
        Assert.True(list.IsSynchronized);
        Assert.Throws<NotSupportedException>(() => list.SyncRoot);
        
        int[] buffer = new int[5];
        list.CopyTo(buffer, 0);
        Assert.Equal(list, buffer);
        
        Assert.Throws<InvalidOperationException>(() => list.RemoveAt(2));
        Assert.Throws<InvalidOperationException>(() => list.Remove(1));
        Assert.Throws<InvalidOperationException>(() => list.Insert(0, 5));
        Assert.Throws<InvalidOperationException>(() => list.Add(5));
        Assert.Throws<InvalidOperationException>(() => list.Clear());
        Assert.Throws<InvalidOperationException>(() => list[0] = -1);
    }

    [Fact]
    public static void ImmutableEquatableSet_EqualsSetEqualValue()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        ImmutableEquatableSet<int> set2 = [1, 2, 3, 5, 4];
        
        Assert.Equal(set1.GetHashCode(), set2.GetHashCode());
        Assert.Equal(set1, set2);
        Assert.True(set1.Equals((object)set2));
    }

    [Fact]
    public static void ImmutableEquatableSet_DoesNotEqualNonEqualValues()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        ImmutableEquatableSet<int> set2 = [1, 2, 3, 4, 7];
        
        Assert.NotEqual(set1, set2);
        Assert.False(set1.Equals((object)set2));
    }

    [Fact]
    public static void ImmutableEquatableSet_ISetOfTView()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        ISet<int> set2 = set1;

        Assert.Equal([1, 2, 3, 4, 5], set2);
        Assert.Equal(5, set2.Count);
        Assert.True(set2.Contains(1));
        Assert.True(set2.IsSubsetOf(set2));
        Assert.True(set2.IsSupersetOf(set2));
        Assert.False(set2.IsProperSubsetOf(set2));
        Assert.False(set2.IsProperSupersetOf(set2));
        Assert.True(set2.Overlaps(set2));
        Assert.True(set2.SetEquals(set2));
        Assert.True(set2.IsReadOnly);
        
        int[] buffer = new int[5];
        set2.CopyTo(buffer, 0);
        Assert.Equal(set2, buffer);
        
        Assert.Throws<InvalidOperationException>(() => ((ICollection<int>)set2).Add(5));
        Assert.Throws<InvalidOperationException>(() => set2.Add(5));
        Assert.Throws<InvalidOperationException>(() => set2.Remove(2));
        Assert.Throws<InvalidOperationException>(() => set2.Clear());
        Assert.Throws<InvalidOperationException>(() => set2.UnionWith(set2));
        Assert.Throws<InvalidOperationException>(() => set2.ExceptWith(set2));
        Assert.Throws<InvalidOperationException>(() => set2.IntersectWith(set2));
        Assert.Throws<InvalidOperationException>(() => set2.SymmetricExceptWith(set2));
    }
    
    [Fact]
    public static void ImmutableEquatableSet_IReadOnlyCollectionOfTView()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        IReadOnlyCollection<int> set2 = set1;

        Assert.Equal([1, 2, 3, 4, 5], set2);
        Assert.Equal(5, set2.Count);
        Assert.Contains(1, set2);
    }
    
    [Fact]
    public static void ImmutableEquatableSet_ICollectionView()
    {
        ImmutableEquatableSet<int> set1 = [1, 2, 3, 4, 5];
        ICollection set2 = set1;
        
        Assert.Equal(5, set2.Count);
        Assert.True(set2.IsSynchronized);
        Assert.Throws<NotSupportedException>(() => set2.SyncRoot);
        
        int[] buffer = new int[5];
        Assert.Throws<InvalidOperationException>(() => set2.CopyTo(buffer, 0));
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_EqualsSequenceEqualValue()
    {
        ImmutableEquatableDictionary<int, int> array1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        ImmutableEquatableDictionary<int, int> array2 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        
        Assert.Equal(array1.GetHashCode(), array2.GetHashCode());
        Assert.Equal(array1, array2);
        Assert.True(array1.Equals((object)array2));
    }

    [Fact]
    public static void ImmutableEquatableDictionary_DoesNotEqualNonEqualValues()
    {
        ImmutableEquatableDictionary<int, int> dictionary1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        ImmutableEquatableDictionary<int, int> dictionary2 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i == 5 ? 20 : i);
        Assert.NotEqual(dictionary1, dictionary2);
        Assert.False(dictionary1.Equals((object)dictionary2));
    }

    [Fact]
    public static void ImmutableEquatableDictionary_IReadOnlyDictionaryOfTView()
    {
        ImmutableEquatableDictionary<int, int> dictionary1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        IReadOnlyDictionary<int, int> dictionary2 = dictionary1;
        
        Assert.Equal(dictionary1, dictionary2);
        Assert.Equal(10, dictionary2.Count);
        Assert.Equal(dictionary1.Keys, dictionary2.Keys);
        Assert.Equal(dictionary1.Values, dictionary2.Values);
        Assert.True(dictionary2.ContainsKey(1));
        Assert.True(dictionary2.TryGetValue(1, out int _));
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_IDictionaryOfTView()
    {
        ImmutableEquatableDictionary<int, int> dictionary1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        IDictionary<int, int> dictionary2 = dictionary1;
        
        Assert.Equal(dictionary1, dictionary2);
        Assert.Equal(10, dictionary2.Count);
        Assert.Equal(dictionary1.Keys, dictionary2.Keys);
        Assert.Equal(dictionary1.Values, dictionary2.Values);
        Assert.True(dictionary2.ContainsKey(1));
        Assert.True(dictionary2.Contains(new KeyValuePair<int, int>(1, 1)));
        Assert.True(dictionary2.TryGetValue(1, out int _));
        Assert.Equal(4, dictionary2[4]);
        Assert.True(dictionary2.IsReadOnly);
        
        KeyValuePair<int, int>[] buffer = new KeyValuePair<int, int>[10];
        dictionary2.CopyTo(buffer, 0);
        Assert.Equal(dictionary2, buffer);
        
        Assert.Throws<InvalidOperationException>(() => dictionary2.Remove(1));
        Assert.Throws<InvalidOperationException>(() => dictionary2.Clear());
        Assert.Throws<InvalidOperationException>(() => dictionary2.Add(55, 5));
        Assert.Throws<InvalidOperationException>(() => dictionary2[1] = 42);
        
        Assert.Throws<InvalidOperationException>(() => dictionary2[2] = 42);
        Assert.Throws<InvalidOperationException>(() => dictionary2.Add(new KeyValuePair<int, int>(1, 42)));
        Assert.Throws<InvalidOperationException>(() => dictionary2.Remove(new KeyValuePair<int, int>(1, 42)));
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_IDictionaryView()
    {
        ImmutableEquatableDictionary<int, int> dictionary1 = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i, i => i);
        IDictionary dictionary2 = dictionary1;
        
        Assert.Equal(dictionary1, dictionary2);
        Assert.Equal(10, dictionary2.Count);
        Assert.Equal(dictionary1.Keys, dictionary2.Keys);
        Assert.Equal(dictionary1.Values, dictionary2.Values);
        Assert.True(dictionary2.Contains(1));
        Assert.Equal(4, dictionary2[4]);
        Assert.True(dictionary2.IsReadOnly);
        Assert.True(dictionary2.IsFixedSize);
        Assert.True(dictionary2.IsSynchronized);
        Assert.Throws<NotSupportedException>(() => dictionary2.SyncRoot);
        
        KeyValuePair<int, int>[] buffer = new KeyValuePair<int, int>[10];
        dictionary2.CopyTo(buffer, 0);
        Assert.Equal(dictionary1, buffer);
        
        Assert.Throws<InvalidOperationException>(() => dictionary2.Remove(1));
        Assert.Throws<InvalidOperationException>(() => dictionary2.Clear());
        Assert.Throws<InvalidOperationException>(() => dictionary2.Add(55, 5));
        Assert.Throws<InvalidOperationException>(() => dictionary2[1] = 42);
        
        Assert.Throws<InvalidOperationException>(() => dictionary2[2] = 42);
        Assert.Throws<InvalidOperationException>(() => dictionary2.Remove(new KeyValuePair<int, int>(1, 42)));
    }

    [Fact]
    public static void ImmutableEquatableDictionary_Empty()
    {
        Assert.Equal(default, ImmutableEquatableDictionary.Empty<int, int>());
        var empty = ImmutableEquatableDictionary.Empty<int, int>();
        Assert.Empty(empty);
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_KeyProjection()
    {
        var dict = Enumerable.Range(1, 10).ToImmutableEquatableDictionary(i => i);
        var dict2 = Enumerable.Range(1, 10).ToDictionary(i => i);
        Assert.Equal(dict, dict2);
    }
    
    [Fact]
    public static void ImmutableEquatableDictionary_FromKeyValuePairs()
    {
        var dict = Enumerable.Range(1, 10)
            .Select(i => new KeyValuePair<int, int>(i, i))
            .ToImmutableEquatableDictionary();
        var dict2 = Enumerable.Range(1, 10).ToDictionary(i => i);
        Assert.Equal(dict, dict2);
    }
}