#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// Marks a type implementing the C# union member pattern.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class UnionAttribute : Attribute;

/// <summary>
/// Exposes the payload of a C# union.
/// </summary>
public interface IUnion
{
    /// <summary>
    /// Gets the union payload.
    /// </summary>
    object? Value { get; }
}
#endif
