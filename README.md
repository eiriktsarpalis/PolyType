# PolyType [![Build & Tests](https://github.com/eiriktsarpalis/PolyType/actions/workflows/build.yml/badge.svg)](https://github.com/eiriktsarpalis/PolyType/actions/workflows/build.yml) [![NuGet Badge](https://img.shields.io/nuget/dt/PolyType)](https://www.nuget.org/packages/PolyType/) [![codecov](https://codecov.io/gh/eiriktsarpalis/PolyType/graph/badge.svg?token=1K2FV94SEL)](https://codecov.io/gh/eiriktsarpalis/PolyType)

PolyType is a practical generic programming library for .NET. It facilitates the rapid development of feature-complete, high-performance libraries that interact with user-defined types. This includes serializers, structured loggers, mappers, validators, parsers, random generators, and equality comparers. Its built-in source generator ensures that any library built on top of PolyType gets [Native AOT support for free](https://eiriktsarpalis.wordpress.com/2024/10/22/source-generators-for-free/).

The project is a port of the [TypeShape](https://github.com/eiriktsarpalis/TypeShape) library for F#, adapted to patterns and idioms available in C#. The name PolyType is a reference to [polytypic programming](https://en.wikipedia.org/wiki/Polymorphism_(computer_science)#Polytypism), another term for generic programming.

See the [project website](https://eiriktsarpalis.github.io/PolyType) for additional background and [API documentation](https://eiriktsarpalis.github.io/PolyType/api/PolyType.html).

## Quick Start

You can try the library by installing the `PolyType` NuGet package:

```bash
$ dotnet add package PolyType
```

which includes the core types and source generator for generating type shapes:

```C#
using PolyType;

[GenerateShape]
public partial record Person(string name, int age);
```

Doing this will augment `Person` with an implementation of the `IShapeable<Person>` interface. This suffices to make `Person` usable with any library that targets the PolyType core abstractions. You can try this out by installing the built-in example libraries:

```bash
$ dotnet add package PolyType.Examples
```

Here's how the same value can be serialized to three separate formats.

```csharp
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.CborSerializer;
using PolyType.Examples.XmlSerializer;

Person person = new("Pete", 70);
JsonSerializerTS.Serialize(person); // {"Name":"Pete","Age":70}
XmlSerializer.Serialize(person);    // <value><Name>Pete</Name><Age>70</Age></value>
CborSerializer.EncodeToHex(person); // A2644E616D656450657465634167651846
```

Since the application uses a source generator to produce the shape for `Person`, it is fully compatible with Native AOT. See the [shape providers](https://eiriktsarpalis.github.io/PolyType/shape-providers.html) article for more details on how to use the library with your types.

## Authoring PolyType Libraries

As a library author, PolyType makes it easy to write high-performance, feature-complete components by targeting its [core abstractions](https://eiriktsarpalis.github.io/PolyType/core-abstractions.html). For example, a parser API using PolyType might look as follows:

```C#
public static class MyFancyParser
{
    public static T? Parse<T>(string myFancyFormat) where T : IShapeable<T>;
}
```

The [`IShapeable<T>` constraint](https://eiriktsarpalis.github.io/PolyType/api/PolyType.IShapeable-1.html) indicates that the parser only works with types augmented with PolyType metadata. This metadata can be provided using the PolyType source generator:

```C#
Person? person = MyFancyParser.Parse<Person>(format); // Compiles

[GenerateShape] // Generate an IShapeable<TPerson> implementation
partial record Person(string name, int age, List<Person> children);
```

For more information see:

* The [core abstractions](https://eiriktsarpalis.github.io/PolyType/core-abstractions.html) document for an overview of the core programming model.
* The [shape providers](https://eiriktsarpalis.github.io/PolyType/shape-providers.html) document for an overview of the built-in shape providers and their APIs.
* The generated [API documentation](https://eiriktsarpalis.github.io/PolyType/api/PolyType.html) for the project.
* The [`PolyType.Examples`](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples) project for advanced examples of libraries built on top of PolyType.

## Case Study: Writing a JSON serializer

The repo includes a [JSON serializer](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples/JsonSerializer) built on top of the `Utf8JsonWriter`/`Utf8JsonReader` primitives provided by System.Text.Json. At the time of writing, the full implementation is just under 1200 lines of code but exceeds STJ's built-in `JsonSerializer` both in terms of [supported types](https://github.com/eiriktsarpalis/PolyType/blob/main/tests/PolyType.Tests/JsonTests.cs) and performance.

### Performance

Here's a [benchmark](https://github.com/eiriktsarpalis/PolyType/blob/main/tests/PolyType.Benchmarks/JsonBenchmark.cs) comparing `System.Text.Json` with the included PolyType implementation:

#### Serialization

| Method                          | Mean      | Ratio | Allocated | Alloc Ratio |
|-------------------------------- |----------:|------:|----------:|------------:|
| Serialize_StjReflection         | 150.43 ns |  1.00 |     312 B |        1.00 |
| Serialize_StjSourceGen          | 151.31 ns |  1.01 |     312 B |        1.00 |
| Serialize_StjSourceGen_FastPath |  96.79 ns |  0.64 |         - |        0.00 |
| Serialize_PolyTypeReflection    | 113.19 ns |  0.75 |         - |        0.00 |
| Serialize_PolyTypeSourceGen     | 112.92 ns |  0.75 |         - |        0.00 |

#### Deserialization

| Method                         | Mean     | Ratio | Allocated | Alloc Ratio |
|------------------------------- |---------:|------:|----------:|------------:|
| Deserialize_StjReflection      | 534.0 ns |  1.00 |    1016 B |        1.00 |
| Deserialize_StjSourceGen       | 534.6 ns |  1.00 |     992 B |        0.98 |
| Deserialize_PolyTypeReflection | 273.1 ns |  0.51 |     440 B |        0.43 |
| Deserialize_PolyTypeSourceGen  | 266.3 ns |  0.50 |     440 B |        0.43 |

Even though both serializers target the same underlying reader and writer types, the PolyType implementation is ~75% faster for serialization and ~100% faster for deserialization, when compared with System.Text.Json's metadata serializer. As expected, fast-path serialization is still fastest since its implementation is fully inlined.

## Known libraries based on PolyType

The following code bases are based upon PolyType and may be worth checking out.

* [Nerdbank.MessagePack](https://github.com/AArnott/Nerdbank.MessagePack) - a MessagePack library with performance to rival MessagePack-CSharp, and greater simplicity and additional features.

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
