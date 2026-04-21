using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json.Serialization;
using PolyType.Abstractions;
using PolyType.Examples.ConfigurationBinder;
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.StructuralEquality;
using Xunit;

namespace PolyType.Tests;

public abstract class ConfigurationBinderTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void BoundResultEqualsOriginalValue<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        Func<IConfiguration, T?> binder = ConfigurationBinderTS.Create(shape);
        IEqualityComparer<T> comparer = StructuralEqualityComparer.Create(shape);
        (IConfiguration configuration, string json) = CreateConfiguration(testCase, shape);

        // In Microsoft.Extensions.Configuration 10.x, JSON `null` literals and `{}`
        // both surface as IConfigurationSection.Value == null with no children, so
        // values whose JSON serialization collapses to one of these round-trip as
        // the default of T. See:
        // https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/configuration-null-values-preserved
        bool sectionIsNull = json is "null" or "{}";

        if (!providerUnderTest.HasConstructor(testCase) && !sectionIsNull)
        {
            Assert.Throws<NotSupportedException>(() => binder(configuration));
            return;
        }

        T? result = binder(configuration);

        if (sectionIsNull)
        {
            Assert.Equal(default, result);
        }
        else if (json.Contains("{}"))
        {
            // Nested empty objects are indistinguishable from nulls in MEC 10.x
            // so we settle for verifying that the binder runs without throwing.
        }
        else
        {
            Assert.Equal(testCase.Value, result, comparer!);
        }
    }
    
    private static (IConfiguration Configuration, string Json) CreateConfiguration<T>(TestCase<T> testCase, ITypeShape<T> shape)
    {
        JsonConverter<T> converter = JsonSerializerTS.CreateConverter(shape);
        string json = converter.Serialize(testCase.Value);
        if (testCase.IsStack)
        {
            T? value = converter.Deserialize(json.AsSpan());
            json = converter.Serialize(value);
        }
        
        string rootJson = $$"""{ "Root" : {{json}} }""";
        var builder = new ConfigurationBuilder();
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(rootJson));
        builder.AddJsonStream(stream);
        return (builder.Build().GetSection("Root"), json);
    }
}

public sealed class ConfigurationBinderTests_Reflection() : ConfigurationBinderTests(ReflectionProviderUnderTest.NoEmit);
public sealed class ConfigurationBinderTests_ReflectionEmit() : ConfigurationBinderTests(ReflectionProviderUnderTest.Emit);
public sealed class ConfigurationBinderTests_SourceGen() : ConfigurationBinderTests(SourceGenProviderUnderTest.Default);