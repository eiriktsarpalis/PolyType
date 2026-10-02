#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// Stores the direct subclasses of a C# closed class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class IsClosedTypeAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the compiler-declared direct subclasses.
    /// </summary>
    public Type[] DerivedTypes { get; set; } = [];
}
#endif
