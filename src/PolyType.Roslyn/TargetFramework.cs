namespace PolyType.Roslyn;

/// <summary>
/// Target framework inferred by the generator. Underlying enum values must be monotonic.
/// </summary>
public enum TargetFramework
{
    /// <summary>
    /// netstandard, netfx, or older .NET Core targets.
    /// </summary>
    Legacy = 20,

    /// <summary>
    /// The modern .NET baseline supported by PolyType.
    /// </summary>
    Net80 = 80,

    /// <summary>
    /// .NET 9 or later.
    /// </summary>
    Net90 = 90,

    /// <summary>
    /// .NET 10 or later.
    /// </summary>
    Net100 = 100,
}
