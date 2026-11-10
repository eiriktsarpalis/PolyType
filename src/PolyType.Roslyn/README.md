# PolyType.Roslyn

PolyType.Roslyn provides components for extracting data models from types using the Roslyn symbol API.

PolyType.Roslyn is a source-only NuGet package. A direct package reference compiles its sources into your project, so source generators can use the components without distributing a separate `PolyType.Roslyn.dll`.

## Using the package

Add the package to an SDK-style C# generator project. The package supplies a `Microsoft.CodeAnalysis.CSharp` dependency with a minimum version of 3.11.0. Add a direct reference when your generator needs a newer Roslyn version, as in this incremental-generator example. The example also uses PolySharp, which is recommended for older target frameworks:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="PolyType.Roslyn" Version="2.0.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.3.0" PrivateAssets="all" />
    <PackageReference Include="PolySharp" Version="1.16.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

The sources require a C# 14-capable compiler because they use field-backed properties and extension blocks. This requirement concerns **building your generator**, not the language version of the application it processes. The shared sources are built against Roslyn 3.11 APIs; the example overrides the package's baseline with 4.3 for an incremental generator. The generator's resolved Roslyn references determine its host compatibility independently of the compiler used to build it.

PolySharp supplies compiler-support types and attributes missing from older target frameworks such as `netstandard2.0`. It is not a dependency of the source package: you can use equivalent definitions instead, or omit it when your framework and project already supply all required types. PolyType.Roslyn does not select a PolySharp version for you, and its build import does not change your project's language version, nullable settings, or implicit usings.

For example, this generator counts the properties of a `Person` type in the input compilation:

```csharp
using Microsoft.CodeAnalysis;
using PolyType.Roslyn;

[Generator]
public sealed class PersonPropertyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider, static (output, compilation) =>
        {
            INamedTypeSymbol? person = compilation.GetTypeByMetadataName("Person");
            if (person is null)
            {
                return;
            }

            var generator = new TypeDataModelGenerator(
                compilation.Assembly, new KnownSymbols(compilation), output.CancellationToken);
            if (generator.IncludeType(person) != TypeDataModelGenerationStatus.Success)
            {
                foreach (Diagnostic diagnostic in generator.Diagnostics)
                {
                    output.ReportDiagnostic(diagnostic);
                }

                return;
            }

            var model = (ObjectDataModel)generator.GeneratedModels[person];
            using var writer = new SourceWriter();
            writer.WriteLine("internal static class PersonMetadata");
            writer.WriteLine("{");
            writer.WriteLine($"public const int PropertyCount = {model.Properties.Length};");
            writer.WriteLine("}");
            output.AddSource("PersonMetadata.g.cs", writer.ToSourceText());
        });
    }
}
```

Model extraction happens inside the output callback. Live Roslyn symbols and `TypeDataModel` objects should not be stored in incremental pipeline models; project them into equatable values when building a cached pipeline.

## Accessibility and type identity

Package types are internal by default. To make the API types public in your assembly, opt in:

```xml
<PropertyGroup>
  <PolyTypeRoslynPublic>true</PolyTypeRoslynPublic>
</PropertyGroup>
```

Each assembly that includes the sources owns a distinct copy of the types. Public opt-in does not make copies in different assemblies interchangeable. If multiple projects need a shared public API, include the sources in one assembly and reference that assembly from the others.

Keep `PrivateAssets="all"` on the reference when packaging your generator. The source import is deliberately non-transitive, so applications using the generator do not compile the Roslyn components or inherit their dependencies.

Only SDK-style C# projects using `PackageReference` are supported. The package does not install editable source files into projects, support `packages.config`, or bundle third-party assemblies. Sources appear under a `PolyType.Roslyn` folder in Solution Explorer and remain available for debugging in the NuGet package cache. A package-local analyzer configuration marks the embedded sources as generated code and disables older Roslyn analyzers' `RS1024` false positives in those files; it does not change analysis of your own code.

## Shared utilities

Shared source-generation utilities include type identifiers with generic arguments, tuple flattening, array ranks, namespace qualification, and keyword escaping. The built-in PolyType generator and the independent PolyType.FastPathJson prototype consume the same implementation without sharing generator-specific code.

## Agent skill

The package includes the `author-polytype-generator` skill at `skills/author-polytype-generator/SKILL.md`, covering parser/model/emitter architecture for creating, extending, reviewing, and debugging source generators. Restoring the package makes the file available in the NuGet cache; it does not install the skill into your repository.
