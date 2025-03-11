using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace PolyType.SourceGenerator.Model;

/// <summary>
/// Represents a cacheable type identifier that uses FQN to derive equality.
/// </summary>
public readonly struct TypeId : IEquatable<TypeId>
{
    public required string FullyQualifiedName { get; init; }
    public required ImmutableArray<TypeId> TypeArguments { get; init; }
    public required bool IsValueType { get; init; }
    public required SpecialType SpecialType { get; init; }

    public bool Equals(TypeId other) => FullyQualifiedName == other.FullyQualifiedName;
    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
    public override bool Equals(object obj) => obj is TypeId other && Equals(other);
    public static bool operator ==(TypeId left, TypeId right) => left.Equals(right);
    public static bool operator !=(TypeId left, TypeId right) => !(left == right);
    public override string ToString() => FullyQualifiedName;

    /// <summary>
    /// Closes a generic type definition with the given type arguments
    /// and writes the fully qualified name.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="typeArguments"></param>
    internal void WriteFullyQualifiedNameWithTypeArgs(StringBuilder writer, ReadOnlySpan<TypeId> typeArguments)
    {
        // TODO: cheap and dirty
        writer.Append(this.FullyQualifiedName.Substring(0, this.FullyQualifiedName.IndexOf('<')));
        writer.Append('<');
        for (int i = 0; i < typeArguments.Length; i++)
        {
            if (i > 0)
            {
                writer.Append(", ");
            }
            writer.Append(typeArguments[i].FullyQualifiedName);
        }

        writer.Append('>');
    }
}