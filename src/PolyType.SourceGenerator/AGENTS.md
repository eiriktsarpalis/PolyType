# AGENTS.md — PolyType.SourceGenerator

Scoped guidance for working inside the built-in incremental source generator. This complements the repository-root [`AGENTS.md`](../../AGENTS.md): the root rules still apply, and this file takes precedence for files under `src/PolyType.SourceGenerator/`.

## What This Project Is

`PolyType.SourceGenerator` is the built-in C# incremental source generator that emits `ITypeShape` implementations for types annotated with `[GenerateShape]` (and related attributes). It targets **netstandard2.0** because it ships as a Roslyn analyzer — do not use APIs unavailable on netstandard2.0 here.

## Architecture: Two Components

Model extraction and code emission are split across two projects:

- **`PolyType.Roslyn`** extracts general-purpose `TypeDataModel` objects from Roslyn `ITypeSymbol`s. These models are **not** incremental-safe — they may hold non-equatable or non-serializable data (including Roslyn symbols).
- **`PolyType.SourceGenerator`** (this project) consumes those models and maps them into its own incremental-safe model types under `Model/`, then emits source. It reads the attribute annotations defined in the core PolyType project.

> **PolyType.Roslyn models ≠ PolyType.SourceGenerator models.** They are distinct hierarchies with different design goals. Never feed a raw PolyType.Roslyn model (or a Roslyn symbol) into the incremental pipeline.

## Incremental Generator Requirements

Everything that flows through the incremental pipeline (the `Model/` types) **MUST** abide by the principles in the [Roslyn Incremental Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md):

- **Use `record` types** for pipeline models to get structural equality for free. Never use classes with reference equality.
- **Never store Roslyn symbols** (`ISymbol`, `ITypeSymbol`, etc.) in pipeline models. Extract the information you need into equatable representations (strings, records, etc.).
- **Do not use standard collection types** (arrays, `List<T>`, `Dictionary<TKey, TValue>`) — they use reference equality. Use the equatable collections from `PolyType.Roslyn/IncrementalTypes/`:
  - `ImmutableEquatableArray<T>`
  - `ImmutableEquatableDictionary<TKey, TValue>`
  - `ImmutableEquatableSet<T>`
- Pipeline steps should short-circuit when models are unchanged, so the generator driver can reuse cached output.

## Separation of Concerns: Parser vs Formatter

The pipeline has a strict division of labour, and keeping it strict is what makes the generator both correct and fast:

- **The parser maps symbols to the incremental model.** All inspection of Roslyn symbols and the compilation happens here. This is the only place that should reason about type semantics, resolve members, evaluate attributes, or decide what to emit.
- **The source formatter maps the incremental model to source code.** It is a straightforward pretty printer over the model and should not perform any nontrivial computation of its own.

Two rules follow from this:

- **The incremental model must not encapsulate any symbols or compilation objects.** Beyond the equatable-model requirements above, holding onto symbols defeats incremental caching and pins large Roslyn object graphs in memory — a real performance problem. Extract everything the formatter needs into plain, equatable data while parsing.
- **Nontrivial computation in the formatter is a design smell.** If the formatter finds itself computing something non-obvious (resolving a name, choosing a strategy, deriving a flag), that is a signal the computation belongs in the parser. Move it there and update the model to carry the result.

Balance this against a competing goal: **keep the incremental model as normalized as possible.** Pushing work into the parser must not become an excuse to stuff the model with redundant or trivially derivable data — every field participates in the structural equality check that gates incremental caching, so redundant state makes those comparisons more expensive and weakens cache hit rates. Prefer carrying the minimal, canonical data the formatter needs; only precompute and store a value when the parser-side computation is genuinely nontrivial or depends on symbols/compilation state the formatter must not see.

## Project Layout & Pipeline

- **`PolyTypeGenerator.cs`** — the `[Generator]` entry point that wires up the incremental pipeline.
- **`PolyTypeKnownSymbols.cs`** — cached well-known symbol lookups.
- **`Parser/`** — extracts and validates models from Roslyn symbols. `Parser.ModelMapper.cs` maps them for generic factory derivation; `Parser.Diagnostics.cs` defines the reported diagnostics.
- **`Model/`** — the incremental-safe (record-based) model types.
- **`SourceFormatter/`** — emits the C# source, split by shape kind (`SourceFormatter.Object.cs`, `SourceFormatter.Enumerable.cs`, `SourceFormatter.Dictionary.cs`, etc.).
- **`Analyzers/`** — companion analyzers shipped alongside the generator.

Flow: `PolyTypeGenerator.cs` → `Parser` (Roslyn symbols → models) → `Parser.ModelMapper` (maps models for generic factory derivation) → `SourceFormatter` (models → C# source).

## Testing Changes

Unit tests live in `tests/PolyType.SourceGenerator.UnitTests/` and cover compilation, diagnostics, incremental behavior, and generated output.

Generated output is verified with **snapshot tests** (`SnapshotTests.cs`); baselines live under `Snapshots/{TFM}-{Configuration}/`. When a change intentionally alters the generated source, refresh the baselines:

```bash
dotnet msbuild tests/PolyType.SourceGenerator.UnitTests/PolyType.SourceGenerator.UnitTests.csproj -t:UpdateSnapshots
```

This rebuilds Debug + Release and rewrites the snapshot files with `POLYTYPE_UPDATE_SNAPSHOTS=true`. Review the diff before committing, then run the suite normally (`dotnet test`) to confirm everything passes.
