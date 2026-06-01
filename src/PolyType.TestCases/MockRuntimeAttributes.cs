using System;

// Mock attributes matching the metadata shape the C# compiler will produce.
// These live in System.Runtime.CompilerServices to match the expected full names.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Mock attribute for closed enums and closed class hierarchies.
    /// The C# compiler will emit this on types declared with the <c>closed</c> modifier.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Struct, Inherited = false)]
    public sealed class ClosedAttribute : Attribute;

    /// <summary>
    /// Mock attribute for declaring the permitted direct subtypes of a closed hierarchy.
    /// The C# compiler will emit one of these for each direct subtype.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
    public sealed class ClosedSubtypeAttribute : Attribute
    {
        public ClosedSubtypeAttribute(Type subtypeType) => SubtypeType = subtypeType;
        public Type SubtypeType { get; }
    }

    /// <summary>
    /// Mock attribute marking a type as a C# union type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class UnionAttribute : Attribute;
}

namespace System
{
    /// <summary>
    /// Mock interface implemented by C# union types to provide access to the wrapped value.
    /// </summary>
    public interface IUnion
    {
        /// <summary>
        /// Gets the value held by the union.
        /// </summary>
        object? Value { get; }
    }
}
