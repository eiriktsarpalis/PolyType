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
            ElementTypeFactory = () => throw new InvalidOperationException("ElementTypeFactory should not be called"),
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
            KeyTypeFactory = () => throw new InvalidOperationException("KeyTypeFactory should not be called"),
            ValueTypeFactory = () => expectedValueType,
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
            KeyTypeFactory = () => expectedKeyType,
            ValueTypeFactory = () => throw new InvalidOperationException("ValueTypeFactory should not be called"),
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

    [Fact]
    public static void SourceGenOptionalTypeShape_ElementType_ObsoleteProperty_HasDesiredEffect()
    {
        // Arrange
        var mockProvider = new MockTypeShapeProvider();
        var expectedElementType = new SourceGenObjectTypeShape<int>
        {
            Provider = mockProvider,
        };

        // Act - Use the obsolete property directly
#pragma warning disable CS0618 // Type or member is obsolete
        var optionalShape = new SourceGenOptionalTypeShape<int?, int>
        {
            Provider = mockProvider,
            ElementTypeFactory = () => throw new InvalidOperationException("ElementTypeFactory should not be called"),
            ElementType = expectedElementType,
            NoneConstructor = () => null,
            SomeConstructor = value => value,
            Deconstructor = (int? optional, out int value) =>
            {
                if (optional.HasValue)
                {
                    value = optional.Value;
                    return true;
                }

                value = default;
                return false;
            },
        };
#pragma warning restore CS0618

        // Assert - The obsolete property should have set the value correctly
        var actualElementType = ((IOptionalTypeShape)optionalShape).ElementType;
        Assert.Same(expectedElementType, actualElementType);
    }

    [Fact]
    public static void SourceGenSurrogateTypeShape_SurrogateType_ObsoleteProperty_HasDesiredEffect()
    {
        // Arrange
        var mockProvider = new MockTypeShapeProvider();
        var expectedSurrogateType = new SourceGenObjectTypeShape<string>
        {
            Provider = mockProvider,
        };

        // Act - Use the obsolete property directly
#pragma warning disable CS0618 // Type or member is obsolete
        var surrogateShape = new SourceGenSurrogateTypeShape<int, string>
        {
            Provider = mockProvider,
            SurrogateTypeFactory = () => throw new InvalidOperationException("SurrogateTypeFactory should not be called"),
            SurrogateType = expectedSurrogateType,
            Marshaler = new TestMarshaler<int, string>(i => i.ToString(), s => int.Parse(s!)),
        };
#pragma warning restore CS0618

        // Assert - The obsolete property should have set the value correctly
        var actualSurrogateType = ((ISurrogateTypeShape)surrogateShape).SurrogateType;
        Assert.Same(expectedSurrogateType, actualSurrogateType);
    }

    [Fact]
    public static void SourceGenUnionTypeShape_BaseType_ObsoleteProperty_HasDesiredEffect()
    {
        // Arrange
        var mockProvider = new MockTypeShapeProvider();
        var expectedBaseType = new SourceGenObjectTypeShape<object>
        {
            Provider = mockProvider,
        };

        // Act - Use the obsolete property directly
#pragma warning disable CS0618 // Type or member is obsolete
        var unionShape = new SourceGenUnionTypeShape<object>
        {
            Provider = mockProvider,
            BaseTypeFactory = () => throw new InvalidOperationException("BaseTypeFactory should not be called"),
            BaseType = expectedBaseType,
            UnionCasesFactory = () => [],
            GetUnionCaseIndex = (ref object _) => 0,
        };
#pragma warning restore CS0618

        // Assert - The obsolete property should have set the value correctly
        var actualBaseType = ((IUnionTypeShape)unionShape).BaseType;
        Assert.Same(expectedBaseType, actualBaseType);
    }

    [Fact]
    public static void SourceGenUnionCaseShape_UnionCaseType_ObsoleteProperty_HasDesiredEffect()
    {
        // Arrange
        var mockProvider = new MockTypeShapeProvider();
        var expectedUnionCaseType = new SourceGenObjectTypeShape<string>
        {
            Provider = mockProvider,
        };

        // Act - Use the obsolete property directly
#pragma warning disable CS0618 // Type or member is obsolete
        var unionCaseShape = new SourceGenUnionCaseShape<string, object>
        {
            UnionCaseTypeFactory = () => throw new InvalidOperationException("UnionCaseTypeFactory should not be called"),
            UnionCaseType = expectedUnionCaseType,
            Name = "StringCase",
            Tag = 0,
            Index = 0,
            Marshaler = new TestMarshaler<string, object>(s => s, o => (string)o!),
        };
#pragma warning restore CS0618

        // Assert - The obsolete property should have set the value correctly
        var actualUnionCaseType = ((IUnionCaseShape)unionCaseShape).UnionCaseType;
        Assert.Same(expectedUnionCaseType, actualUnionCaseType);
    }

    [Fact]
    public static void SourceGenEnumTypeShape_UnderlyingType_ObsoleteProperty_HasDesiredEffect()
    {
        // Arrange
        var mockProvider = new MockTypeShapeProvider();
        var expectedUnderlyingType = new SourceGenObjectTypeShape<int>
        {
            Provider = mockProvider,
        };

        // Act - Use the obsolete property directly
#pragma warning disable CS0618 // Type or member is obsolete
        var enumShape = new SourceGenEnumTypeShape<DayOfWeek, int>
        {
            Provider = mockProvider,
            UnderlyingTypeFactory = () => throw new InvalidOperationException("UnderlyingTypeFactory should not be called"),
            UnderlyingType = expectedUnderlyingType,
            Members = new Dictionary<string, int>(),
        };
#pragma warning restore CS0618

        // Assert - The obsolete property should have set the value correctly
        var actualUnderlyingType = ((IEnumTypeShape)enumShape).UnderlyingType;
        Assert.Same(expectedUnderlyingType, actualUnderlyingType);
    }

    [Fact]
    public static void SourceGenFunctionTypeShape_ReturnType_ObsoleteProperty_HasDesiredEffect()
    {
        // Arrange
        var mockProvider = new MockTypeShapeProvider();
        var expectedReturnType = new SourceGenObjectTypeShape<int>
        {
            Provider = mockProvider,
        };

        // Act - Use the obsolete property directly
#pragma warning disable CS0618 // Type or member is obsolete
        var functionShape = new SourceGenFunctionTypeShape<Func<int>, EmptyArgumentState, int>
        {
            Provider = mockProvider,
            ReturnTypeFactory = () => throw new InvalidOperationException("ReturnTypeFactory should not be called"),
            ReturnType = expectedReturnType,
            ArgumentStateConstructor = () => EmptyArgumentState.Instance,
            FunctionInvoker = (ref Func<int> func, ref EmptyArgumentState state) => new ValueTask<int>(func()),
        };
#pragma warning restore CS0618

        // Assert - The obsolete property should have set the value correctly
        var actualReturnType = ((IFunctionTypeShape)functionShape).ReturnType;
        Assert.Same(expectedReturnType, actualReturnType);
    }

    private sealed class MockTypeShapeProvider : ITypeShapeProvider
    {
        public ITypeShape? GetTypeShape(Type type) => null;
    }

    private sealed class TestMarshaler<T, TSurrogate>(Func<T?, TSurrogate?> marshal, Func<TSurrogate?, T?> unmarshal) : IMarshaler<T, TSurrogate>
    {
        public TSurrogate? Marshal(T? value) => marshal(value);
        public T? Unmarshal(TSurrogate? surrogate) => unmarshal(surrogate);
    }
}
