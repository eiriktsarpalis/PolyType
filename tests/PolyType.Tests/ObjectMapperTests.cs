using PolyType.Abstractions;
using PolyType.Examples.ObjectMapper;
using PolyType.Examples.StructuralEquality;
using Xunit;

namespace PolyType.Tests;

public abstract class ObjectMapperTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void MapperPairKeys_KeepCanonicalCaching(int length)
    {
        ITypeShape<MyLinkedList<int>> shape = providerUnderTest.Provider.GetTypeShapeOrThrow<MyLinkedList<int>>();
        MyLinkedList<int>? value = null;
        for (int i = 0; i < length; i++)
        {
            value = new() { Value = i, Next = value };
        }

        var mapper = Mapper.Create(shape, shape);
        Assert.Same(mapper, Mapper.Create(shape, shape));
        Assert.Same(mapper, Mapper.Create<MyLinkedList<int>, MyLinkedList<int>>(shape.Provider));
        MyLinkedList<int>? result = mapper(value);
        Assert.NotSame(value, result);
        Assert.Equal(value, result, StructuralEqualityComparer.Create(shape));
    }

    [Fact]
    public void MapperPairKeys_ExcludeContextualViews()
    {
        var unionShape = (IUnionTypeShape<PolymorphicClass>)providerUnderTest.Provider.GetTypeShapeOrThrow<PolymorphicClass>();
        var mapper = Mapper.Create(unionShape.BaseType, unionShape.BaseType);
        Assert.Equal(new PolymorphicClass(42), mapper(new PolymorphicClass(42)));
        Assert.Throws<NotImplementedException>(() => Mapper.Create(unionShape, unionShape));
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void MapToTheSameType_ProducesEqualCopy<T>(TestCase<T> testCase)
    {
        if (!providerUnderTest.HasConstructor(testCase) || testCase.IsUnion)
        {
            return;
        }

        if (providerUnderTest.ResolveShape(testCase) is IOptionalTypeShape { ElementType: IUnionTypeShape })
        {
            Assert.Throws<NotImplementedException>(() => GetMapperAndEqualityComparer<T>(testCase));
            return;
        }

        (Mapper<T, T> mapper, IEqualityComparer<T> comparer, ITypeShape<T> shape) = GetMapperAndEqualityComparer<T>(testCase);

        T? mappedValue = mapper(testCase.Value);

        if (!typeof(T).IsValueType && testCase.Value != null)
        {
            if (shape is IObjectTypeShape { Constructor: null, Properties: [] })
            {
                // Trivial objects without ctors or properties are not copied.
                Assert.Same((object?)mappedValue, (object?)testCase.Value);
            }
            else
            {
                Assert.NotSame((object?)mappedValue, (object?)testCase.Value);
            }
        }

        if (testCase.IsStack)
        {
            mappedValue = mapper(mappedValue);
        }

        Assert.Equal(testCase.Value, mappedValue, comparer!);
    }

#if NET
    [Fact]
    public void MapsToMatchingType()
    {
        Assert.Throws<InvalidOperationException>(() => GetMapper<WeatherForecast, WeatherForecastDTO>());
        Mapper<WeatherForecastDTO, WeatherForecast> mapper = GetMapper<WeatherForecastDTO, WeatherForecast>();

        var weatherForecastDTO = new WeatherForecastDTO
        {
            Id = "id",
            Date = DateTime.Parse("1975-01-01"),
            DatesAvailable = [DateTime.Parse("1975-01-01"), DateTime.Parse("1976-01-01")],
            Summary = "Summary",
            SummaryField = "SummaryField",
            TemperatureCelsius = 42,
            SummaryWords = ["Summary", "Words"],
            TemperatureRanges = new()
            {
                ["Range1"] = new() { Low = 1, High = 2 },
                ["Range2"] = new() { Low = 3, High = 4 },
            }
        };

        var weatherForecast = mapper(weatherForecastDTO);

        Assert.Equal(weatherForecastDTO.Date, weatherForecast.Date);
        Assert.Equal(weatherForecastDTO.DatesAvailable, weatherForecast.DatesAvailable!);
        Assert.Equal(weatherForecastDTO.TemperatureCelsius, weatherForecast.TemperatureCelsius);
        Assert.Equal(weatherForecastDTO.SummaryWords, weatherForecast.SummaryWords!);
        Assert.Equal(new Dictionary<string, HighLowTemps> { ["Range1"] = new() { High = 2 }, ["Range2"] = new() { High = 4 }, }, weatherForecast.TemperatureRanges!);
        Assert.Null(weatherForecast.UnmatchedProperty);
    }

    private Mapper<TFrom, TTo> GetMapper<TFrom, TTo>() => Mapper.Create<TFrom, TTo>(providerUnderTest.Provider);
#endif

    private (Mapper<T, T>, IEqualityComparer<T>, ITypeShape<T>) GetMapperAndEqualityComparer<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        return (Mapper.Create(shape, shape), StructuralEqualityComparer.Create(shape), shape);
    }
}

public sealed class MapperTests_Reflection() : ObjectMapperTests(ReflectionProviderUnderTest.NoEmit);
public sealed class MapperTests_ReflectionEmit() : ObjectMapperTests(ReflectionProviderUnderTest.Emit);
public sealed class MapperTests_SourceGen() : ObjectMapperTests(SourceGenProviderUnderTest.Default);