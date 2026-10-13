# PolyType [![Build & Tests](https://github.com/eiriktsarpalis/PolyType/actions/workflows/build.yml/badge.svg)](https://github.com/eiriktsarpalis/PolyType/actions/workflows/build.yml) [![NuGet Version](https://img.shields.io/nuget/vpre/PolyType)](https://www.nuget.org/packages/PolyType/) [![codecov](https://codecov.io/gh/eiriktsarpalis/PolyType/graph/badge.svg?token=1K2FV94SEL)](https://codecov.io/gh/eiriktsarpalis/PolyType)

PolyType is a practical generic programming library for .NET. It facilitates the rapid development of feature-complete, high-performance libraries that interact with user-defined types. This includes serializers, RPC frameworks, structured loggers, mappers, validators, parsers, random generators, and equality comparers. Its built-in source generator ensures that any library built on top of PolyType gets [Native AOT support for free](https://eiriktsarpalis.wordpress.com/2024/10/22/source-generators-for-free/).

The project is a port of the [TypeShape](https://github.com/eiriktsarpalis/TypeShape) library for F#, adapted to patterns and idioms available in C#. The name PolyType is a reference to [polytypic programming](https://en.wikipedia.org/wiki/Polymorphism_(computer_science)#Polytypism), another term for generic programming.

See the [project website](https://eiriktsarpalis.github.io/PolyType/docs/getting-started.html) for additional background and [API documentation](https://eiriktsarpalis.github.io/PolyType/api/PolyType.html).

## Applications

The following projects have been based on PolyType:

* [Nerdbank.MessagePack](https://github.com/AArnott/Nerdbank.MessagePack) - a MessagePack library with performance to rival MessagePack-CSharp, and greater simplicity and additional features.
* _“At [Alvys](https://alvys.com), we built an EDI mapping engine to automate and accelerate EDI integrations. The engine had to be fast, configurable and portable across EDI versions. A crucial step is mapping the EDI segments model to an object model. For this, we used PolyType, which allowed us to easily build both the encoder and the decoder. We appreciated the emphasis on fundamentals and performance, as well as the plethora of examples. The result: a solution with only a fraction of the code and an order-of-magnitude increase in micro-benchmark performance compared to the previous solution that used a commercial EDI library.”_ — [Leo Gorodinski](https://x.com/eulerfx/status/1968690959438504260)

## Project structure

The repo consists of the following projects:

* The core `PolyType` library containing:
  * The [core abstractions](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType/Abstractions) defining the type model.
  * The [reflection provider](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType/ReflectionProvider) implementation.
  * The [model classes](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType/SourceGenModel) used by the source generator.
* The [`PolyType.SourceGenerator`](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.SourceGenerator) project contains the built-in source generator implementation.
* The [`PolyType.Roslyn`](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Roslyn) library exposes a set of components for extracting data models from Roslyn type symbols. Used as the foundation for the built-in source generator.
* [`PolyType.Examples`](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples) containing library examples:
  * A serializer built on top of System.Text.Json,
  * A serializer built on top of System.Xml,
  * A serializer built on top of System.Formats.Cbor,
  * A YAML serializer built on top of YamlDotNet,
  * A `ConfigurationBinder` like implementation,
  * A dependency injection implementation,
  * A simple pretty-printer for .NET values,
  * A generic random value generator based on `System.Random`,
  * A JSON schema generator for .NET types,
  * An object cloning function,
  * A structural `IEqualityComparer<T>` generator for POCOs and collections,
  * An object validator in the style of System.ComponentModel.DataAnnotations.
  * A simple .NET object mapper.
* The [`applications`](https://github.com/eiriktsarpalis/PolyType/tree/main/applications) folder contains sample Native AOT console applications.

## CI Packages

CI builds of NuGet packages are available on [feedz.io](https://feedz.io/). To use the feed, add the following package source to your `NuGet.config`:

```xml
<configuration>
  <packageSources>
    <add key="feedz.io" value="https://f.feedz.io/eiriktsarpalis/PolyType/nuget/index.json" />
  </packageSources>
```

## Publishing releases

Publishing a GitHub release triggers [the NuGet publishing workflow](.github/workflows/release.yml). It uses [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) to exchange a GitHub Actions OIDC token for a short-lived API key immediately before pushing packages.

Create a GitHub Actions environment named `nuget` and set its `NUGET_USER` variable to the NuGet.org username that creates the trust policy, not an email address or organization name. On NuGet.org, open **Trusted Publishing** and create a policy owned by the account that owns the packages, with these GitHub settings:

| Field | Value |
|-------|-------|
| Repository owner | `eiriktsarpalis` |
| Repository | `PolyType` |
| Workflow file | `release.yml` |
| Environment | `nuget` |

Scope the policy to publishing new versions of `PolyType`, `PolyType.Roslyn`, `PolyType.Examples`, and `PolyType.TestCases`. After the first successful trusted-publishing release, revoke the old publishing API key on NuGet.org and delete the repository's `NUGET_API_KEY` secret.

NuGet.org credentials are not used for CI publishing to GitHub Packages or Feedz.io; those feeds keep their existing authentication.
