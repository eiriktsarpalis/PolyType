namespace PolyType;

/// <summary>
/// Marks an interface as being disallowed for implementation outside PolyType.dll,
/// as enforced by an analyzer we define.
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
internal sealed class InternalImplementationsOnlyAttribute : Attribute
{
}
