using System.Diagnostics;

namespace PolyType.FastPathJson;

/// <summary>Generates an explicit <see cref="IJsonSerializable{T}"/> implementation on a partial type.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
[Conditional("NEVER")]
public sealed class GenerateJsonAttribute : Attribute;

/// <summary>Generates a serializer for an external type on a partial witness type.</summary>
/// <param name="target">The closed type to serialize.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
[Conditional("NEVER")]
public sealed class GenerateJsonForAttribute(Type target) : Attribute
{
    /// <summary>Gets the closed type to serialize.</summary>
    public Type Target { get; } = target;
}

/// <summary>Generates a serializer for <typeparamref name="T"/> on a partial witness type.</summary>
/// <typeparam name="T">The closed type to serialize.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
[Conditional("NEVER")]
public sealed class GenerateJsonForAttribute<T> : Attribute;
