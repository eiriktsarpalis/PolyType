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

### Public root surface (`src/PolyType/*.cs`)

The top-level files form the primary public entry points:

- **Attributes** that the source generator and reflection provider both honour: `GenerateShapeAttribute`, `GenerateShapeForAttribute`, `TypeShapeAttribute`, `PropertyShapeAttribute`, `ConstructorShapeAttribute`, `ParameterShapeAttribute`, `MethodShapeAttribute`, `EventShapeAttribute`, `DerivedTypeShapeAttribute`, `EnumMemberShapeAttribute`, `AssociatedTypeShapeAttribute`, `TypeShapeExtensionAttribute`.
- **Core entry-point types**: `ITypeShape`, `ITypeShapeProvider`, `IShapeable<T>` (`IShapeableOfT.cs`), `IMarshaler`, `TypeShapeKind`, `MethodShapeFlags`, and `TypeShapeProviderExtensions`.

### `Abstractions/`

The heart of the type model — the shape interfaces a consumer traverses, plus the visitor:

- **Shape interfaces**: `IObjectTypeShape`, `IEnumerableTypeShape`, `IDictionaryTypeShape`, `IEnumTypeShape`, `IOptionalTypeShape`, `ISurrogateTypeShape`, `IUnionTypeShape` / `IUnionCaseShape`, `IFunctionTypeShape`, and the member-level shapes `IConstructorShape`, `IParameterShape`, `IPropertyShape`, `IMethodShape`, `IEventShape`.
- **`TypeShapeVisitor`** — the visitor consumers subclass to build functionality generically over arbitrary types.
- **Supporting model types**: `TypeShapeResolver`, `TypeShapeExtensions`, `TypeShapeRequirements`, collection-construction descriptors (`CollectionConstructionStrategy`, `CollectionConstructionOptions`, `CollectionComparerOptions`, `DictionaryInsertionMode`), and primitives like `IArgumentState`, `ITypeShapeFunc`, `ParameterKind`, and `Unit`.

### `ReflectionProvider/`

The **runtime reflection-based provider**. `ReflectionTypeShapeProvider` (configured via `ReflectionTypeShapeProviderOptions`) builds shapes on the fly from `System.Reflection` metadata, with one `Reflection*TypeShape` implementation per shape kind and dedicated F# support (`FSharpUnionTypeShape`, `FSharpOptionTypeShape`, `FSharpFunctionTypeShape`, etc.).

- **`MemberAccessors/`** abstracts member get/set/construct behind `IReflectionMemberAccessor`, with two strategies: `ReflectionEmitMemberAccessor` (uses `Reflection.Emit` for speed where available) and `ReflectionMemberAccessor` (a pure-reflection fallback usable under AOT/no-emit).

### `SourceGenModel/`

The **runtime counterparts the generated code instantiates**. The source generator (`src/PolyType.SourceGenerator/`) emits code that wires up `SourceGenTypeShapeProvider` and the `SourceGen*TypeShape` types defined here. `Helpers/` holds the supporting machinery the emitted code relies on (argument-state implementations such as `SmallArgumentState` / `LargeArgumentState`, `CollectionHelpers`, marshalling, etc.).

> When changing the shape of generated code in the source generator, the corresponding `SourceGenModel/` types here usually need matching updates — the two evolve together.

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
