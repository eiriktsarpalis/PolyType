#nullable enable

namespace PolyType.Roslyn;

/// <summary>
/// Target framework inferred by the generator. Underlying enum values must be monotonic.
/// </summary>
#if POLYTYPE_ROSLYN_PUBLIC
public
#else
internal
#endif
enum TargetFramework
{
    /// <summary>
    /// netstandard, netfx, or older .NET Core targets.
    /// </summary>
    Legacy = 20,

    /// <summary>
    /// The .NET 8 capability tier.
    /// </summary>
    Net80 = 80,

    /// <summary>
    /// .NET 9 or later.
    /// </summary>
    Net90 = 90,
}
