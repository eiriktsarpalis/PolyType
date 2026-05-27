#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
internal sealed class SetsRequiredMembersAttribute : Attribute { }
#endif
