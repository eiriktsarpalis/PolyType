---
_layout: landing
---

# Overview

PolyType is a practical generic programming library for .NET. It facilitates the rapid development of feature-complete, high-performance libraries that interact with user-defined types. This includes serializers, RPC frameworks, structured loggers, mappers, validators, parsers, random generators, and equality comparers. Its built-in source generator ensures that any library built on top of PolyType gets [Native AOT support for free](https://eiriktsarpalis.wordpress.com/2024/10/22/source-generators-for-free/).

The project is a port of the [TypeShape](https://github.com/eiriktsarpalis/TypeShape) library for F#, adapted to patterns and idioms available in C#. The name PolyType is a reference to [polytypic programming](https://en.wikipedia.org/wiki/Polymorphism_(computer_science)#Polytypism), another term for generic programming.

See the [Documentation](docs/getting-started.md) section to get started.

## Why PolyType

In .NET, serialization libraries are predominantly **code-first**: users supply their own types, and the library derives a corresponding data contract, determining which members represent data and how to read or populate them.

Achieving this reliably is extremely complex. Maintainers must contend with:

* **Member discovery:** inheritance, overrides, hiding, interface diamonds.
* **Access and mutation:** accessibility, readonly fields, init-only properties.
* **Construction:** constructor selection, parameter binding, required members, optional parameter defaults.
* **Nullability:** nullable reference annotations, attribute-based nullability (e.g. `AllowNullAttribute`), nullable value types.
* **Generics and constraints:** nested generic types, type substitution, generic subtype specialization, constraint validation.
* **Collections:** multidimensional arrays, dictionaries, sets, immutable collections, custom collection builders.
* **Representations:** tuples, enums, unions.

These features interact: supporting each individually does not establish that their combinations work. Every type must either receive a correct contract or be deliberately rejected, and language evolution continually expands that obligation. Each library repeating this work creates maintenance overhead and inconsistent implementations whose differences become unpredictable paper cuts for users.

PolyType consolidates data-contract discovery, data access, and object construction behind a shared layer, letting serializers focus on encoding and decoding their particular formats. Like LSP's separation of language tooling from editors, this boundary replaces duplicated integration work with reusable infrastructure. Its reflection provider and source generator let the same serializer implementation support runtime-discovered types and Native AOT. Coding agents make that boundary no less valuable: generating a plausible serializer is easier than anticipating the language's full feature matrix. PolyType supplies guardrails that let humans and agents alike work within a smaller, better-defined problem.
