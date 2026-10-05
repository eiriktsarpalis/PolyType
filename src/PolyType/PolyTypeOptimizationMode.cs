namespace PolyType;

/// <summary>
/// Specifies the optimization preference for PolyType source generation.
/// </summary>
public enum PolyTypeOptimizationMode
{
    /// <summary>
    /// Favors runtime performance and preserves the default argument-state representation.
    /// </summary>
    Performance = 0,

    /// <summary>
    /// Favors application size. The initial optimization reduces Native AOT code size.
    /// </summary>
    AppSize = 1,
}
