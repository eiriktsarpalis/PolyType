using System.Reflection;

namespace PolyType.ReflectionProvider;

/// <summary>
/// Exposes configuration options for the reflection-based type shape provider.
/// </summary>
public sealed record ReflectionTypeShapeProviderOptions
{
    /// <summary>
    /// Gets the default configuration options.
    /// </summary>
    public static ReflectionTypeShapeProviderOptions Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether System.Reflection.Emit should be used when generating member accessors.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c> if the runtime supports dynamic code generation.
    /// </remarks>
    public bool UseReflectionEmit { get; init; } = ReflectionHelpers.IsDynamicCodeSupported;

    /// <summary>
    /// Gets the list of assemblies to scan for <see cref="TypeShapeExtensionAttribute"/> attributes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The content of this list will typically be made up of the calling assembly,
    /// and from <see cref="Assembly.GetReferencedAssemblies()"/> invoked on the calling assembly.
    /// </para>
    /// <para>
    /// Conflicts between <see cref="TypeShapeExtensionAttribute"/> attributes are resolved by taking the first of the conflicting values.
    /// It is therefore advisable to order this list so that the calling assembly appears first in the list.
    /// The caller is then in a position to set all policies and resolve conflicts between other assemblies.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Assembly> TypeShapeExtensionAssemblies { get; init; } = [];

    // Lazily initialized HashSet for efficient O(n) equality comparison
    private HashSet<Assembly> AssemblySet => field ??= new(TypeShapeExtensionAssemblies);

    /// <inheritdoc/>
    public bool Equals(ReflectionTypeShapeProviderOptions? other)
    {
        if (other is null)
        {
            return false;
        }

        return UseReflectionEmit == other.UseReflectionEmit
            && AssemblySet.SetEquals(other.TypeShapeExtensionAssemblies);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Use XOR for order-independent hashing of assemblies
        int assemblyHash = 0;
        foreach (var assembly in TypeShapeExtensionAssemblies)
        {
            assemblyHash ^= assembly.GetHashCode();
        }

        return unchecked((UseReflectionEmit ? 1 << 30 : 0) + assemblyHash);
    }
}
