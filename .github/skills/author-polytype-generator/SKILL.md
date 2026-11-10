---
name: author-polytype-generator
description: >-
  Create, extend, review, or debug C# source generators built with PolyType.Roslyn.
  Use for compile-time type-model extraction, incremental generator architecture,
  and model-to-source generation, not runtime visitor-based PolyType libraries or
  unrelated Roslyn analyzer work.
---

# Author a PolyType-based source generator

Default to standalone, domain-specific generated output without a dependency on PolyType runtime shapes. Use the [Roslyn Incremental Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md) as the architectural authority.

## Package setup

- Reference the source-only `PolyType.Roslyn` package directly with `PrivateAssets="all"`; its sources compile into your generator, not a separately distributed assembly.
- Build the generator with a C# 14-capable compiler and Roslyn references compatible with its intended hosts. These are separate from the language version of applications it processes.
- Supply compiler-support types required by older target frameworks, using PolySharp or equivalent definitions. See the [package README](https://github.com/eiriktsarpalis/PolyType/blob/main/src/PolyType.Roslyn/README.md) for configuration.

## Parser, model, and emitter

Keep parsing and semantic decisions separate from formatting:

1. **Parser:** inherit and configure `TypeDataModelGenerator` for the domain. Extract the relevant Roslyn type information and project it into the generator's own model.
2. **Model:** use immutable records and the provided equatable collections. Keep live symbols and `TypeDataModel` objects out of cached emission models; value equality must account for everything that affects generated output.
3. **Emitter:** format the domain model with `SourceWriter`, without revisiting semantic analysis. Keep compiler state transient so unrelated edits can reuse equal models and cached emission.

`SourceWriter` applies its current indentation to each line of a multiline raw string:

```csharp
using SourceWriter writer = new() { Indentation = 1 };
writer.WriteLine("""
    internal sealed class Person
    {
        public int Age { get; init; }
    }
    """);
```

With the default four-space indent, this writes:

```csharp
    internal sealed class Person
    {
        public int Age { get; init; }
    }
```

C# removes the raw-string closing delimiter's indentation; `SourceWriter` then adds its current indentation to each line while preserving relative indentation. It does not infer indentation from braces. [Interpolated raw strings work the same way](https://github.com/eiriktsarpalis/PolyType/blob/a805ef3bfbef9aeb0b4c5ab5d579a888c528adb1/tests/PolyType.Roslyn.Tests/SourceWriterTests.cs#L58-L90).

## Verification

Compile a consumer and exercise the generated API. Check expected diagnostics for unsupported input and verify that an unrelated edit reuses cached emission. Follow the consuming project's test conventions rather than prescribing a framework or snapshot layout.

PolyType's `CompilationHelpers` builds in-memory `CSharpCompilation` instances with explicit references and parse options, then runs the generator through `RunGeneratorsAndUpdateCompilation`. The harness checks generator and generated-code diagnostics, logs generated sources, and enables incremental-step tracking. Adapt that compilation/driver setup rather than copying PolyType-specific fixtures or model hooks. For edit-based incrementality checks, retain the driver between runs and inspect tracked run reasons; identical output text alone does not demonstrate cache reuse.

## Additional resources

Use these as architecture references, not generated contracts or sample-specific policies to reproduce:

- [Built-in PolyType generator](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.SourceGenerator).
- [Independent FastPathJson generator](https://github.com/eiriktsarpalis/PolyType/tree/main/src/fast-path-json/PolyType.FastPathJson.SourceGenerator).
- [Source-generator test infrastructure](https://github.com/eiriktsarpalis/PolyType/blob/main/tests/PolyType.SourceGenerator.UnitTests/CompilationHelpers.cs).
