# AGENTS.md — PolyType

Scoped guidance for working inside the core PolyType library. This complements the repository-root [`AGENTS.md`](../../AGENTS.md): the root rules still apply, and this file takes precedence for files under `src/PolyType/`.

## What This Project Is

`PolyType` is the core, packable runtime library and the single assembly that consumers reference. It defines the type-model abstractions, the attributes that drive source generation, and **both** providers that populate the model — the runtime reflection provider and the runtime support types for the source generator. It targets **net10.0;net9.0;net8.0;net472;netstandard2.0** and is AOT-compatible on net8.0 and newer.

A few cross-cutting facts:

- The project references `PolyType.SourceGenerator` as an analyzer and packs that generator into the NuGet package (under `analyzers/dotnet/cs`), so installing PolyType brings the generator along.
- `IShapeable<T>` and other static-abstract-interface features only exist on **net8.0+** targets; guard such APIs accordingly when touching multi-targeted code.
- Package validation runs against the **v1.0.0** baseline, so avoid breaking public API changes. Include XML docs on new public APIs.
- Some helpers and polyfills are linked in from [`../Shared/`](../Shared); they are not defined in this project.

## Project Structure

### The public type model — two namespaces, two audiences

The public abstractions are split across two namespaces, distinguished by **who consumes them**, not by which folder they live in. Both are "abstractions" conceptually; the difference is the intended caller.

#### `PolyType` namespace — end-user facing

These types live in the **root namespace** because they are consumed directly by *end users*: the developers who use a library built on PolyType and annotate their own types so that library can understand them. They are mostly defined in the top-level `*.cs` files:

- **Attributes** end users apply to their own types, honoured by both providers — `GenerateShapeAttribute`, `PropertyShapeAttribute`, `ConstructorShapeAttribute`, and friends.
- **Entry-point types**: `ITypeShape`, `ITypeShapeProvider`, `IShapeable<T>` (`IShapeableOfT.cs`), `TypeShapeKind`, and similar. (`TypeShapeRequirements` also belongs to this root namespace even though its file sits under `Abstractions/` — folder and namespace are not 1:1 here.)

#### `PolyType.Abstractions` namespace — library-author facing

These types live under `Abstractions/` and are meant to be consumed by *libraries that depend on PolyType* to build functionality generically over user-defined types — not by end users directly. This is the heart of the type model:

- **Shape interfaces** that describe a type's structure, one per `TypeShapeKind` — e.g. `IObjectTypeShape`, `IEnumerableTypeShape`, `IDictionaryTypeShape`, `IUnionTypeShape` — down to member-level shapes such as `IPropertyShape` and `IConstructorShape`.
- **`TypeShapeVisitor`** — the visitor a consuming library subclasses to build functionality generically over arbitrary types.
- **Supporting model types** — resolvers, extension helpers, collection-construction descriptors, and small primitives (`Unit`, `ParameterKind`, etc.).

> **The split is by audience, not folder.** End users interact with the `PolyType` namespace (mostly the attributes); libraries built on PolyType program against `PolyType.Abstractions`. Keep new APIs in the namespace that matches their intended consumer.

### `ReflectionProvider/`

The **runtime reflection-based provider**: `ReflectionTypeShapeProvider` uses `System.Reflection` to build shapes on the fly, with no build-time step. Where available it uses `Reflection.Emit`-based member accessors (under `MemberAccessors/`) for performance, falling back to plain reflection otherwise. These implementations are **not trimming- or AOT-safe** — they depend on unreferenced/dynamic code — so the source-generated provider is the path for those scenarios.

### `SourceGenModel/`

The runtime model types targeted by the source generator (`src/PolyType.SourceGenerator/`): the emitted code instantiates these to build shapes at compile time, making this the trimming- and AOT-safe provider.

> When changing the shape of generated code in the source generator, the `SourceGenModel/` types here usually need matching updates — the two evolve together.

### `Utilities/`

Cross-cutting runtime helpers:

- **`Caching/`** — `TypeCache`, `MultiProviderTypeCache`, and `TypeGenerationContext` cache resolved shapes; `DelayedValue` / `IDelayedValueFactory` break cycles when resolving recursive type graphs.
- `AggregatingTypeShapeProvider` composes multiple providers; `ReflectionUtilities` holds shared reflection helpers.

### `Debugging/`

`DebuggerProxies.cs` — debugger display proxies that make the type model easier to inspect in the debugger.

## The Two Providers

A consumer obtains an `ITypeShape` from one of two providers, both living in this assembly:

- The **reflection provider** (`ReflectionProvider/`) resolves shapes at runtime — convenient, no build-time step, but not trimming/AOT-friendly when it depends on `Reflection.Emit`.
- The **source-generated provider** (`SourceGenModel/` here + the generator project) resolves shapes from code emitted at compile time — the trimming- and Native-AOT-safe path.

Both populate the same `Abstractions/` model, so consumers written against the abstractions work identically regardless of which provider supplied the shape. Keep that substitutability intact: behavioural changes should be made to the abstractions/model so both providers stay in agreement.
