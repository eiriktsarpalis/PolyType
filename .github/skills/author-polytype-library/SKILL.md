---
name: author-polytype-library
description: >-
  Author, extend, or audit libraries built on PolyType type shapes, including
  serializers, validators, mappers, and comparers. Use for library architecture
  and implementation, not ordinary application consumption or Roslyn generator
  authoring.
---

# Author a PolyType library

Focus on PolyType architecture and efficient visitor construction. Use the [official documentation](https://eiriktsarpalis.github.io/PolyType/docs/core-abstractions.html) and [API reference](https://eiriktsarpalis.github.io/PolyType/api/PolyType.html) for detailed mechanics.

## Construction and execution

Separate one-time construction from per-value execution. Use `TypeShapeVisitor` to compose reusable typed artifacts: delegates, converters, comparers, or other components. Execute those artifacts without traversing type metadata for each value.

Hoist type- and immutable-configuration-bound work into construction. Precompute lookups, constants, and access plans; specialize the returned artifacts rather than merely caching a generic execution path. Examples include [rank/type-specific JSON converters](https://github.com/eiriktsarpalis/PolyType/blob/a805ef3bfbef9aeb0b4c5ab5d579a888c528adb1/src/PolyType.Examples/JsonSerializer/JsonSerializer.Builder.cs#L151-L169) and [Counter's member-count-based delegate selection](https://github.com/eiriktsarpalis/PolyType/blob/a805ef3bfbef9aeb0b4c5ab5d579a888c528adb1/src/PolyType.Examples/Counter/Counter.Builder.cs#L16-L27). Keep genuinely value-dependent work, such as an array's actual lengths, in execution.

Obtain typed accessors and constructors from shapes. Do not rediscover structure through direct reflection or generate runtime code independently.

## Caching and recursive types

Caching and recursive-type support are required. `MultiProviderTypeCache` partitions caches by provider; `TypeCache` stores reusable artifacts; `TypeGenerationContext` coordinates construction. Resolve child artifacts through the same context and scope caches to result-affecting configuration.

Cache initializer fragment, where `Builder` is your visitor implementing `ITypeShapeFunc` and `DelayedFactory` implements `IDelayedValueFactory`:

```csharp
ValueBuilderFactory = ctx => new Builder(ctx),
DelayedValueFactory = new DelayedFactory(),
```

Delayed construction ties the recursive knot, effectively a Y combinator for recursive artifacts. For a delegate-shaped artifact, the self-reference is:

```csharp
new DelayedValue<Func<T, TResult>>(self => value => self.Result(value))
```

Dereference delayed results during execution, not construction. Forwarding converters use the [same pattern](https://github.com/eiriktsarpalis/PolyType/blob/main/src/PolyType.Examples/JsonSerializer/Converters/DelayedJsonConverterFactory.cs).

Recursive types and cyclic values are separate concerns. Where applicable, use tokenizer-layer depth enforcement to bound traversal of cyclic/deep values. A depth bound is neither exact cycle detection nor identity preservation; reference preservation is a separate optional feature, not something delayed construction provides.

## Public API

Follow the [JSON example's API design](https://github.com/eiriktsarpalis/PolyType/blob/main/src/PolyType.Examples/JsonSerializer/JsonSerializer.cs). For libraries targeting only modern .NET, prefer `IShapeable<T>`-constrained entry points, including witnesses where needed. For `netstandard2.0` compatibility, use equivalent APIs with dynamic generated-shape resolution through `TypeShapeResolver`, not a reflection-provider fallback.

Keep `ITypeShape<T>` an implementation detail for entry-level use; reserve explicit shape parameters for advanced APIs. Isolate reflection-provider opt-in behind distinct entry points so `RequiresUnreferencedCode` and `RequiresDynamicCode` annotations do not permeate the ordinary API surface.

## Coverage and testing

Support applicable shape kinds broadly: objects/scalars, enums, optionals, enumerables, dictionaries, unions, and surrogates. Support function/member scenarios where meaningful. Reject unsupported shapes or operations explicitly.

Use grounded, library-specific behavioral tests alongside theories driven by [PolyType.TestCases](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.TestCases). The shared cases provide a complete, representative set of supported types, ensuring type-coverage completeness without redundant fixture catalogues. They complement, rather than replace, tests of the library's semantics.

## Additional resources

- [Nerdbank.MessagePack](https://github.com/AArnott/Nerdbank.MessagePack): the primary production-grade reference for a library built on PolyType.
- [PolyType.Examples](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples): focused examples of visitor-based libraries.
