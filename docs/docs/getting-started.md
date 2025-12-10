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

Here's how the same value can be serialized to three separate formats:

```csharp
using PolyType.Examples.JsonSerializer;
using PolyType.Examples.CborSerializer;
using PolyType.Examples.XmlSerializer;

Person person = new("Pete", 70);
JsonSerializerTS.Serialize(person); // {"Name":"Pete","Age":70}
XmlSerializer.Serialize(person);    // <value><Name>Pete</Name><Age>70</Age></value>
CborSerializer.EncodeToHex(person); // A2644E616D656450657465634167651846
```

Since the application uses a source generator to produce the shape for `Person`, it is fully compatible with Native AOT. See the [shape providers](shape-providers.md) article for more details on how to use the library with your types.

## Authoring PolyType Libraries

As a library author, PolyType makes it easy to write high-performance, feature-complete components by targeting its [core abstractions](core-abstractions.md). For example, a parser API using PolyType might look as follows:

```csharp
public static class MyFancyParser
{
    public static T? Parse<T>(string myFancyFormat) where T : IShapeable<T>;
}
```

The <xref:PolyType.IShapeable`1> constraint indicates that the parser only works with types augmented with PolyType metadata. This metadata can be provided using the PolyType source generator:

```csharp
string myFancyFormat = "..."; // Some format string
Person? person = MyFancyParser.Parse<Person>(myFancyFormat); // Compiles

[GenerateShape] // Generate an IShapeable<Person> implementation
partial record Person(string name, int age, List<Person> children);
```

For more information, see:

* The [core abstractions](core-abstractions.md) document for an overview of the core programming model.
* The [shape providers](shape-providers.md) document for an overview of the built-in shape providers and their APIs.
* The [type shape derivation specification](specification.md) document detailing how .NET types are mapped to different type shapes.
* The generated <xref:PolyType> API documentation for the project.
* The [`PolyType.Examples`](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples) project for advanced examples of libraries built on top of PolyType.
