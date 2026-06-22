# Threat model

This document describes the security posture of the PolyType **core programming model** and its two built-in shape providers — the [reflection provider](shape-providers.md) and the [source generator](source-generator.md). It is intended for two audiences:

- **Maintainers**, as the reference for the security invariants the library commits to.
- **Consumers** building libraries on top of PolyType (serializers, validators, mappers, binders, and similar), as a description of which threats PolyType mitigates and which remain the consumer's responsibility.

It builds on the [.NET baseline security assumptions](https://github.com/dotnet/core/blob/main/Documentation/security-foundations/baseline-security-assumptions.md): the runtime, base class libraries, and the C# compiler are trusted, and an attacker cannot alter the application binary, its dependencies, or the build environment. Threats that the baseline already excludes are not repeated here.

## Summary

PolyType is a generic programming facility, not a data-processing component. It consumes a program's **control plane** — .NET `Type` instances, the program's own source code, and PolyType's own attributes — and produces a *type model*: a set of [type shapes](core-abstractions.md) together with strongly-typed accessor delegates (reflection provider) or generated C# source (source generator).

Crucially, **PolyType never inspects, parses, or makes control-flow decisions based on untrusted runtime data.** The untrusted-data boundary — where a hostile payload first meets application logic — lives entirely in the *consumer* that drives runtime values through the shapes PolyType produces. As a result, the classic data-plane threats associated with serializers (resource exhaustion from hostile payloads, deserialization of untrusted data into dangerous types, algorithmic complexity attacks, and so on) are **out of scope** for PolyType and owned by its consumers.

PolyType's own security contract is therefore narrow and is expressed as a set of invariants about *what shape it produces for a given type* and *how it produces it*. The remainder of this document defines the trust boundary precisely, classifies every input, enumerates the out-of-scope (consumer-owned) threats, and states the in-scope invariants PolyType upholds.

## Trust boundary

### Actors and authority

| Actor | Trust | Role |
| --- | --- | --- |
| Application developer | Trusted | Authors the types, applies PolyType attributes, selects the provider, and configures provider options. All of this is fixed at build/deploy time. |
| The C# compiler / runtime / BCL | Trusted | Per the .NET baseline assumptions. The source generator runs inside the trusted compiler; the reflection provider runs on the trusted runtime. |
| PolyType | Trusted component under analysis | Transforms the developer's control-plane inputs into a type model. |
| Consumer library | Trusted code, untrusted inputs | Uses the type model to read/write **runtime data**, which may be attacker-controlled. This is where the untrusted-data boundary lives. |
| Runtime payload (JSON, CBOR, form data, …) | **Untrusted** | Never seen by PolyType. Crosses the boundary only inside the consumer. |

### The taint seam

```text
  developer-authored (control plane)            runtime values (data plane)
  ┌───────────────────────────────┐             ┌──────────────────────────┐
  │ Type / typeof(T)               │             │  untrusted payload bytes │
  │ program source + attributes    │             └───────────┬──────────────┘
  │ provider options               │                         │
  └───────────────┬───────────────┘                         │  ← taint seam
                  │                                          │   (inside consumer)
                  ▼                                          ▼
            ┌───────────┐   type model (shapes,      ┌────────────────┐
            │ PolyType  │ ─ accessors, generated ──▶ │ Consumer logic │
            └───────────┘   source)                  └────────────────┘
```

PolyType sits entirely on the left of the seam. Everything it consumes is authored by the trusted developer; everything it emits is a *template* that the consumer later applies to data. PolyType performs no I/O against the payload and holds no payload state.

## Inputs and outputs

The following enumeration is exhaustive for the core model and both providers. Every input is control-plane; none is derived from untrusted runtime data.

### Reflection provider

- **Inputs:** `System.Type` instances (obtained from `typeof(T)` or reflection over developer-authored types), `ReflectionTypeShapeProviderOptions`, and PolyType attributes read via reflection. All are supplied by the developer.
- **Outputs:** `ITypeShape` instances backed by accessor delegates. Where reflection emit is available, accessors are dynamic methods; otherwise a reflection-based fallback is used. Both paths require dynamic code and are annotated accordingly (see invariant **G**).

### Source generator

- **Inputs:** the Roslyn `Compilation` (symbols and syntax for the program being compiled) plus PolyType attributes — `[GenerateShape]`, `[GenerateShapeFor]`, `[PropertyShape]`, `[ConstructorShape]`, marshaler attributes, and the like. All exist in the developer's own source.
- **Outputs:** C# source compiled into the developer's assembly. The generator runs at compile time and emits no runtime behavior of its own beyond the code it generates.

### The single runtime touch: marshalers

When a type opts into a [surrogate](specification.md) via a marshaler attribute, the reflection provider instantiates the developer-specified marshaler type during shape construction. The marshaler type is named in an attribute (control plane) and is never selected by runtime data, so this remains inside the trust boundary. It is called out only because it is the one place where shape *construction* runs developer code eagerly.

## Out of scope (consumer-owned threats)

The following threats are **not** mitigated by PolyType because PolyType never touches the data plane. Each is the responsibility of the consumer library, exactly as it would be for a hand-written serializer. The [System.Text.Json threat model](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Json/docs/ThreatModel.md) is a good reference for how a data-plane consumer should reason about these.

1. **Payload resource exhaustion** — unbounded input length, document size, or token count. PolyType imposes no payload limits because it sees no payload; the consumer must cap input size and processing time.
2. **Deserialization depth / stack exhaustion** — deeply nested untrusted documents. PolyType describes a type's structure; the consumer must enforce a maximum traversal depth when reading data.
3. **Cyclic data** — reference cycles in runtime object graphs. Cycle detection during read/write is a consumer concern.
4. **Deserialization of dangerous types** — a payload selecting a type to instantiate. PolyType never instantiates a type chosen by data (invariant **C**); a consumer that implements polymorphic/`$type` dispatch must restrict the permitted type set itself.
5. **Algorithmic complexity / hash-flooding** — a payload crafted to degrade dictionary or set performance. PolyType surfaces the collection's comparer choice but does not select it; the consumer must choose collision-resistant comparers where untrusted keys are involved.
6. **Information disclosure through serialized output** — emitting a member whose value is sensitive. PolyType faithfully projects the members the developer exposed (subject to invariant **A**); deciding what is safe to write is a consumer/developer policy.
7. **Mass-assignment / over-posting** — a payload setting members the developer did not intend to be writable from untrusted input. The shape reports which members are writable; binding policy is the consumer's responsibility.
8. **Numeric / culture parsing of payload values** — overflow, locale-sensitive parsing, lossy conversions of *data*. PolyType performs no payload parsing.
9. **Injection through deserialized values** — SQL/command/path injection from attacker-controlled strings. PolyType does not interpret values.
10. **Concurrency on consumer state** — thread-safety of the consumer's own caches and buffers. PolyType's shape caches are described under invariant **E/G notes**, but consumer state is the consumer's concern.
11. **Timing / side-channel observation of data** — comparisons over secret values. PolyType performs no value comparisons.
12. **Denial of service via shape construction** — pathologically large or deeply recursive *type graphs*. Type graphs are finite, developer-authored, and resolved once and cached; they are not attacker-controlled. Recursive type definitions are handled explicitly by cycle detection during shape construction, and type-graph depth is bounded by the CLR/IL representation limits of the program itself, so shape construction is not a stack-exhaustion vector.

## In-scope invariants

These are the guarantees PolyType commits to. They are the library's positive security contract; a regression in any of them is a security-relevant defect.

- **A. No non-public member is surfaced without explicit opt-in.** By default, only public properties/fields with public accessors are projected. Non-public members are included only when the developer applies `[PropertyShape]` (or the equivalent provider option). This keeps a type's data contract aligned with its public surface unless the developer says otherwise.
- **B. Provider parity.** The reflection provider and the source generator project the *same* shape for the same type. A consumer's security review performed against one provider must hold for the other.
- **C. No data-driven type loading or instantiation.** PolyType never calls `Type.GetType(string)`, `Assembly.Load`, or equivalent. All generic instantiation derives from caller-supplied `Type`/`typeof(T)`, attribute-named marshalers, or attribute-named providers — never from runtime data.
- **D. No ambient I/O or environment dependence.** Neither provider reads files, network, environment variables, or the clock as part of producing shapes. (The sole exception is a debug-only, opt-in `Debugger.Launch` hook in the source generator, gated behind an environment variable that has no effect in normal builds.)
- **E. Accessors do only what they advertise.** A property getter reads exactly that property; a setter writes exactly that property; a constructor invokes exactly that constructor. Accessors carry no hidden side effects beyond the member they represent.
- **F. Deterministic, culture-independent generation.** For a fixed input compilation the source generator emits byte-for-byte identical output: members are ordered deterministically, equatable collections back the incremental pipeline, and no `Guid`/`DateTime`/`Random`/culture-sensitive formatting leaks into the output.
- **G. Correct AOT / trimming annotations.** The reflection provider is annotated `[RequiresDynamicCode]` and `[RequiresUnreferencedCode]` at the type level, and even its non-emit fallback requires dynamic code — there is no silently AOT-unsafe path. The source-generator path is fully AOT- and trim-safe by construction.

### Capability surface note

Beyond data-shape description, the core model can also expose `IMethodShape`, `IEventShape`, and `IFunctionTypeShape`, which enable invoking methods and subscribing to events. These are **opt-in**: method and event inclusion default to off, and including non-public members additionally requires a per-member attribute. Consumers that enable these capabilities take on the corresponding responsibility for what they invoke.

## Guidance for consumers

Because PolyType hands you a faithful — and potentially broad — projection of a developer's type, a library that drives **untrusted data** through that projection must add the data-plane defenses PolyType deliberately omits:

- Enforce a maximum traversal **depth** and overall input **size**/time budget when reading untrusted input.
- Detect or bound **cycles** in object graphs you construct.
- For polymorphic dispatch, restrict the set of **permitted types**; never instantiate a type named purely by the payload.
- Choose **collision-resistant comparers** for dictionaries and sets populated from untrusted keys.
- Apply your own **binding policy** (allow-lists, `[PropertyShape]` review) to decide which writable members untrusted input may set, and which readable members are safe to emit.
- Treat the shape as a *description*, not a *sanitizer*: value validation, encoding, and injection defenses remain your responsibility.
