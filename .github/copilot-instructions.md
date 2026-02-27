**Any code you commit MUST compile, and new and existing tests related to the change MUST pass.**

You MUST make your best effort to ensure any code changes satisfy those criteria before committing. If for any reason you were unable to build or test code changes, you MUST report that. You MUST NOT claim success unless all builds and tests pass as described above.

If you make code changes, do not complete without checking the relevant code builds and relevant tests still pass after the last edits you make. Do not simply assume that your changes fix test failures you see — actually build and run those tests again to confirm.

You MUST follow all code-formatting and naming conventions defined in [`.editorconfig`](../.editorconfig).

---

## Building & Testing

### Quick Start

```bash
dotnet build
dotnet test
```

For fast iteration during development, target a single framework:

```bash
dotnet test --framework net10.0
```

### Full E2E Validation (Makefile)

The [`Makefile`](../Makefile) at the repo root defines all steps that must pass in CI/CD. Use it for full end-to-end validation:

| Target | Description |
|--------|-------------|
| `make build` | Restore tools + NuGet packages, then build the solution |
| `make test-clr` | Run CLR tests with coverage, crash dumps, and hang dumps |
| `make test-aot` | Publish and run Native AOT smoke tests |
| `make test` | Run both CLR and AOT tests (default target) |
| `make pack` | Create NuGet packages |
| `make generate-docs` | Build documentation with DocFX |
| `make serve-docs` | Generate and serve docs locally on port 8080 |
| `make release VERSION=x.y` | Bump version, tag, push, and create a GitHub release |

You can use `dotnet test` for quick feedback, but a full `make test` run should be completed before finalizing changes to ensure nothing is missed.

---

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
- For markdown (`.md`) files, ensure there is no trailing whitespace at the end of any line.
- When adding new public APIs, include XML documentation comments.

---

## Architecture Overview

PolyType is a practical generic programming library for .NET. It provides a type model that describes the structure of .NET types, enabling the rapid development of high-performance libraries such as serializers, validators, parsers, and mappers.

### Key Concepts

- **Type shapes** — Core abstractions (`ITypeShape`, `IObjectTypeShape`, `IEnumerableTypeShape`, `IDictionaryTypeShape`, etc.) that describe the structure of types: their properties, constructors, collection semantics, enum values, and more.
- **Two providers** — The type model can be populated via a **reflection provider** (for runtime use) or a **source generator** (for Native AOT and trimming support).
- **Visitor pattern** — `TypeShapeVisitor` allows consumers to traverse the type model and build functionality generically over arbitrary types.
- **`IShapeable<T>`** — Types implement this interface (typically via source generation) to advertise that they can provide their own type shape.
- **Attributes** — `[GenerateShape]`, `[PropertyShape]`, `[ConstructorShape]`, etc. control how the source generator produces type shapes.

---

## Project Layout

### Source Projects (`src/`)

- **`PolyType/`** — Core library. Contains the type shape abstractions, visitor pattern, `IShapeable<T>`, attributes for source generation, the reflection-based type shape provider, and model classes used by the source generator. Targets net10.0/net9.0/net8.0/net472/netstandard2.0. Packable.
- **`PolyType.Roslyn/`** — Roslyn library for extracting general-purpose type data models from `ITypeSymbol`s. Provides the foundation used by the source generator. Models in this project are **not** designed for direct use by incremental source generators (they may contain non-equatable data). Also provides `ImmutableEquatableArray<T>`, `ImmutableEquatableDictionary<TKey, TValue>`, and `ImmutableEquatableSet<T>` for use in incremental pipelines. Targets netstandard2.0. Packable (for use by third-party source generators).
- **`PolyType.SourceGenerator/`** — Built-in incremental source generator. Consumes PolyType.Roslyn for model extraction, maps to its own incremental-safe model types, and emits C# source. Targets netstandard2.0 (analyzer project).
- **`PolyType.Examples/`** — Reference implementations built on PolyType: JSON/XML/CBOR serializers, configuration binder, DI container, pretty-printer, random generator, JSON schema generator, cloner, structural equality comparer, validator, and object mapper. Packable.
- **`PolyType.Examples.FSharp/`** — F# example implementations (pretty-printer). Not packable.
- **`PolyType.TestCases/`** + **`PolyType.TestCases.FSharp/`** — Shared test type definitions used across test projects. Provides exhaustive type coverage for testing PolyType consumers. Packable.
- **`Shared/`** — Shared helper code and polyfills: reflection utilities, debug helpers, and compatibility shims for older target frameworks.

### Test Projects (`tests/`)

- **`PolyType.Tests/`** — Main test suite. Comprehensive xUnit tests covering serialization, type shapes, caching, validation, collections, DI, and more. Targets net10.0/net9.0/net8.0/net472.
- **`PolyType.Tests.NativeAOT/`** — Native AOT smoke tests. Published as an AOT binary and run directly to establish that basic functionality works under AOT constraints. Not intended to be comprehensive — the main test suite in `PolyType.Tests/` provides full coverage. Targets net10.0 only. Uses TUnit.
- **`PolyType.SourceGenerator.UnitTests/`** — Source generator unit tests. Tests compilation, diagnostics, incremental compilation behavior, and code generation output. Targets net9.0/net8.0 (+ net472 on Windows). xUnit.
- **`PolyType.Roslyn.Tests/`** — Tests for Roslyn utility types (equatable collections, source writer). Targets net10.0/net9.0/net8.0/net472. xUnit.
- **`PolyType.Benchmarks/`** — BenchmarkDotNet performance benchmarks. Not a test project — used for profiling and optimization. Targets net10.0.

### Sample Applications (`applications/`)

Six Native AOT console apps and one reflection-based app demonstrating serialization, configuration binding, object mapping, random generation, and validation. All AOT apps target net10.0 with `PublishAot=true`.

---

## Source Generator Guidance

The source generator is split into two separate components:

- **`PolyType.Roslyn`** extracts general-purpose `TypeDataModel` objects from Roslyn `ITypeSymbol`s. These models are not meant for use by incremental source generators — they may contain non-equatable or non-serializable data.
- **`PolyType.SourceGenerator`** consumes PolyType.Roslyn's models and maps them to its own model types (in `PolyType.SourceGenerator/Model/`) that are designed for incremental generator pipelines. It uses the attribute annotations specified in the core PolyType project.

### Incremental Generator Requirements

Source generator models in `PolyType.SourceGenerator` **MUST** abide by the principles detailed in the [Roslyn Incremental Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md):

- **Use `record` types** for pipeline models to get structural equality for free. Never use classes with reference equality.
- **Never store Roslyn symbols** (`ISymbol`, `ITypeSymbol`, etc.) in pipeline models. Extract the information you need into equatable representations (strings, records, etc.).
- **Do not use standard collection types** (arrays, `List<T>`, `Dictionary<TKey, TValue>`) which have reference equality. Use the provided equatable collections from `PolyType.Roslyn/IncrementalTypes/`:
  - `ImmutableEquatableArray<T>`
  - `ImmutableEquatableDictionary<TKey, TValue>`
  - `ImmutableEquatableSet<T>`
- Pipeline steps should short-circuit when models haven't changed, allowing the generator driver to reuse cached output.

### Pipeline Overview

`PolyTypeGenerator.cs` → `Parser` (extracts models from Roslyn symbols) → `Parser.ModelMapper` (maps models for generic factory derivation) → `SourceFormatter` (emits C# source code)

Unit tests in `PolyType.SourceGenerator.UnitTests/` validate the generated output.

---

## Testing Patterns

- The main test suite in `PolyType.Tests/` targets four frameworks: net10.0, net9.0, net8.0, and net472.
- Shared test type definitions live in `PolyType.TestCases` — add new type scenarios there to get coverage across all test configurations.
- The `ProviderUnderTest` abstraction allows testing both reflection and source-generated providers with the same test logic.
- NativeAOT smoke tests in `PolyType.Tests.NativeAOT/` are published and run as an AOT binary. They verify basic functionality under AOT constraints but are not comprehensive.
- For quick iteration: `dotnet test --framework net10.0`
- For full validation: `make test` (runs both CLR and AOT tests)

---

## Documentation

- [DocFX](https://dotnet.github.io/docfx/) generates the project website from the `docs/` directory.
- API documentation is auto-generated from XML doc comments in source code.
- Conceptual documentation lives in `docs/docs/`.
- Build docs: `make generate-docs`
- Serve locally: `make serve-docs` (port 8080)
- When adding new public APIs, always include XML documentation comments.

---

## Things to Avoid / Common Gotchas

- **Multi-targeting** — The core library targets net10.0, net9.0, net8.0, net472, and netstandard2.0. Be aware of API availability differences across target frameworks.
- **Source generator targets netstandard2.0** — This is a Roslyn analyzer requirement. Do not use APIs unavailable in netstandard2.0 within the source generator.
- **PolyType.Roslyn models ≠ PolyType.SourceGenerator models** — These are distinct model hierarchies with different design goals. Don't confuse them.
- **Strong naming** — All assemblies are signed with `OpenKey.snk`.
- **Versioning** — Nerdbank.GitVersioning (nbgv) manages versions from `version.json`. Don't manually edit assembly versions.
- **Package validation** — Enabled with a baseline of v1.0.0. Breaking public API changes will fail the build.

---

## Keeping This Document Current

Any changes that substantially update the project structure — adding, removing, or renaming projects; changing target frameworks; or altering the build pipeline — should also trigger updates to this `copilot-instructions.md` file to keep it accurate.
