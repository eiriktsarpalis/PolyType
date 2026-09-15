using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.Tests;

/// <summary>
/// Verifies that member accessors surface exceptions thrown by user code without wrapping them
/// in a <see cref="System.Reflection.TargetInvocationException"/>. The Reflection.Emit accessor
/// emits direct calls and never wrapped exceptions; this exercises the reflection accessor (and
/// the source generator) to guarantee the same behavior. See dotnet/runtime#130503.
/// </summary>
public abstract partial class MemberAccessorExceptionTests(ProviderUnderTest providerUnderTest)
{
    [Theory]
    [InlineData(typeof(ThrowingDefaultConstructor))]
    [InlineData(typeof(ThrowingPrivateDefaultConstructor))]
    [InlineData(typeof(ThrowingPrivateDefaultConstructor<int>))]
    public void DefaultConstructor_PropagatesUserExceptionUnwrapped(Type type)
    {
        var shape = (IObjectTypeShape)providerUnderTest.Provider.GetTypeShapeOrThrow(type);
        Assert.NotNull(shape.Constructor);

        var ex = Assert.Throws<InvalidOperationException>(() => shape.Constructor.Accept(MemberInvoker.Instance));
        Assert.Equal(ThrowingMember.Message, ex.Message);
        Assert.Contains(type.Name.Split('`')[0], ex.StackTrace);
    }

    [Theory]
    [InlineData(typeof(ThrowingParameterizedConstructor))]
    [InlineData(typeof(ThrowingPrivateParameterizedConstructor))]
    [InlineData(typeof(ThrowingPrivateParameterizedConstructor<int>))]
    [InlineData(typeof(ThrowingPrivateParameterizedConstructor<string>))]
    public void ParameterizedConstructor_PropagatesUserExceptionUnwrapped(Type type)
    {
        var shape = (IObjectTypeShape)providerUnderTest.Provider.GetTypeShapeOrThrow(type);
        Assert.NotNull(shape.Constructor);

        var ex = Assert.Throws<InvalidOperationException>(() => shape.Constructor.Accept(MemberInvoker.Instance));
        Assert.Equal(ThrowingMember.Message, ex.Message);
        Assert.Contains(type.Name.Split('`')[0], ex.StackTrace);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrivateGenericMembers_PreserveExceptionIdentity(bool throwTargetInvocationException)
    {
        Test<int>();
        Test<string>();

        void Test<T>()
        {
            Exception expected = throwTargetInvocationException
                ? new TargetInvocationException(new InvalidOperationException(ThrowingMember.Message))
                : new InvalidOperationException(ThrowingMember.Message);
            var instance = new ThrowingPrivateAccessors<T> { Exception = expected };
            var shape = (IObjectTypeShape<ThrowingPrivateAccessors<T>>)providerUnderTest.Provider.GetTypeShapeOrThrow<ThrowingPrivateAccessors<T>>();
            var property = Assert.IsAssignableFrom<IPropertyShape<ThrowingPrivateAccessors<T>, T>>(Assert.Single(shape.Properties));
            var @event = Assert.IsAssignableFrom<IEventShape<ThrowingPrivateAccessors<T>, Action<T>>>(Assert.Single(shape.Events));
            Action<T> handler = _ => { };

            Assert.Same(expected, Record.Exception(() => property.GetGetter()(ref instance)));
            Assert.Same(expected, Record.Exception(() => property.GetSetter()(ref instance, default!)));
            Assert.Same(expected, Record.Exception(() => Assert.Single(shape.Methods).Accept(MemberInvoker.Instance, instance)));
            Assert.Same(expected, Record.Exception(() => @event.GetAddHandler()(ref instance, handler)));
            Assert.Same(expected, Record.Exception(() => @event.GetRemoveHandler()(ref instance, handler)));
            Assert.Contains(nameof(ThrowingPrivateAccessors<int>), expected.StackTrace);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrivateConstructor_DoesNotUnwrapUserThrownTargetInvocationException(bool generic)
    {
        Type type = generic ? typeof(ThrowingTargetInvocationConstructor<int>) : typeof(ThrowingTargetInvocationConstructor);
        var shape = (IObjectTypeShape)providerUnderTest.Provider.GetTypeShapeOrThrow(type);
        var exception = Assert.Throws<TargetInvocationException>(() => shape.Constructor!.Accept(MemberInvoker.Instance));
        var innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(ThrowingMember.Message, innerException.Message);
        Assert.Contains(type.Name.Split('`')[0], exception.StackTrace);
    }

    [Theory]
    [MemberData(nameof(GetCrossAssemblyConstructorCases))]
    public void CrossAssemblyPrivateConstructor_PropagatesUserException(ITestCase testCase)
    {
        var shape = (IObjectTypeShape)providerUnderTest.ResolveShape(testCase);
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => shape.Constructor!.Accept(NegativeArgumentInvoker.Instance));
        Assert.Equal("count", exception.ParamName);
        Assert.Contains("GenericPrivateFixedConstructor", exception.StackTrace);
    }

    public static IEnumerable<object[]> GetCrossAssemblyConstructorCases() =>
        TestTypes.GetTestCasesCore()
            .Where(c => c.Type.Name is "GenericPrivateFixedConstructor`1")
            .Select(c => new object[] { c });

    [Theory]
    [InlineData(typeof(ThrowingStaticAccessors))]
    [InlineData(typeof(ThrowingStaticAccessors<int>))]
    public void PrivateStaticMembers_PropagateUserExceptions(Type type)
    {
        ITypeShape shape = providerUnderTest.Provider.GetTypeShapeOrThrow(type);
        var methodException = Assert.Throws<InvalidOperationException>(() => Assert.Single(shape.Methods).Accept(MemberInvoker.Instance));
        Assert.Equal(ThrowingMember.Message, methodException.Message);
        Assert.Contains(type.Name.Split('`')[0], methodException.StackTrace);

        Assert.Single(shape.Events).Accept(StaticEventInvoker.Instance);
    }

    [Theory]
    [InlineData(typeof(TypeWithThrowingMarshaler))]
    [InlineData(typeof(TypeWithThrowingStructMarshaler))]
    public void MarshalerConstructor_PropagatesUserException(Type type)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => providerUnderTest.Provider.GetTypeShapeOrThrow(type));
        Assert.Equal(ThrowingMember.Message, exception.Message);
        Assert.Contains("Marshaler..ctor", exception.StackTrace);
    }

    private sealed class NegativeArgumentInvoker : TypeShapeVisitor
    {
        public static readonly NegativeArgumentInvoker Instance = new();

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            TArgumentState arguments = constructor.GetArgumentStateConstructor()();
            var parameter = Assert.IsAssignableFrom<IParameterShape<TArgumentState, int>>(constructor.Parameters[0]);
            parameter.GetSetter()(ref arguments, -1);
            return constructor.GetParameterizedConstructor()(ref arguments);
        }
    }

    private sealed class StaticEventInvoker : TypeShapeVisitor
    {
        public static readonly StaticEventInvoker Instance = new();

        public override object? VisitEvent<TDeclaringType, THandler>(IEventShape<TDeclaringType, THandler> @event, object? state)
        {
            var target = default(TDeclaringType);
            var handler = (THandler)(object)new Action(() => { });
            var addException = Assert.Throws<InvalidOperationException>(() => @event.GetAddHandler()(ref target, handler));
            var removeException = Assert.Throws<InvalidOperationException>(() => @event.GetRemoveHandler()(ref target, handler));
            Assert.Equal(ThrowingMember.Message, addException.Message);
            Assert.Equal(ThrowingMember.Message, removeException.Message);
            return null;
        }
    }

    private sealed class MemberInvoker : TypeShapeVisitor
    {
        public static readonly MemberInvoker Instance = new();

        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            if (constructor.Parameters.Count == 0)
            {
                return constructor.GetDefaultConstructor()();
            }

            TArgumentState argumentState = constructor.GetArgumentStateConstructor()();
            foreach (IParameterShape parameter in constructor.Parameters)
            {
                argumentState = (TArgumentState)parameter.Accept(this, argumentState)!;
            }

            return constructor.GetParameterizedConstructor()(ref argumentState);
        }

        public override object? VisitMethod<TDeclaringType, TArgumentState, TResult>(IMethodShape<TDeclaringType, TArgumentState, TResult> method, object? state)
        {
            var target = (TDeclaringType?)state;
            TArgumentState arguments = method.GetArgumentStateConstructor()();
            foreach (IParameterShape parameter in method.Parameters)
            {
                arguments = (TArgumentState)parameter.Accept(this, arguments)!;
            }

            return method.GetMethodInvoker()(ref target, ref arguments).GetAwaiter().GetResult();
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ThrowingDefaultConstructor() => throw new InvalidOperationException(ThrowingMember.Message);
    }

    public sealed class ThrowingParameterizedConstructor
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
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

    public sealed class ThrowingPrivateDefaultConstructor
    {
        [ConstructorShape, MethodImpl(MethodImplOptions.NoInlining)]
        private ThrowingPrivateDefaultConstructor() => throw new InvalidOperationException(ThrowingMember.Message);
    }

    public sealed class ThrowingPrivateDefaultConstructor<T>
    {
        [ConstructorShape, MethodImpl(MethodImplOptions.NoInlining)]
        private ThrowingPrivateDefaultConstructor() => throw new InvalidOperationException(ThrowingMember.Message);
    }

    public sealed class ThrowingPrivateParameterizedConstructor
    {
        [ConstructorShape, MethodImpl(MethodImplOptions.NoInlining)]
        private ThrowingPrivateParameterizedConstructor(int value) => throw new InvalidOperationException(ThrowingMember.Message);
        public int Value { get; }
    }

    public sealed class ThrowingPrivateParameterizedConstructor<T>
    {
        [ConstructorShape, MethodImpl(MethodImplOptions.NoInlining)]
        private ThrowingPrivateParameterizedConstructor(T value) => throw new InvalidOperationException(ThrowingMember.Message);
        public T Value { get; }
    }

    public sealed class ThrowingTargetInvocationConstructor
    {
        [ConstructorShape, MethodImpl(MethodImplOptions.NoInlining)]
        private ThrowingTargetInvocationConstructor() => throw new TargetInvocationException(new InvalidOperationException(ThrowingMember.Message));
    }

    public sealed class ThrowingTargetInvocationConstructor<T>
    {
        [ConstructorShape, MethodImpl(MethodImplOptions.NoInlining)]
        private ThrowingTargetInvocationConstructor() => throw new TargetInvocationException(new InvalidOperationException(ThrowingMember.Message));
    }

    public struct ThrowingPrivateAccessors<T>
    {
        [PropertyShape(Ignore = true)]
        public Exception Exception { get; set; }

        [PropertyShape]
        private T Value
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get => throw Exception;
            [MethodImpl(MethodImplOptions.NoInlining)]
            set => throw Exception;
        }

        [MethodShape, MethodImpl(MethodImplOptions.NoInlining)]
        private T Method(T value) => throw Exception;

        [EventShape]
        private event Action<T> Changed
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            add => throw Exception;
            [MethodImpl(MethodImplOptions.NoInlining)]
            remove => throw Exception;
        }
    }

    public sealed class ThrowingStaticAccessors
    {
        [MethodShape, MethodImpl(MethodImplOptions.NoInlining)]
        private static int Method() => throw new InvalidOperationException(ThrowingMember.Message);

        [EventShape]
        private static event Action Changed
        {
            add => throw new InvalidOperationException(ThrowingMember.Message);
            remove => throw new InvalidOperationException(ThrowingMember.Message);
        }
    }

    public sealed class ThrowingStaticAccessors<T>
    {
        [MethodShape, MethodImpl(MethodImplOptions.NoInlining)]
        private static int Method() => throw new InvalidOperationException(ThrowingMember.Message);

        [EventShape]
        private static event Action Changed
        {
            add => throw new InvalidOperationException(ThrowingMember.Message);
            remove => throw new InvalidOperationException(ThrowingMember.Message);
        }
    }

    [TypeShape(Marshaler = typeof(ThrowingMarshaler))]
    public sealed class TypeWithThrowingMarshaler;

    public sealed class ThrowingMarshaler : IMarshaler<TypeWithThrowingMarshaler, int>
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ThrowingMarshaler() => throw new InvalidOperationException(ThrowingMember.Message);
        public int Marshal(TypeWithThrowingMarshaler? value) => 0;
        public TypeWithThrowingMarshaler Unmarshal(int value) => new();
    }

    [TypeShape(Marshaler = typeof(ThrowingStructMarshaler))]
    public sealed class TypeWithThrowingStructMarshaler;

    public struct ThrowingStructMarshaler : IMarshaler<TypeWithThrowingStructMarshaler, int>
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ThrowingStructMarshaler() => throw new InvalidOperationException(ThrowingMember.Message);
        public int Marshal(TypeWithThrowingStructMarshaler? value) => 0;
        public TypeWithThrowingStructMarshaler Unmarshal(int value) => new();
    }

    [GenerateShapeFor<ThrowingDefaultConstructor>]
    [GenerateShapeFor<ThrowingParameterizedConstructor>]
    [GenerateShapeFor<ThrowingGetter>]
    [GenerateShapeFor<ThrowingSetter>]
    [GenerateShapeFor<ThrowingPrivateDefaultConstructor>]
    [GenerateShapeFor<ThrowingPrivateDefaultConstructor<int>>]
    [GenerateShapeFor<ThrowingPrivateParameterizedConstructor>]
    [GenerateShapeFor<ThrowingPrivateParameterizedConstructor<int>>]
    [GenerateShapeFor<ThrowingPrivateParameterizedConstructor<string>>]
    [GenerateShapeFor<ThrowingTargetInvocationConstructor>]
    [GenerateShapeFor<ThrowingTargetInvocationConstructor<int>>]
    [GenerateShapeFor<ThrowingPrivateAccessors<int>>]
    [GenerateShapeFor<ThrowingPrivateAccessors<string>>]
    [GenerateShapeFor<ThrowingStaticAccessors>]
    [GenerateShapeFor<ThrowingStaticAccessors<int>>]
    [GenerateShapeFor<TypeWithThrowingMarshaler>]
    [GenerateShapeFor<TypeWithThrowingStructMarshaler>]
    protected partial class Witness;

    public sealed class Reflection() : MemberAccessorExceptionTests(ReflectionProviderUnderTest.NoEmit);
    public sealed class ReflectionEmit() : MemberAccessorExceptionTests(ReflectionProviderUnderTest.Emit);
    public sealed class SourceGen() : MemberAccessorExceptionTests(new SourceGenProviderUnderTest(Witness.GeneratedTypeShapeProvider));
}
