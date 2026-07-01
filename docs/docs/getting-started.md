# Getting Started

You can try the library by installing the `PolyType` NuGet package:

```bash
$ dotnet add package PolyType
```

Which includes the core types and source generator for generating type shapes:

```csharp
using PolyType;

[GenerateShape]
public partial record Person(string Name, int Age);
```

Doing this will augment `Person` with an implementation of the `IShapeable<Person>` interface. This suffices to make `Person` usable with any library that targets the PolyType core abstractions. You can try this out by installing the built-in example libraries:

```bash
$ dotnet add package PolyType.Examples
```

Here's how the same value can be serialized to four separate formats:

```csharp
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.CborSerializer;
using PolyType.Examples.XmlSerializer;
using PolyType.Examples.YamlSerializer;

Person person = new("Pete", 70);
JsonSerializerTS.Serialize(person); // {"Name":"Pete","Age":70}
XmlSerializer.Serialize(person);    // <value><Name>Pete</Name><Age>70</Age></value>
CborSerializer.EncodeToHex(person); // A2644E616D656450657465634167651846
YamlSerializer.Serialize(person);   // Name: Pete\nAge: 70
```

Since the application uses a source generator to produce the shape for `Person`, it is fully compatible with Native AOT. See the [shape providers](shape-providers.md) article for more details on how to use the library with your types.

## Authoring PolyType Libraries

As a library author, PolyType makes it easy to write high-performance, feature-complete components by targeting its [core abstractions](core-abstractions.md). For example, a parser API using PolyType might look as follows:

[!code-csharp[](../CSharpSamples/Middleware.cs#MyFancyParser)]

The <xref:PolyType.IShapeable`1> constraint indicates that the parser only works with types augmented with PolyType metadata. This metadata can be provided using the PolyType source generator:

[!code-csharp[](../CSharpSamples/Middleware.cs#MyFancyParserUser)]
[!code-csharp[](../CSharpSamples/Middleware.cs#Person)]

### Multi-targeting libraries

If your library targets both .NET and .NET Standard/Framework, the following syntax is encouraged as it produces a single API surface for all target frameworks, encourages compile-time optimizations and moves some runtime exceptions to compile-time errors for your consumers:

[!code-csharp[](../CSharpSamples/Middleware.cs#IdealMultitargetingAPI)]

> [!NOTE]
> The above syntax is callable by all .NET projects, as well as .NET Framework and .NET Standard projects that utilize C# 14+.
> C# 14 can be forced for .NET Framework and .NET Standard projects by adding the following to the project file:
> ```xml
> <PropertyGroup>
>   <LangVersion>14</LangVersion>
> </PropertyGroup>
> ```

For more information, see:

* The [core abstractions](core-abstractions.md) document for an overview of the core programming model.
* The [shape providers](shape-providers.md) document for an overview of the built-in shape providers and their APIs.
* The [type shape derivation specification](specification.md) document detailing how .NET types are mapped to different type shapes.
* The generated <xref:PolyType> API documentation for the project.
* The [`PolyType.Examples`](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples) project for advanced examples of libraries built on top of PolyType.
