using System.Diagnostics;

namespace PolyType;

/// <summary>
/// Configures source-generation options for the current assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
[Conditional("NEVER")]
public sealed class PolyTypeSourceGenerationOptionsAttribute : Attribute
{
    /// <summary>
    /// Gets or initializes the optimization mode used by generated type shapes.
    /// </summary>
    public PolyTypeOptimizationMode OptimizationMode { get; init; }
}
