namespace PolyType.Tests;

public static class CommonHelpersTests
{
    [Theory]
    // Enumerate at execution time so test-case serialization cannot alter unpaired surrogates.
    [MemberData(nameof(GetEquivalentCamelCaseLabels), DisableDiscoveryEnumeration = true)]
    public static void CamelCaseInvariantComparer_EquivalentLabels(string expected, string input)
    {
        var comparer = CommonHelpers.CamelCaseInvariantComparer.Instance;

        Assert.True(comparer.Equals(expected, input));
        Assert.True(comparer.Equals(input, expected));
        Assert.True(comparer.Equals(expected, expected));
        Assert.True(comparer.Equals(input, input));
        Assert.Equal(comparer.GetHashCode(expected), comparer.GetHashCode(input));
    }

    [Theory]
    [MemberData(nameof(GetDistinctCamelCaseLabels), DisableDiscoveryEnumeration = true)]
    public static void CamelCaseInvariantComparer_DistinctLabels(string left, string right)
    {
        var comparer = CommonHelpers.CamelCaseInvariantComparer.Instance;

        Assert.False(comparer.Equals(left, right));
        Assert.False(comparer.Equals(right, left));
        Assert.True(comparer.Equals(left, left));
        Assert.True(comparer.Equals(right, right));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, "", false)]
    [InlineData(null, "Value", false)]
    public static void CamelCaseInvariantComparer_NullOperands(string? left, string? right, bool expected)
    {
        var comparer = CommonHelpers.CamelCaseInvariantComparer.Instance;

        Assert.Equal(expected, comparer.Equals(left, right));
        Assert.Equal(expected, comparer.Equals(right, left));
    }

    [Theory]
    [InlineData("ILDelta", "ilDelta", "IlDelta")]
    [InlineData("URLValue", "urlValue", "UrlValue")]
    [InlineData("IsJSONProperty", "isJsonProperty", "IsJsonPROPERTY")]
    [InlineData("BUILDING PROPERTY", "building Property", "Building Property")]
    [InlineData("I Phone", "i Phone", "i phone")]
    [InlineData("A B", "a B", "a b")]
    public static void CamelCaseInvariantComparer_TransitiveEquality(string first, string second, string third)
    {
        var comparer = CommonHelpers.CamelCaseInvariantComparer.Instance;

        Assert.True(comparer.Equals(first, second));
        Assert.True(comparer.Equals(second, third));
        Assert.True(comparer.Equals(first, third));
        Assert.Equal(comparer.GetHashCode(first), comparer.GetHashCode(second));
        Assert.Equal(comparer.GetHashCode(first), comparer.GetHashCode(third));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(1024)]
    [InlineData(4096)]
    public static void CamelCaseInvariantComparer_LongLabels(int length)
    {
        var comparer = CommonHelpers.CamelCaseInvariantComparer.Instance;
        string suffix = new('x', length);
        string left = "URLValue" + suffix;
        string right = "urlValue" + suffix;

        Assert.True(comparer.Equals(left, right));
        Assert.True(comparer.Equals(right, left));
        Assert.Equal(comparer.GetHashCode(left), comparer.GetHashCode(right));
        Assert.False(comparer.Equals(left, right + "Z"));
        Assert.False(comparer.Equals(left + "Z", right));
    }

    public static IEnumerable<object[]> GetEquivalentCamelCaseLabels()
    {
        // Regular member names exercised by System.Text.Json's NamingPolicyUnitTests.GetValidMemberNames.
        // https://github.com/dotnet/runtime/blob/a4106c734319b45fd6576e0a6dcd8bbdd08f8e7c/src/libraries/System.Text.Json/tests/Common/PropertyNameTests.cs
        yield return ["name", "Name"];
        yield return ["myObject", "MyObject"];
        yield return ["myValue", "MyValue"];
        yield return ["myFirstProperty", "MyFirstProperty"];
        yield return ["mySecondProperty", "MySecondProperty"];
        yield return ["propertyNameWithWhitespace", "PropertyNameWithWhitespace"];

        // Space-preserving examples from https://github.com/AArnott/Nerdbank.MessagePack/blob/aa43461cb4cc7d3c73eb61b1605fee073a6df299/test/Nerdbank.MessagePack.TUnit/MessagePackNamingPolicyTests.cs#L13-L43
        yield return ["urlValue", "URLValue"];
        yield return ["url", "URL"];
        yield return ["id", "ID"];
        yield return ["i", "I"];
        yield return ["", ""];
        yield return ["\U0001F600\u845B\U0001F004", "\U0001F600\u845B\U0001F004"];
        yield return ["\u03AC\u03BB\u03C6\u03B1\u0392\u03AE\u03C4\u03B1\u0393\u03AC\u03BC\u03BC\u03B1", "\u0386\u03BB\u03C6\u03B1\u0392\u03AE\u03C4\u03B1\u0393\u03AC\u03BC\u03BC\u03B1"];
        yield return ["\U00010400\U00010428\U00010428\U00010400\U00010428\U00010428", "\U00010400\U00010428\U00010428\U00010400\U00010428\U00010428"];
        yield return ["\uDE00\uD83D", "\uDE00\uD83D"];
        yield return ["person", "Person"];
        yield return ["iPhone", "iPhone"];
        yield return ["iPhone", "IPhone"];
        yield return ["isCia", "IsCIA"];
        yield return ["vmQ", "VmQ"];
        yield return ["xml2Json", "Xml2Json"];
        yield return ["snAkEcAsE", "SnAkEcAsE"];
        yield return ["snA__kEcAsE", "SnA__kEcAsE"];
        yield return ["isJsonProperty", "IsJSONProperty"];
        yield return ["shouting_case", "SHOUTING_CASE"];
        yield return ["9999-12-31T23:59:59.9999999Z", "9999-12-31T23:59:59.9999999Z"];
        yield return ["building", "BUILDING"];

        yield return ["i Phone", "I Phone"];
        yield return ["i  Phone", "I  Phone"];
        yield return [" iPhone", " IPhone"];
        yield return [" iPhone ", " IPhone "];
        yield return ["snA__ kEcAsE", "SnA__ kEcAsE"];
        yield return ["already_snake_case_ ", "already_snake_case_ "];
        yield return ["hi!! this Is Text. time To Test.", "Hi!! This is text. Time to test."];
        yield return ["building Property", "BUILDING Property"];
        yield return ["building Property", "Building Property"];
        yield return ["building Property", "BUILDING PROPERTY"];
        yield return [" ", " "];
        yield return ["\u00A0", "\u00A0"];

        yield return ["ilDelta", "ILDelta"];
        yield return ["xmlHttpRequest", "XMLHttpRequest"];
        yield return ["sha512Hash", "SHA512Hash"];
        yield return ["valueId", "ValueID"];
        yield return ["\U00010400urlValue", "\U00010400URLValue"];
        yield return ["a\U00010428b", "A\U00010428B"];
        yield return ["\uD800value", "\uD800Value"];
        yield return ["value\uDC00", "Value\uDC00"];
        yield return ["\uD800", "\uD800"];
        yield return ["\uDC00", "\uDC00"];
    }

    public static IEnumerable<object[]> GetDistinctCamelCaseLabels()
    {
        // These space-removing Nerdbank.MessagePack examples are intentionally unequal here.
        yield return ["iPhone", "I Phone"];
        yield return ["iPhone", "I  Phone"];
        yield return ["iPhone", " IPhone"];
        yield return ["iPhone", " IPhone "];
        yield return ["snA__kEcAsE", "SnA__ kEcAsE"];
        yield return ["already_snake_case_", "already_snake_case_ "];
        yield return ["hi!!thisIsText.timeToTest.", "Hi!! This is text. Time to test."];
        yield return ["buildingProperty", "BUILDING Property"];
        yield return ["buildingProperty", "Building Property"];
        yield return ["buildingProperty", "BUILDING PROPERTY"];

        yield return ["", " \u00A0\u2002 "];
        yield return ["", " "];
        yield return [" ", "  "];
        yield return [" ", "\u00A0"];
        yield return ["\u00A0", "\u2002"];
        yield return [" a", "a "];
        yield return ["I Phone", "I\u00A0Phone"];
        yield return [" A", "A "];
        yield return ["ILDelta", "iLDelta"];
        yield return ["ilDelta", "iLDelta"];
        yield return ["ID", "iD"];
        yield return ["Value", "ValUE"];
        yield return ["isjsonProperty", "IsJSONProperty"];
        yield return ["urlvalue", "URLValue"];
        yield return ["xml2json", "Xml2Json"];
        yield return ["snake_case", "snake__case"];
        yield return ["iPhone", "iphone"];
        yield return ["\U00010400", "\U00010428"];
        yield return ["\U00010400Name", "\U00010428Name"];
        yield return ["\U00010400\U00010428", "\U00010428\U00010400"];
        yield return ["\uDE00\uD83D", "\uD83D\uDE00"];
        yield return ["\uD800", "\uD801"];
        yield return ["\uDC00", "\uDC01"];
        yield return ["\uD800", "\uDC00"];
        yield return ["\uD800Value", "\uD801Value"];
        yield return ["\uDC00Value", "\uDC01Value"];
    }

    [Theory]
    [MemberData(nameof(GetDirectedAcyclicGraphs))]
    public static void TraverseGraphWithTopologicalSort_VisitsParentsBeforeChildren(int[][] graph)
    {
        int[] sortedNodes = CommonHelpers.TraverseGraphWithTopologicalSort(0, node => graph[node]);

        Assert.Equal(Enumerable.Range(0, graph.Length), sortedNodes.OrderBy(node => node));
        for (int node = 0; node < graph.Length; node++)
        {
            foreach (int child in graph[node])
            {
                Assert.True(Array.IndexOf(sortedNodes, node) < Array.IndexOf(sortedNodes, child));
            }
        }
    }

    public static IEnumerable<object[]> GetDirectedAcyclicGraphs()
    {
        yield return [new int[][] { [] }];
        yield return [new int[][] { [1], [] }];
        yield return [new int[][] { [1], [2], [] }];
        yield return [new int[][] { [1, 2], [3], [3], [] }];
        yield return [new int[][] { [1, 2], [3], [], [4], [] }];
    }
}
