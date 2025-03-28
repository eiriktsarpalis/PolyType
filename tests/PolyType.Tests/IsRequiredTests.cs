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
        Assert.True(shape.Properties.Single(p => p.Name == nameof(HasRequiredProperty.RequiredProperty)).IsRequired);
        Assert.False(shape.Properties.Single(p => p.Name == nameof(HasRequiredProperty.NotRequiredProperty)).IsRequired);
    }

    [Fact]
    public void AttributedProperties_NoChanges()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasRequiredPropertyWithNonOverriddingAttributes));
        Assert.NotNull(shape);
        Assert.True(shape.Properties.Single(p => p.Name == nameof(HasRequiredProperty.RequiredProperty)).IsRequired);
        Assert.False(shape.Properties.Single(p => p.Name == nameof(HasRequiredProperty.NotRequiredProperty)).IsRequired);
    }

    [Fact]
    public void AttributedProperties_WithChanges()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasRequiredPropertyWithOverriddingAttributes));
        Assert.NotNull(shape);
        Assert.False(shape.Properties.Single(p => p.Name == nameof(HasRequiredProperty.RequiredProperty)).IsRequired); // Updated to check for false
        Assert.True(shape.Properties.Single(p => p.Name == nameof(HasRequiredProperty.NotRequiredProperty)).IsRequired); // Updated to check for true
    }

    [Fact]
    public void NonDefaultCtor()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructor));
        Assert.NotNull(shape);
        Assert.False(shape.Properties.Single(p => p.Name == nameof(HasNonDefaultConstructor.MyProperty)).IsRequired);
        Assert.True(shape.Constructor?.Parameters[0].IsRequired);
    }

    [Fact]
    public void NonDefaultCtor_NonRequiredProperty_WithOverride()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorAndParameterOverride));
        Assert.NotNull(shape);
        Assert.False(shape.Properties.Single(p => p.Name == nameof(HasNonDefaultConstructorAndParameterOverride.MyProperty)).IsRequired);
        Assert.False(shape.Constructor?.Parameters[0].IsRequired);
    }

    [Fact]
    public void NonDefaultCtor_NonRequiredProperty_WithShapeNoOverride()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorAndParameterNonOverride));
        Assert.NotNull(shape);
        Assert.False(shape.Properties.Single(p => p.Name == nameof(HasNonDefaultConstructorAndParameterNonOverride.MyProperty)).IsRequired);
        Assert.True(shape.Constructor?.Parameters[0].IsRequired);
    }

    [Fact]
    public void RequiredPropertyAndCtorWithDefaultValue()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorWithDefaultAndRequiredProperty));
        Assert.NotNull(shape);
        Assert.True(shape.Properties.Single(p => p.Name == nameof(HasNonDefaultConstructorWithDefaultAndRequiredProperty.MyProperty)).IsRequired);
        Assert.False(shape.Constructor?.Parameters[0].IsRequired);
    }

    [Fact]
    public void NonDefaultConstructorAndUnrelatedRequiredProperty()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorAndUnrelatedRequiredProperty));
        Assert.NotNull(shape);
        Assert.True(shape.Properties.Single(p => p.Name == nameof(HasNonDefaultConstructorAndUnrelatedRequiredProperty.RequiredProperty)).IsRequired);
        Assert.True(shape.Constructor?.Parameters[0].IsRequired);
    }

    [Fact]
    public void NonDefaultConstructorAndUnrelatedRequiredOverriddenProperty()
    {
        var shape = (IObjectTypeShape?)providerUnderTest.Provider.GetShape(typeof(HasNonDefaultConstructorAndUnrelatedRequiredOverriddenProperty));
        Assert.NotNull(shape);
        Assert.False(shape.Properties.Single(p => p.Name == nameof(HasNonDefaultConstructorAndUnrelatedRequiredOverriddenProperty.RequiredProperty)).IsRequired);
        Assert.True(shape.Constructor?.Parameters[0].IsRequired);
    }

    [GenerateShape]
    public partial class HasRequiredProperty
    {
        public required bool RequiredProperty { get; init; }

        public bool NotRequiredProperty { get; init; }
    }

    [GenerateShape]
    public partial class HasRequiredPropertyWithNonOverriddingAttributes
    {
        [PropertyShape]
        public required bool RequiredProperty { get; init; }

        [PropertyShape]
        public bool NotRequiredProperty { get; init; }
    }

    [GenerateShape]
    public partial class HasRequiredPropertyWithOverriddingAttributes
    {
        [PropertyShape(IsRequired = false)]
        public required bool RequiredProperty { get; init; }

        [PropertyShape(IsRequired = true)]
        public bool NotRequiredProperty { get; init; }
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

    [GenerateShape]
    public partial class HasNonDefaultConstructorAndParameterNonOverride
    {
        public HasNonDefaultConstructorAndParameterNonOverride([ParameterShape] int myProperty)
        {
            MyProperty = myProperty;
        }

        public int MyProperty { get; set; }
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

    [GenerateShape]
    public partial class HasNonDefaultConstructorWithDefaultAndRequiredProperty
    {
        public HasNonDefaultConstructorWithDefaultAndRequiredProperty(int myProperty = 5)
        {
            MyProperty = myProperty;
        }

        public required int MyProperty { get; set; }
    }

    [GenerateShape]
    public partial class HasNonDefaultConstructorAndUnrelatedRequiredProperty
    {
        public HasNonDefaultConstructorAndUnrelatedRequiredProperty(int unrelated)
        {
        }

        public required bool RequiredProperty { get; set; }
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

    [GenerateShape<HasRequiredProperty>]
    internal partial class Witness;

    public sealed class Reflection() : IsRequiredTests(RefectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : IsRequiredTests(RefectionProviderUnderTest.Emit);
    public sealed class SourceGen() : IsRequiredTests(new SourceGenProviderUnderTest(Witness.ShapeProvider));
}
