using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using RuntimeReflectionHelpers = PolyType.ReflectionProvider.ReflectionHelpers;

namespace PolyType.Tests;

public static class ReflectionHelpersTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CreateInstance_PreservesParameterlessActivation(bool useArgumentOverload)
    {
        Assert.Equal(42, Assert.IsType<ReferenceType>(CreateInstance(typeof(ReferenceType))).Value);

        // Compare against Activator after its constructor cache is populated.
        _ = ActivateNormally(typeof(ValueTypeWithConstructor));
        for (int i = 0; i < 2; i++)
        {
            int expected = Assert.IsType<ValueTypeWithConstructor>(ActivateNormally(typeof(ValueTypeWithConstructor))).Value;
            Assert.Equal(expected, Assert.IsType<ValueTypeWithConstructor>(CreateInstance(typeof(ValueTypeWithConstructor))).Value);
        }

        Assert.Equal(0, Assert.IsType<ValueTypeWithoutConstructor>(CreateInstance(typeof(ValueTypeWithoutConstructor))).Value);
        Assert.Null(CreateInstance(typeof(int?)));
        Assert.Throws<MissingMethodException>(() => CreateInstance(typeof(PrivateConstructors)));
        Assert.Throws<MissingMethodException>(() => CreateInstance(typeof(ParameterizedConstructor)));

        object? CreateInstance(Type type) => useArgumentOverload
            ? RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(type, args: [])
            : RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(type);

        object? ActivateNormally(Type type) => useArgumentOverload
            ? Activator.CreateInstance(type, args: [])
            : Activator.CreateInstance(type);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CreateInstance_NullOrEmptyArgumentsUseParameterlessConstructor(bool useNullArguments)
    {
        object?[]? args = useNullArguments ? null : [];
        var instance = Assert.IsType<ReferenceType>(RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(typeof(ReferenceType), args));
        Assert.Equal(42, instance.Value);
        Assert.Null(RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(typeof(int?), args));
    }

    [Theory]
    [InlineData(42, "value")]
    [InlineData(0, null)]
    public static void CreateInstance_PreservesArgumentBinding(int value, string? label)
    {
        var instance = Assert.IsType<ParameterizedConstructor>(
            RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(typeof(ParameterizedConstructor), value, label));
        Assert.Equal(value, instance.Value);
        Assert.Equal(label, instance.Label);
        Assert.Throws<MissingMethodException>(() => RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(typeof(PrivateConstructors), value));
    }

    [Theory]
    [InlineData(typeof(ThrowingReferenceType), false)]
    [InlineData(typeof(ThrowingReferenceType), true)]
    [InlineData(typeof(ThrowingValueType), false)]
    [InlineData(typeof(ThrowingValueType), true)]
    public static void CreateInstance_PreservesUserThrownTargetInvocationException(Type type, bool useArgumentOverload)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => useArgumentOverload
            ? RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(type, args: [])
            : RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(type));
        Assert.Equal("User-thrown exception.", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains(type.Name, exception.StackTrace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CreateInstance_PreservesExceptionIdentity(bool throwTargetInvocationException)
    {
        Exception expected = throwTargetInvocationException
            ? new TargetInvocationException(new InvalidOperationException("User-thrown exception."))
            : new InvalidOperationException("User-thrown exception.");

        Assert.Same(expected, Record.Exception(() =>
            RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(typeof(ThrowingParameterizedConstructor), expected)));
        Assert.Contains(nameof(ThrowingParameterizedConstructor), expected.StackTrace);
    }

#if NET
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CreateInstance_DoesNotCreateExceptionWrapper(bool useArgumentOverload)
    {
        Exception expected = useArgumentOverload
            ? new InvalidOperationException("User-thrown exception.")
            : ThrowingDefaultConstructor.Expected;
        bool wrapperObserved = false;
        EventHandler<FirstChanceExceptionEventArgs> handler = (_, args) =>
        {
            if (args.Exception is TargetInvocationException exception && ReferenceEquals(exception.InnerException, expected))
            {
                wrapperObserved = true;
            }
        };

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try
        {
            Assert.Same(expected, Record.Exception(() => useArgumentOverload
                ? RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(typeof(ThrowingParameterizedConstructor), expected)
                : RuntimeReflectionHelpers.CreateInstanceNoWrapExceptions(typeof(ThrowingDefaultConstructor))));
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        Assert.False(wrapperObserved);
    }

    public sealed class ThrowingDefaultConstructor
    {
        public static Exception Expected { get; } = new InvalidOperationException("User-thrown exception.");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public ThrowingDefaultConstructor() => throw Expected;
    }
#endif

    public sealed class ReferenceType
    {
        public int Value { get; } = 42;
    }

    public struct ValueTypeWithConstructor
    {
        public ValueTypeWithConstructor() => Value = 42;
        public int Value { get; }
    }

    public struct ValueTypeWithoutConstructor
    {
        public int Value;
    }

    public sealed class PrivateConstructors
    {
        private PrivateConstructors() => throw new InvalidOperationException("Private constructor invoked.");
        private PrivateConstructors(int value) => throw new InvalidOperationException("Private constructor invoked.");
    }

    public sealed class ParameterizedConstructor(int value, string? label)
    {
        public int Value { get; } = value;
        public string? Label { get; } = label;
    }

    public sealed class ThrowingReferenceType
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ThrowingReferenceType() => throw new TargetInvocationException("User-thrown exception.", new InvalidOperationException());
    }

    public struct ThrowingValueType
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ThrowingValueType() => throw new TargetInvocationException("User-thrown exception.", new InvalidOperationException());
    }

    public sealed class ThrowingParameterizedConstructor
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ThrowingParameterizedConstructor(Exception exception) => throw exception;
    }
}
