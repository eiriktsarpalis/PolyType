# AGENTS.md

PolyType is a practical generic programming library for .NET. It provides a type model that describes the structure of .NET types, enabling the rapid development of high-performance libraries such as serializers, validators, parsers, and mappers. The type model can be populated either by a runtime **reflection provider** or by a **source generator** (for Native AOT and trimming support).

## Core Requirements

- **Any code you commit MUST compile, and new and existing tests related to the change MUST pass.**
- Make your best effort to verify changes build and pass tests before committing. If for any reason you were unable to build or test code changes, you MUST report that. You MUST NOT claim success unless all builds and tests pass.
- Do not finish a code change without building and running the relevant tests after your last edits. Do not simply assume that your changes fix test failures you see — actually build and run those tests again to confirm.
- Follow all code-formatting and naming conventions defined in [`.editorconfig`](.editorconfig).

## Building & Testing

```bash
dotnet build
dotnet test                        # add --framework net10.0 for fast single-TFM iteration
```

`dotnet test` is fine for quick feedback, but a full `make test` must pass before finalizing a change. The [`Makefile`](Makefile) defines every step CI enforces:

| Target | Description |
|--------|-------------|
| `make build` | Restore tools + NuGet packages, then build the solution |
| `make test-clr` | Run CLR tests with coverage, crash dumps, and hang dumps |
| `make test-aot` | Publish and run Native AOT smoke tests |
| `make test` | Run both CLR and AOT tests (default target) |
| `make test-aot-size` | Publish the canonical AOT app and check its binary size against the committed per-RID baselines |
| `make pack` | Create NuGet packages |
| `make generate-docs` / `make serve-docs` | Build / serve the DocFX site (port 8080) |
| `make release VERSION=x.y` | Bump version, tag, push, and create a GitHub release |

Test conventions:

- Add new type scenarios to **`PolyType.TestCases`** so they get coverage across every test configuration.
- The **`ProviderUnderTest`** abstraction runs the same test logic against both the reflection and source-generated providers.

## Coding Conventions

In addition to the rules enforced by `.editorconfig`, you SHOULD:

- Prefer file-scoped namespace declarations.
- Use `is null` or `is not null` instead of `== null` or `!= null`.
- Use `nameof` instead of string literals when referring to member names.
- Use pattern matching and switch expressions wherever possible.
- Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.
- Prefer `?.` if applicable (e.g., `scope?.Dispose()`).
- Ensure that the final return statement of a method is on its own line.
- When adding new unit tests, prefer `[Theory]` with multiple data sources (like `[InlineData]` or `[MemberData]`) over multiple duplicative `[Fact]` methods.
- Do not emit "Arrange", "Act", or "Assert" comments in tests.
- Include XML documentation comments on new public APIs.
- For markdown (`.md`) files, ensure there is no trailing whitespace at the end of any line.

## Architecture

PolyType's type model is a small set of core abstractions populated by either provider. Key vocabulary:

- **Type shapes** — abstractions (`ITypeShape`, `IObjectTypeShape`, `IEnumerableTypeShape`, etc.) that describe a type's structure: properties, constructors, collection semantics, enum values, and more.
- **Visitor pattern** — `TypeShapeVisitor` traverses the model to build functionality generically over arbitrary types.
- **`IShapeable<T>`** — types advertise their own shape through this interface (typically via source generation).
- **Attributes** — `[GenerateShape]`, `[PropertyShape]`, `[ConstructorShape]`, etc. drive the source generator.

For deeper, scoped detail see the nested files: [`src/PolyType/AGENTS.md`](src/PolyType/AGENTS.md) (core library components) and [`src/PolyType.SourceGenerator/AGENTS.md`](src/PolyType.SourceGenerator/AGENTS.md) (source generator architecture and incremental-safety rules).

## Project Layout

- **`src/`** — `PolyType` (core library: both providers, abstractions, attributes), `PolyType.Roslyn` (general-purpose `ITypeSymbol` → model extraction, reusable by third-party generators), `PolyType.SourceGenerator` (built-in incremental generator), `PolyType.Examples` (reference serializers/binders/mappers built on PolyType), `PolyType.TestCases` (+ `.FSharp`, shared test types), and `Shared` (polyfills/helpers).
- **`tests/`** — `PolyType.Tests` (main xUnit suite), `PolyType.Tests.NativeAOT` (AOT smoke tests, TUnit), `PolyType.SourceGenerator.UnitTests` (generator + snapshot tests), `PolyType.Roslyn.Tests`, `PolyType.Benchmarks` (BenchmarkDotNet; not a test project), and `SizeTrackingApp.AOT` (canonical app whose published size is tracked per-RID in `aot-size-baselines.json` via `make test-aot-size`).
- **`applications/`** — five Native AOT sample apps plus one reflection-based app.
- **`eng/AotSizeCheck/`** — internal CLI that backs `make test-aot-size`.

## Things to Avoid / Common Gotchas

- **Multi-targeting** — the core library targets net10.0/net9.0/net8.0/net472/netstandard2.0; watch for API availability differences across TFMs.
- **Strong naming** — assemblies under `src/` are signed with `OpenKey.snk`; test and build-tooling projects are not.
- **Versioning** — Nerdbank.GitVersioning (nbgv) manages versions from `version.json`; don't manually edit assembly versions.
- **Package validation** — runs against the **v1.0.0** baseline, so breaking public API changes will fail the build.

## Keeping This Document Current

**Any change that invalidates something stated in an `AGENTS.md` file — root or nested — must update the affected file(s) as part of the same change.** Treat these files as living documentation: if you add, remove, or rename a project, component, or folder; change target frameworks; alter the build/test pipeline; or otherwise make a description here inaccurate, fix the corresponding `AGENTS.md` in the same commit/PR so it never drifts from the code.

This repository uses nested `AGENTS.md` files for subproject-specific guidance:

- [`src/PolyType/AGENTS.md`](src/PolyType/AGENTS.md) — core library structure and components.
- [`src/PolyType.SourceGenerator/AGENTS.md`](src/PolyType.SourceGenerator/AGENTS.md) — source generator architecture and incremental-safety rules.

Agents read the file closest to the code being edited, so keep scoped guidance in the nearest `AGENTS.md` and reserve this root file for repository-wide concerns.
