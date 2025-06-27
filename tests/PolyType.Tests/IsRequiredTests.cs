using PolyType;
using PolyType.Tests;

namespace PolyType.Tests;

public abstract partial class IsRequiredTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void UnattributedProperties()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasRequiredProperty));
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
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasRequiredPropertyWithNonOverriddingAttributes));
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
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasRequiredPropertyWithOverriddingAttributes));
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
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasPropertyRequiredByAttribute));
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
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasPropertyRequiredButNotByAttribute));
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
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructor));
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
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorAndParameterOverride));
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
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorAndParameterNonOverride));
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
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorWithDefaultAndRequiredProperty));
        Assert.NotNull(shape);
        Assert.NotNull(shape.Constructor);
        var parameter = shape.Constructor.Parameters.Single(p => p.Name == nameof(HasNonDefaultConstructorWithDefaultAndRequiredProperty.MyProperty) && p.Kind == ParameterKind.MethodParameter);
        var member = shape.Constructor.Parameters.Single(p => p.Name == nameof(HasNonDefaultConstructorWithDefaultAndRequiredProperty.MyProperty) && p.Kind == ParameterKind.MemberInitializer);
        Assert.False(parameter.IsRequired);
        Assert.True(member.IsRequired);
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
    public void NonDefaultConstructorAndUnrelatedRequiredProperty()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorAndUnrelatedRequiredProperty));
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
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorAndUnrelatedRequiredOverriddenProperty));
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
    public sealed class SourceGen() : IsRequiredTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
