namespace PolyType.Tests;

/// <summary>
/// Verifies that member accessors surface exceptions thrown by user code without wrapping them
/// in a <see cref="System.Reflection.TargetInvocationException"/>. The Reflection.Emit accessor
/// emits direct calls and never wrapped exceptions; this exercises the reflection accessor (and
/// the source generator) to guarantee the same behavior. See dotnet/runtime#130503.
/// </summary>
public abstract partial class MemberAccessorExceptionTests(ProviderUnderTest providerUnderTest)
{
    [Fact]
    public void DefaultConstructor_PropagatesUserExceptionUnwrapped()
    {
        var shape = (IObjectTypeShape<ThrowingDefaultConstructor>)providerUnderTest.Provider.GetTypeShapeOrThrow<ThrowingDefaultConstructor>();
        Func<ThrowingDefaultConstructor>? constructor = shape.GetDefaultConstructor();
        Assert.NotNull(constructor);

        var ex = Assert.Throws<InvalidOperationException>(() => constructor());
        Assert.Equal(ThrowingMember.Message, ex.Message);
    }

    [Fact]
    public void ParameterizedConstructor_PropagatesUserExceptionUnwrapped()
    {
        var shape = (IObjectTypeShape<ThrowingParameterizedConstructor>)providerUnderTest.Provider.GetTypeShapeOrThrow<ThrowingParameterizedConstructor>();
        Assert.NotNull(shape.Constructor);

        var ex = Assert.Throws<InvalidOperationException>(() => shape.Constructor.Accept(MemberInvoker.Instance));
        Assert.Equal(ThrowingMember.Message, ex.Message);
    }

    [Fact]
    public void PropertyGetter_PropagatesUserExceptionUnwrapped()
    {
        var shape = (IObjectTypeShape<ThrowingGetter>)providerUnderTest.Provider.GetTypeShapeOrThrow<ThrowingGetter>();
        IPropertyShape property = shape.Properties.Single(p => p.Name == nameof(ThrowingGetter.Value));

        var ex = Assert.Throws<InvalidOperationException>(() => property.Accept(MemberInvoker.Instance, new ThrowingGetter()));
        Assert.Equal(ThrowingMember.Message, ex.Message);
    }

    [Fact]
    public void PropertySetter_PropagatesUserExceptionUnwrapped()
    {
        var shape = (IObjectTypeShape<ThrowingSetter>)providerUnderTest.Provider.GetTypeShapeOrThrow<ThrowingSetter>();
        IPropertyShape property = shape.Properties.Single(p => p.Name == nameof(ThrowingSetter.Value));

        var ex = Assert.Throws<InvalidOperationException>(() => property.Accept(MemberInvoker.Instance, new ThrowingSetter()));
        Assert.Equal(ThrowingMember.Message, ex.Message);
    }

    private sealed class MemberInvoker : TypeShapeVisitor
    {
        public static readonly MemberInvoker Instance = new();

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            TArgumentState argumentState = constructor.GetArgumentStateConstructor()();
            foreach (IParameterShape parameter in constructor.Parameters)
            {
                argumentState = (TArgumentState)parameter.Accept(this, argumentState)!;
            }

            return constructor.GetParameterizedConstructor()(ref argumentState);
        }

        public override object? VisitParameter<TArgumentState, TParameter>(IParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            var argumentState = (TArgumentState)state!;
            parameter.GetSetter()(ref argumentState, default!);
            return argumentState;
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            var obj = (TDeclaringType)state!;
            TPropertyType value = default!;
            if (property.HasGetter)
            {
                value = property.GetGetter()(ref obj);
            }

            if (property.HasSetter)
            {
                property.GetSetter()(ref obj, value);
            }

            return null;
        }
    }

    private static class ThrowingMember
    {
        public const string Message = "Thrown from user code.";
    }

    public sealed class ThrowingDefaultConstructor
    {
        public ThrowingDefaultConstructor() => throw new InvalidOperationException(ThrowingMember.Message);
    }

    public sealed class ThrowingParameterizedConstructor
    {
        public ThrowingParameterizedConstructor(int value) => throw new InvalidOperationException(ThrowingMember.Message);

        public int Value { get; }
    }

    public sealed class ThrowingGetter
    {
        public int Value => throw new InvalidOperationException(ThrowingMember.Message);
    }

    public sealed class ThrowingSetter
    {
        public int Value
        {
            get => 0;
            set => throw new InvalidOperationException(ThrowingMember.Message);
        }
    }

    [GenerateShapeFor<ThrowingDefaultConstructor>]
    [GenerateShapeFor<ThrowingParameterizedConstructor>]
    [GenerateShapeFor<ThrowingGetter>]
    [GenerateShapeFor<ThrowingSetter>]
    protected partial class Witness;

    public sealed class Reflection() : MemberAccessorExceptionTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : MemberAccessorExceptionTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : MemberAccessorExceptionTests(new SourceGenProviderUnderTest(Witness.GeneratedTypeShapeProvider));
}
