using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.UnitTests;

internal class ReferencesHelper
{
#if NET
	internal static ReferenceAssemblies References = ReferenceAssemblies.Net.Net80;
#else
    internal static ReferenceAssemblies References = ReferenceAssemblies.NetStandard.NetStandard20
        .WithPackages(ImmutableArray.Create([
            new PackageIdentity("System.Memory", "4.6.0"),
            new PackageIdentity("System.Text.Json", "9.0.0"),
        ]));
#endif

    internal static IEnumerable<MetadataReference> GetReferences()
    {
        yield return MetadataReference.CreateFromFile(typeof(GenerateShapeAttribute).Assembly.Location);
    }
}
