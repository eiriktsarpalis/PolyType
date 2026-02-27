using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Diagnostics;
using System.Linq;

namespace PolyType.Roslyn;

/// <summary>
/// Descriptor for diagnostic instances using structural equality comparison.
/// Provides a work-around for https://github.com/dotnet/roslyn/issues/68291.
/// </summary>
public readonly struct EquatableDiagnostic(
    DiagnosticDescriptor descriptor,
    Location? location,
    object?[] messageArgs) : IEquatable<EquatableDiagnostic>
{
    /// <summary>
    /// The <see cref="DiagnosticDescriptor"/> for the diagnostic.
    /// </summary>
    public DiagnosticDescriptor Descriptor { get; } = descriptor;

    /// <summary>
    /// The message arguments for the diagnostic.
    /// </summary>
    public object?[] MessageArgs { get; } = messageArgs;

    /// <summary>
    /// The location of the diagnostic.
    /// </summary>
    public Location? Location { get; } = location?.GetLocationTrimmed();

    /// <summary>
    /// Creates a new <see cref="Diagnostic"/> instance from the current instance.
    /// </summary>
    public Diagnostic CreateDiagnostic()
        => Diagnostic.Create(Descriptor, Location, MessageArgs);

    /// <summary>
    /// Creates a new <see cref="Diagnostic"/> instance, recovering the <see cref="SyntaxTree"/>
    /// from the <paramref name="compilation"/> to produce a pragma-suppressible <c>SourceLocation</c>.
    /// </summary>
    /// <remarks>
    /// See https://github.com/dotnet/runtime/issues/92509 for context.
    /// Locations created via <c>Location.Create(string, TextSpan, LinePositionSpan)</c> produce
    /// <c>ExternalFileLocation</c> instances which are not suppressible via <c>#pragma warning disable</c>.
    /// By looking up the <see cref="SyntaxTree"/> from the compilation at emission time, we can
    /// create a <c>SourceLocation</c> that properly respects inline suppressions.
    /// </remarks>
    public Diagnostic CreateDiagnostic(Compilation compilation)
    {
        Location? location = Location;
        if (location is not null)
        {
            Debug.Assert(location.Kind is LocationKind.ExternalFile, "Trimmed locations should always be ExternalFileLocation.");
            string filePath = location.GetLineSpan().Path;
            SyntaxTree? tree = compilation.SyntaxTrees
                .FirstOrDefault(t => t.FilePath == filePath);

            if (tree is not null)
            {
                location = Location.Create(tree, location.SourceSpan);
            }
        }

        return Diagnostic.Create(Descriptor, location, MessageArgs);
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is EquatableDiagnostic info && Equals(info);

    /// <inheritdoc/>
    public readonly bool Equals(EquatableDiagnostic other)
    {
        return Descriptor.Equals(other.Descriptor) &&
            MessageArgs.SequenceEqual(other.MessageArgs) &&
            Location == other.Location;
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        int hashCode = Descriptor.GetHashCode();
        foreach (object? messageArg in MessageArgs)
        {
            hashCode = CommonHelpers.CombineHashCodes(hashCode, messageArg?.GetHashCode() ?? 0);
        }

        hashCode = CommonHelpers.CombineHashCodes(hashCode, Location?.GetHashCode() ?? 0);
        return hashCode;
    }

    /// <inheritdoc/>
    public static bool operator ==(EquatableDiagnostic left, EquatableDiagnostic right)
    {
        return left.Equals(right);
    }

    /// <inheritdoc/>
    public static bool operator !=(EquatableDiagnostic left, EquatableDiagnostic right)
    {
        return !left.Equals(right);
    }
}