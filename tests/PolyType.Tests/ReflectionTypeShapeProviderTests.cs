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
