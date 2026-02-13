using PolyType.SourceGenModel;

namespace PolyType.Tests.SourceGenModel;

public static class ObsoletePropertyTests
{
    [Fact]
    public static void SourceGenEnumerableTypeShape_ElementType_ObsoleteProperty_HasDesiredEffect()
    {
        // Arrange
        var mockProvider = new MockTypeShapeProvider();
        var expectedElementType = new SourceGenObjectTypeShape<int>
        {
            Provider = mockProvider,
        };

        // Act - Use the obsolete property directly
#pragma warning disable CS0618 // Type or member is obsolete
        var enumerableShape = new SourceGenEnumerableTypeShape<List<int>, int>
        {
            Provider = mockProvider,
            ElementTypeFunc = () => throw new InvalidOperationException("ElementTypeFunc should not be called"),
            ElementType = expectedElementType,
            Rank = 1,
            GetEnumerable = list => list,
            SupportedComparer = CollectionComparerOptions.None,
            ConstructionStrategy = CollectionConstructionStrategy.Mutable,
            DefaultConstructor = (in CollectionConstructionOptions<int> _) => [],
            Appender = (ref List<int> list, int value) => { list.Add(value); return true; },
        };
#pragma warning restore CS0618

        // Assert - The obsolete property should have set the value correctly
        var actualElementType = ((IEnumerableTypeShape)enumerableShape).ElementType;
        Assert.Same(expectedElementType, actualElementType);
    }

    [Fact]
    public static void SourceGenDictionaryTypeShape_KeyType_ObsoleteProperty_HasDesiredEffect()
    {
        // Arrange
        var mockProvider = new MockTypeShapeProvider();
        var expectedKeyType = new SourceGenObjectTypeShape<string>
        {
            Provider = mockProvider,
        };
        var expectedValueType = new SourceGenObjectTypeShape<int>
        {
            Provider = mockProvider,
        };

        // Act - Use the obsolete property directly
#pragma warning disable CS0618 // Type or member is obsolete
        var dictionaryShape = new SourceGenDictionaryTypeShape<Dictionary<string, int>, string, int>
        {
            Provider = mockProvider,
            KeyTypeFunc = () => throw new InvalidOperationException("KeyTypeFunc should not be called"),
            ValueTypeFunc = () => expectedValueType,
            KeyType = expectedKeyType,
            GetDictionary = dict => dict,
            SupportedComparer = CollectionComparerOptions.None,
            ConstructionStrategy = CollectionConstructionStrategy.Mutable,
            DefaultConstructor = (in CollectionConstructionOptions<string> _) => [],
            OverwritingInserter = (ref Dictionary<string, int> dict, string key, int value) => { dict[key] = value; return true; },
        };
#pragma warning restore CS0618

        // Assert - The obsolete property should have set the value correctly
        var actualKeyType = ((IDictionaryTypeShape)dictionaryShape).KeyType;
        Assert.Same(expectedKeyType, actualKeyType);
    }

    [Fact]
    public static void SourceGenDictionaryTypeShape_ValueType_ObsoleteProperty_HasDesiredEffect()
    {
        // Arrange
        var mockProvider = new MockTypeShapeProvider();
        var expectedKeyType = new SourceGenObjectTypeShape<string>
        {
            Provider = mockProvider,
        };
        var expectedValueType = new SourceGenObjectTypeShape<int>
        {
            Provider = mockProvider,
        };

        // Act - Use the obsolete property directly
#pragma warning disable CS0618 // Type or member is obsolete
        var dictionaryShape = new SourceGenDictionaryTypeShape<Dictionary<string, int>, string, int>
        {
            Provider = mockProvider,
            KeyTypeFunc = () => expectedKeyType,
            ValueTypeFunc = () => throw new InvalidOperationException("ValueTypeFunc should not be called"),
            ValueType = expectedValueType,
            GetDictionary = dict => dict,
            SupportedComparer = CollectionComparerOptions.None,
            ConstructionStrategy = CollectionConstructionStrategy.Mutable,
            DefaultConstructor = (in CollectionConstructionOptions<string> _) => [],
            OverwritingInserter = (ref Dictionary<string, int> dict, string key, int value) => { dict[key] = value; return true; },
        };
#pragma warning restore CS0618

        // Assert - The obsolete property should have set the value correctly
        var actualValueType = ((IDictionaryTypeShape)dictionaryShape).ValueType;
        Assert.Same(expectedValueType, actualValueType);
    }

    private sealed class MockTypeShapeProvider : ITypeShapeProvider
    {
        public ITypeShape? GetTypeShape(Type type) => null;
    }
}
