using PolyType;
using PolyType.Tests;

namespace PolyType.Tests;

public abstract partial class IsRequiredTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void UnattributedProperties()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasRequiredProperty));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.True(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasRequiredProperty.RequiredProperty)).IsRequired);
        Assert.False(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasRequiredProperty.NotRequiredProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasRequiredProperty
    {
        public required bool RequiredProperty { get; init; }

        public bool NotRequiredProperty { get; init; }
    }

    [Fact]
    public void AttributedProperties_NoChanges()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasRequiredPropertyWithNonOverriddingAttributes));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.True(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasRequiredPropertyWithNonOverriddingAttributes.RequiredProperty)).IsRequired);
        Assert.False(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasRequiredPropertyWithNonOverriddingAttributes.NotRequiredProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasRequiredPropertyWithNonOverriddingAttributes
    {
        [PropertyShape]
        public required bool RequiredProperty { get; init; }

        [PropertyShape]
        public bool NotRequiredProperty { get; init; }
    }

    [Fact]
    public void AttributedProperties_WithChanges()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasRequiredPropertyWithOverriddingAttributes));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.False(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasRequiredPropertyWithOverriddingAttributes.RequiredProperty)).IsRequired);
        Assert.True(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasRequiredPropertyWithOverriddingAttributes.NotRequiredProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasRequiredPropertyWithOverriddingAttributes
    {
        [PropertyShape(IsRequired = false)]
        public required bool RequiredProperty { get; init; }

        [PropertyShape(IsRequired = true)]
        public bool NotRequiredProperty { get; init; }
    }

    [Fact]
    public void PropertyRequiredByAttribute()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasPropertyRequiredByAttribute));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.True(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasPropertyRequiredByAttribute.RequiredProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasPropertyRequiredByAttribute
    {
        [PropertyShape(IsRequired = true)]
        public bool RequiredProperty { get; set; }
    }

    [Fact]
    public void PropertyRequiredButNotByAttribute()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasPropertyRequiredButNotByAttribute));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.False(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasPropertyRequiredButNotByAttribute.RequiredProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasPropertyRequiredButNotByAttribute
    {
        [PropertyShape(IsRequired = false)]
        public required bool RequiredProperty { get; init; }
    }

    [Fact]
    public void NonDefaultCtor()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasNonDefaultConstructor));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.True(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasNonDefaultConstructor.MyProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasNonDefaultConstructor
    {
        public HasNonDefaultConstructor(int myProperty)
        {
            MyProperty = myProperty;
        }

        public int MyProperty { get; set; }
    }

    [Fact]
    public void NonDefaultCtor_NonRequiredProperty_WithOverride()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasNonDefaultConstructorAndParameterOverride));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.False(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasNonDefaultConstructorAndParameterOverride.MyProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasNonDefaultConstructorAndParameterOverride
    {
        public HasNonDefaultConstructorAndParameterOverride([ParameterShape(IsRequired = false)] int myProperty)
        {
            MyProperty = myProperty;
        }

        public int MyProperty { get; set; }
    }

    [Fact]
    public void NonDefaultCtor_NonRequiredProperty_WithShapeNoOverride()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasNonDefaultConstructorAndParameterNonOverride));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.True(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasNonDefaultConstructorAndParameterNonOverride.MyProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasNonDefaultConstructorAndParameterNonOverride
    {
        public HasNonDefaultConstructorAndParameterNonOverride([ParameterShape] int myProperty)
        {
            MyProperty = myProperty;
        }

        public int MyProperty { get; set; }
    }

    [Fact]
    public void RequiredPropertyAndCtorWithDefaultValue()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasNonDefaultConstructorWithDefaultAndRequiredProperty));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        IParameterShape parameter = Assert.Single(shape.Constructor.Parameters, p => p.Name == nameof(HasNonDefaultConstructorWithDefaultAndRequiredProperty.MyProperty));
        Assert.Equal(ParameterKind.MethodParameter, parameter.Kind);
        Assert.False(parameter.IsRequired);
        Assert.True(parameter.HasDefaultValue);
    }

    [GenerateShape]
    public partial class HasNonDefaultConstructorWithDefaultAndRequiredProperty
    {
        public HasNonDefaultConstructorWithDefaultAndRequiredProperty(int myProperty = 5)
        {
            MyProperty = myProperty;
        }

        public required int MyProperty { get; set; }
    }

    [Fact]
    public void RequiredPropertyAndRequiredConstructorParameter()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasRequiredPropertyAndRequiredConstructorParameter));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);

        Assert.Equal(1, shape.Constructor.Parameters.Count(p => p.Name == nameof(HasRequiredPropertyAndRequiredConstructorParameter.RequiredString)));
        Assert.Contains(shape.Constructor.Parameters, p =>
            p.Name == nameof(HasRequiredPropertyAndRequiredConstructorParameter.RequiredString) &&
            p.Kind == ParameterKind.MethodParameter &&
            p.IsRequired);
    }

    [GenerateShape]
    public partial class HasRequiredPropertyAndRequiredConstructorParameter
    {
        public HasRequiredPropertyAndRequiredConstructorParameter(string requiredString)
        {
            RequiredString = requiredString;
        }

        public required string RequiredString { get; set; }
    }

    [Theory]
    [MemberData(nameof(GetConstructorParameterRequirednessCases))]
    public void ConstructorParameterRequirednessMatrix(Type type, string parameterName, bool isRequired, bool hasDefaultValue)
    {
        IObjectTypeShape shape = Assert.IsAssignableFrom<IObjectTypeShape>(providerUnderTest.Provider.GetTypeShape(type));
        Assert.NotNull(shape.Constructor);

        IParameterShape parameter = Assert.Single(shape.Constructor.Parameters, p => p.Name == parameterName);
        Assert.Equal(ParameterKind.MethodParameter, parameter.Kind);

        if (isRequired)
        {
            Assert.True(parameter.IsRequired);
        }
        else
        {
            Assert.False(parameter.IsRequired);
        }

        if (hasDefaultValue)
        {
            Assert.True(parameter.HasDefaultValue);
        }
        else
        {
            Assert.False(parameter.HasDefaultValue);
        }
    }

    public static TheoryData<Type, string, bool, bool> GetConstructorParameterRequirednessCases()
        => new()
        {
            { typeof(OptionalPropertyWithRequiredParameter), nameof(OptionalPropertyWithRequiredParameter.Value), true, false },
            { typeof(OptionalPropertyWithDefaultParameter), nameof(OptionalPropertyWithDefaultParameter.Value), false, true },
            { typeof(RequiredPropertyWithRequiredParameter), nameof(RequiredPropertyWithRequiredParameter.Value), true, false },
            { typeof(RequiredPropertyWithDefaultParameter), nameof(RequiredPropertyWithDefaultParameter.Value), false, true },
            { typeof(PropertyShapeRequiredWithDefaultParameter), nameof(PropertyShapeRequiredWithDefaultParameter.Value), true, true },
            { typeof(RequiredPropertyShapeFalseWithRequiredParameter), nameof(RequiredPropertyShapeFalseWithRequiredParameter.Value), false, false },
        };

    [GenerateShape]
    public partial class OptionalPropertyWithRequiredParameter
    {
        public OptionalPropertyWithRequiredParameter(string value)
        {
            Value = value;
        }

        public string Value { get; set; }
    }

    [GenerateShape]
    public partial class OptionalPropertyWithDefaultParameter
    {
        public OptionalPropertyWithDefaultParameter(string? value = null)
        {
            Value = value;
        }

        public string? Value { get; set; }
    }

    [GenerateShape]
    public partial class RequiredPropertyWithRequiredParameter
    {
        public RequiredPropertyWithRequiredParameter(string value)
        {
            Value = value;
        }

        public required string Value { get; set; }
    }

    [GenerateShape]
    public partial class RequiredPropertyWithDefaultParameter
    {
        public RequiredPropertyWithDefaultParameter(string? value = null)
        {
            Value = value;
        }

        public required string? Value { get; set; }
    }

    [GenerateShape]
    public partial class PropertyShapeRequiredWithDefaultParameter
    {
        public PropertyShapeRequiredWithDefaultParameter(string? value = null)
        {
            Value = value;
        }

        [PropertyShape(IsRequired = true)]
        public string? Value { get; set; }
    }

    [GenerateShape]
    public partial class RequiredPropertyShapeFalseWithRequiredParameter
    {
        public RequiredPropertyShapeFalseWithRequiredParameter(string value)
        {
            Value = value;
        }

        [PropertyShape(IsRequired = false)]
        public required string Value { get; set; }
    }

    [Fact]
    public void NonDefaultConstructorAndUnrelatedRequiredProperty()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasNonDefaultConstructorAndUnrelatedRequiredProperty));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.True(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasNonDefaultConstructorAndUnrelatedRequiredProperty.RequiredProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasNonDefaultConstructorAndUnrelatedRequiredProperty
    {
        public HasNonDefaultConstructorAndUnrelatedRequiredProperty(int unrelated)
        {
        }

        public required bool RequiredProperty { get; set; }
    }

    [Fact]
    public void NonDefaultConstructorAndUnrelatedRequiredOverriddenProperty()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetTypeShape(typeof(HasNonDefaultConstructorAndUnrelatedRequiredOverriddenProperty));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        Assert.False(shape.Constructor.Parameters.Single(p => p.Name == nameof(HasNonDefaultConstructorAndUnrelatedRequiredOverriddenProperty.RequiredProperty)).IsRequired);
    }

    [GenerateShape]
    public partial class HasNonDefaultConstructorAndUnrelatedRequiredOverriddenProperty
    {
        public HasNonDefaultConstructorAndUnrelatedRequiredOverriddenProperty(int unrelated)
        {
        }

        [PropertyShape(IsRequired = false)]
        public required bool RequiredProperty { get; set; }
    }

    [GenerateShapeFor<HasRequiredProperty>]
    internal partial class Witness;

    public sealed class Reflection() : IsRequiredTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : IsRequiredTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : IsRequiredTests(new SourceGenProviderUnderTest(Witness.GeneratedTypeShapeProvider));
}
