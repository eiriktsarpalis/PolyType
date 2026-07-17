using PolyType.ReflectionProvider;
using System.Reflection;
using System.Runtime.CompilerServices;
#if NET
using System.Runtime.Loader;
#endif

namespace PolyType.Tests;

public static class ReflectionTypeShapeProviderTests
{
    [Fact]
    public static void OptionsEquality_WithDifferentAssemblyOrder_ShouldBeEqual()
    {
        // Arrange
        var assembly1 = typeof(ReflectionTypeShapeProviderTests).Assembly;
        var assembly2 = typeof(ReflectionTypeShapeProvider).Assembly;

        var options1 = new ReflectionTypeShapeProviderOptions
        {
            TypeShapeExtensionAssemblies = [assembly1, assembly2],
        };

        var options2 = new ReflectionTypeShapeProviderOptions
        {
            TypeShapeExtensionAssemblies = [assembly2, assembly1],
        };

        // Act & Assert
        Assert.Equal(options1, options2);
        Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
    }

    [Fact]
    public static void OptionsEquality_WithSameAssemblies_ShouldBeEqual()
    {
        // Arrange
        var assembly1 = typeof(ReflectionTypeShapeProviderTests).Assembly;
        var assembly2 = typeof(ReflectionTypeShapeProvider).Assembly;

        var options1 = new ReflectionTypeShapeProviderOptions
        {
            TypeShapeExtensionAssemblies = [assembly1, assembly2],
        };

        var options2 = new ReflectionTypeShapeProviderOptions
        {
            TypeShapeExtensionAssemblies = [assembly1, assembly2],
        };

        // Act & Assert
        Assert.Equal(options1, options2);
        Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
    }

    [Fact]
    public static void OptionsEquality_WithDifferentAssemblies_ShouldNotBeEqual()
    {
        // Arrange
        var assembly1 = typeof(ReflectionTypeShapeProviderTests).Assembly;
        var assembly2 = typeof(ReflectionTypeShapeProvider).Assembly;

        var options1 = new ReflectionTypeShapeProviderOptions
        {
            TypeShapeExtensionAssemblies = [assembly1],
        };

        var options2 = new ReflectionTypeShapeProviderOptions
        {
            TypeShapeExtensionAssemblies = [assembly2],
        };

        // Act & Assert
        Assert.NotEqual(options1, options2);
    }

    [Fact]
    public static void OptionsEquality_WithDifferentUseReflectionEmit_ShouldNotBeEqual()
    {
        // Arrange
        var options1 = new ReflectionTypeShapeProviderOptions
        {
            UseReflectionEmit = true,
        };

        var options2 = new ReflectionTypeShapeProviderOptions
        {
            UseReflectionEmit = false,
        };

        // Act & Assert
        Assert.NotEqual(options1, options2);
    }

    [Fact]
    public static void OptionsEquality_EmptyAssemblies_ShouldBeEqual()
    {
        // Arrange
        var options1 = new ReflectionTypeShapeProviderOptions
        {
            TypeShapeExtensionAssemblies = [],
        };

        var options2 = new ReflectionTypeShapeProviderOptions
        {
            TypeShapeExtensionAssemblies = [],
        };

        // Act & Assert
        Assert.Equal(options1, options2);
        Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
    }

    [DerivedTypeShape(typeof(DuplicateClosedAndOpenDerived<int>), Name = "closed", Tag = 1)]
    [DerivedTypeShape(typeof(DuplicateClosedAndOpenDerived<>), Name = "open", Tag = 2)]
    private class DuplicateClosedAndOpenBase<T>;

    private class DuplicateClosedAndOpenDerived<T> : DuplicateClosedAndOpenBase<T>;

    [Fact]
    public static void ClosedAndOpenDerivedTypesResolvingToSameType_ThrowsForResolvedType()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ReflectionTypeShapeProvider.Default.GetTypeShape(typeof(DuplicateClosedAndOpenBase<int>)));

        Assert.Contains("duplicate assignments for the derived type", ex.Message);
        Assert.Contains(typeof(DuplicateClosedAndOpenDerived<int>).ToString(), ex.Message);
    }

    private class EnclosingMismatchOuter<T>
    {
        public class Box<U>;
    }

    [DerivedTypeShape(typeof(EnclosingMismatchDerived<>))]
    private class EnclosingMismatchBase<T>;

    private class EnclosingMismatchDerived<T> : EnclosingMismatchBase<EnclosingMismatchOuter<int>.Box<T>>;

    [Fact]
    public static void OpenGenericDerivedType_EnclosingMismatch_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ReflectionTypeShapeProvider.Default.GetTypeShape(
                typeof(EnclosingMismatchBase<EnclosingMismatchOuter<string>.Box<int>>)));

        Assert.Contains("arguments do not match", ex.Message);
    }

    [DerivedTypeShape(typeof(MultipleBaseImpl<>))]
    private interface IMultipleBase1<T>;

    [DerivedTypeShape(typeof(MultipleBaseImpl<>))]
    private interface IMultipleBase2<T>;

    private class MultipleBaseImpl<T> : IMultipleBase1<T>, IMultipleBase2<T>;

    [Theory]
    [InlineData(typeof(IMultipleBase1<int>))]
    [InlineData(typeof(IMultipleBase2<string>))]
    public static void OpenGenericDerivedType_MultipleGenericInterfaceBases_ResolveIndependently(Type baseType)
    {
        var union = Assert.IsAssignableFrom<IUnionTypeShape>(ReflectionTypeShapeProvider.Default.GetTypeShape(baseType));
        Type expectedDerivedType = typeof(MultipleBaseImpl<>).MakeGenericType(baseType.GetGenericArguments());
        Assert.Contains(union.UnionCases, unionCase => unionCase.UnionCaseType.Type == expectedDerivedType);
    }

    [DerivedTypeShape(typeof(CovariantImpl<>))]
    private interface ICovariant<out T>;
    private class CovariantImpl<T> : ICovariant<T>;

    [DerivedTypeShape(typeof(ContravariantImpl<>))]
    private interface IContravariant<in T>;
    private class ContravariantImpl<T> : IContravariant<T>;

    [DerivedTypeShape(typeof(BivariantImpl<,>))]
    private interface IBivariant<in TIn, out TOut>;
    private class BivariantImpl<TIn, TOut> : IBivariant<TIn, TOut>;

    [Theory]
    [InlineData(typeof(ICovariant<object>), typeof(CovariantImpl<object>))]
    [InlineData(typeof(IContravariant<string>), typeof(ContravariantImpl<string>))]
    [InlineData(typeof(IBivariant<string, object>), typeof(BivariantImpl<string, object>))]
    public static void OpenGenericDerivedType_VariantInterface_Resolves(Type baseType, Type expectedDerivedType)
    {
        var union = Assert.IsAssignableFrom<IUnionTypeShape>(ReflectionTypeShapeProvider.Default.GetTypeShape(baseType));
        Assert.Contains(union.UnionCases, unionCase => unionCase.UnionCaseType.Type == expectedDerivedType);
    }

#if NET
    [Fact]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TypeUnloading_TypeShapeCache_ShouldAllowUnloading()
    {
        // This test verifies that the ConditionalWeakTable allows type unloading
        // when the AssemblyLoadContext is unloaded.
        WeakReference weakRef = CreateTypeShapeAndGetWeakReference();

        // Force GC to collect the unloaded assembly
        for (int i = 0; i < 10 && weakRef.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // The type should have been collected after the AssemblyLoadContext was unloaded
        Assert.False(weakRef.IsAlive, "The type should have been collected after AssemblyLoadContext unload.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateTypeShapeAndGetWeakReference()
    {
        // Create a collectible AssemblyLoadContext
        var alc = new AssemblyLoadContext("TestContext", isCollectible: true);

        // Load the PolyType assembly into the new context
        var polyTypeAssembly = alc.LoadFromAssemblyPath(typeof(ReflectionTypeShapeProvider).Assembly.Location);
        var providerType = polyTypeAssembly.GetType(typeof(ReflectionTypeShapeProvider).FullName!)!;

        // Get the Default provider from the loaded assembly
        var defaultProperty = providerType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!;
        var provider = defaultProperty.GetValue(null)!;

        // Get a type shape for a type that's loaded in the ALC
        var testType = polyTypeAssembly.GetType(typeof(ReflectionTypeShapeProviderOptions).FullName!)!;
        var getShapeMethod = providerType.GetMethod("GetTypeShape", [typeof(Type)])!;
        var typeShape = getShapeMethod.Invoke(provider, [testType]);

        // Verify that the type shape was created successfully
        if (typeShape is null)
        {
            throw new InvalidOperationException("Failed to create type shape for test type.");
        }

        // Get a weak reference to the test type
        var weakRef = new WeakReference(testType);

        // Unload the AssemblyLoadContext
        alc.Unload();

        return weakRef;
    }
#endif
}
