# Shape providers

This document provides a walkthrough of the built-in type shape providers. These are typically consumed by end users looking to use their types with libraries built on top of the PolyType core abstractions.

## Source Generator

We can use the built-in source generator to auto-generate shape metadata for a user-defined type like so:

```C#
using PolyType;

[GenerateShape]
partial record Person(string name, int age, List<Person> children);
```

This augments `Person` with an explicit implementation of `IShapeable<Person>`, which can be used as an entry point by libraries targeting PolyType:

```C#
MyRandomGenerator.Generate<Person>(); // Compiles

public static class MyRandomGenerator
{
    public static T Generate<T>(int seed = 0) where T : IShapeable<T>;
}
```

The source generator also supports shape generation for third-party types using witness types:

```C#
[GenerateShapeFor<Person[]>]
[GenerateShapeFor<List<int>>]
public partial class Witness; // : IShapeable<Person[]>, IShapeable<List<int>>
```

which can be applied against supported libraries like so:

```C#
MyRandomGenerator.Generate<Person[], Witness>() // Compiles
MyRandomGenerator.Generate<List<int>, Witness>() // Compiles

public static class MyRandomGenerator
{
    public static T Generate<T, TWitness>(int seed = 0) where TWitness : IShapeable<T>;
}
```

## Reflection Provider

PolyType includes a reflection-based provider that resolves shape metadata at run time:

```C#
using PolyType.ReflectionProvider;

ITypeShapeProvider provider = ReflectionTypeShapeProvider.Default;
var shape = (ITypeShape<Person>)provider.GetTypeShape(typeof(Person));
```

Which can be consumed by supported libraries as follows:

```C#
MyRandomGenerator.Generate<Person>(ReflectionTypeShapeProvider.Default);
MyRandomGenerator.Generate<Person[][]>(ReflectionTypeShapeProvider.Default);
MyRandomGenerator.Generate<List<int>>(ReflectionTypeShapeProvider.Default);

public static class MyRandomGenerator
{
    public static T Generate<T>(ITypeShapeProvider provider);
}
```

By default, the reflection provider uses dynamic methods (Reflection.Emit) to speed up reflection, however, this might not be desirable when running on certain platforms (e.g., Blazor WebAssembly). It can be turned off using the relevant constructor parameter:

```C#
ITypeShapeProvider provider = new ReflectionTypeShapeProvider(useReflectionEmit: false);
```

## Shape attributes

PolyType exposes a number of attributes that tweak aspects of the generated shape. These attributes are recognized both by the source generator and the reflection provider.

### TypeShapeAttribute

The `TypeShape` attribute can be applied on type declarations to customize their generated shapes. It is independent of the `GenerateShape` attribute since it doesn't trigger the source generator and is recognized by the reflection-based provider.

The `Kind` property can be used to override the default shape kind for a particular type:

```C#
[TypeShape(Kind = TypeShapeKind.Object)]
class MyList : List<int>
{
    public int Value { get; set; }
}
```

The above will instruct the providers to generate an object shape as opposed to an enumerable shape that is the default. It can also be used to completely disable any nested member resolution for a given type:

```C#
[TypeShape(Kind = TypeShapeKind.None)]
record MyPoco(int Value);
```

#### Surrogate types

The `TypeShape` attribute can also be used to specify marshalers to surrogate types:

```C#
[TypeShape(Marshaler = typeof(EnvelopeMarshaler))]
record Envelope(string Value);

class EnvelopeMarshaler : IMarshaler<Envelope, string>
{
    public string? Marshal(Envelope? envelope) => envelope?.Value;
    public Envelope? Unmarshal(string? surrogateString) => surrogateString is null ? null : new(surrogateString);
}
```

The above configures `Envelope` to admit a string-based shape using the specified surrogate representation. In other words, it assumes a string schema instead of that of an object. Surrogates are used as a more versatile alternative compared to format-specific converters.

In the following example, we marshal the internal state of an object to a surrogate struct:

```C#
[TypeShape(Marshaler = typeof(Marshaler))]
public class PocoWithInternalState(int value1, string value2)
{
    private readonly int _value1 = value1;
    private readonly string _value2 = value2;

    public record struct Surrogate(int Value1, string Value2);

    public sealed class Marshaler : IMarshaler<PocoWithInternalState, Surrogate>
    {
        public Surrogate Marshal(PocoWithInternalState? poco) => poco is null ? default : new(poco._value1, poco._value2);
        public PocoWithInternalState Unmarshal(Surrogate surrogate) => new(surrogate._value1, surrogate._value2 ?? "");
    }
}
```

It's possible to define marshalers for generic types, provided that the type parameters of the Marshaler match the type parameters of declaring type:

```C#
[TypeShape(Marshaler = typeof(Marshaler<>))]
public record MyPoco<T>(T Value);

public class Marshaler<T> : IMarshaler<MyPoco<T>, T>
{
    public T? Marshal(MyPoco<T>? value) => value is null ? default : value.Value;
    public MyPoco<T>? Unmarshal(T? value) => value is null ? null : new(value);
}

[GenerateShapeFor<MyPoco<string>>]
public partial class Witness;
```

The above will configure `MyPoco<string>` with a Marshaler of type `Marshaler<string>`. Nested generic Marshalers are also supported:

```C#
[TypeShape(Marshaler = typeof(MyPoco<>.Marshaler))]
public record MyPoco<T>(T Value)
{
    public class Marshaler : IMarshaler<MyPoco<T>, T>
    {
        /* Implementation goes here */
    }
}
```

#### Associated types

The `TypeShape` attribute can also be used to specify associated types for the target type.
In the context of source generated/AOT applications, this provides a Reflection-free way to leap from one @PolyType.ITypeShape to an associated shape, and get all the functionality of the associated shape.
For example, serializers may need to jump from a type shape to its converter.

[!code-csharp[](CSharpSamples/AssociatedTypes.cs#TypeShapeOneType)]

Use the @PolyType.ITypeShape.GetAssociatedTypeShape*?displayProperty=nameWithType method to obtain the shape of an associated type.
The @PolyType.SourceGenModel.SourceGenTypeShapeProvider implementation of this method requires that the associated types be pre-determined at compile time via attributes.
The @PolyType.ReflectionProvider.ReflectionTypeShapeProvider is guaranteed to work with all types and therefore does _not_ require these attributes.
Thus, it can be valuable to test your associated types code with the source generation provider to ensure your code is AOT-compatible.

An associated type must have at least `internal` visibility for its shape to be generated for use within its same assembly.
Making the type `public` is highly recommended so that when the data type is used in other assemblies, the associated type's shape is available from their context as well.

Registering associated types is particularly important when the associated type is generic, and the generic type arguments come from the target type.

[!code-csharp[](CSharpSamples/AssociatedTypes.cs#GenericAssociatedType)]

Associated shapes can be only partially defined.
If for example you only need to be able to activate an instance of an associated type but don't need its properties (and their respective types) defined, you can indicate this and shrink the output of source generation.
This can be appropriate for a serializer that needs to jump from a generic data type to a generic converter, for example.

[!code-csharp[](CSharpSamples/AssociatedTypes.cs#SerializerConverter)]

Only @PolyType.SourceGenModel.SourceGenTypeShapeProvider produces partial shapes.
The @PolyType.ReflectionProvider.ReflectionTypeShapeProvider always produces complete shapes.

At present, only @PolyType.Abstractions.IObjectTypeShape shapes are generated partially.
Shapes for collections, enums, unions, etc. are generated as full shapes.

Type associations can be defined directly on the originating type via @PolyType.AssociatedTypeShapeAttribute.AssociatedTypes?displayProperty=nameWithType when that type and its associated type are in the same assembly.
When the associated type is in another assembly, use @PolyType.TypeShapeExtensionAttribute.AssociatedTypes?displayProperty=nameWithType in the assembly that declares the associated type.
You can also define a custom attribute that can define associated types by attributing your own custom attribute with @PolyType.Abstractions.AssociatedTypeAttributeAttribute.

### TypeShapeExtensionAttribute

The @PolyType.TypeShapeExtensionAttribute is an assembly-level attribute.
It is very similar to @PolyType.TypeShapeAttribute, but it is used to customize the generated shape for a type that your assembly does not declare.

### Polymorphic types

The `DerivedTypeShape` attribute can be used to declare polymorphic type hierarchies for classes and interfaces:

```C#
[DerivedTypeShape(typeof(Horse))]
[DerivedTypeShape(typeof(Dog))]
[DerivedTypeShape(typeof(Cat))]
interface IAnimal;

class Dog : IAnimal;
class Cat : IAnimal;
class Horse : IAnimal;
```

The above incorporates the shapes for `Cat`, `Dog` and `Horse` as polymorphic cases in the shape of `IAnimal`.
Serializing an instance of type `Dog` as `IAnimal` using the example JSON serializer will produce the following payload:

```JSON
{ "$type": "Dog" }
```

Each derived type declaration is given a unique string identifier (the name) and a unique integer identifier (the tag).
The former is used as a type discriminator in the case of text-based formats like XML or JSON and the latter is used
as a discriminator in compact binary formats like CBOR or MessagePack. Either name or tag can be specified explicitly
for each derived type declaration:

```C#
[DerivedTypeShape(typeof(Leaf), Name = "leaf", Tag = 5)]
[DerivedTypeShape(typeof(Node), Name = "node", Tag = 6)]
abstract record BinTree;
abstract record Leaf : BinTree;
abstract record Node(int label, int left, int right) : BinTree;
```

If left unset, the name of a derived type defaults to its type name (i.e. `nameof(TDerived)`) and the tag corresponds to the attribute declaration order.
It should be noted that mono reflection does not preserve attribute declaration ordering, so it is recommended that applications targeting mono should either use the source generator or explicitly set the tags for all model types.

For the case of unregistered derived types, PolyType applies a "nearest known ancestor" resolution algorithm. Given the type hierarchy

```C#
[DerivedTypeShape(typeof(Horse))]
[DerivedTypeShape(typeof(Cow))]
class Animal;
class Horse : Animal;
class Cow : Animal;

class Pony : Horse;
class Chicken : Animal;
```

instances of type `Pony` will resolve as `Horse` and instances of type `Chicken` will resolve as the `Animal` base. Note that this can result in undefined behavior in the case of diamonds in interface hierarchies:

```C#
[DerivedTypeShape(typeof(IDerived1))]
[DerivedTypeShape(typeof(IDerived2))]
interface IBase;
interface IDerived1 : IBase;
interface IDerived2 : IBase;

class Impl : IDerived1, IDerived2;
```

Instances of type `Impl` could resolve as either `IDerived1` or `IDerived2`, depending on the particular runtime and shape provider implementation. This ambiguity can be resolved by explicitly adding a `DerivedTypeShape` declaration for `Impl` or any intermediate interface type implementing both `IDerived1` and `IDerived2`.

### PropertyShapeAttribute

Configures aspects of a generated property shape, for example:

```C#
class UserData
{
    [PropertyShape(Name = "id", Order = 0)]
    public required string Id { get; init; }

    [PropertyShape(Name = "name", Order = 1)]
    public string? Name { get; init; }

    [PropertyShape(Ignore = true)]
    public string? UserSecret { get; init; }
}
```

Compare with `System.Runtime.Serialization.DataMemberAttribute` and `Newtonsoft.Json.JsonPropertyAttribute`.

### ConstructorShapeAttribute

Can be used to pick a specific constructor for a given type, if there is ambiguity:

```C#
class PocoWithConstructors
{
    public PocoWithConstructors();
    [ConstructorShape] // <--- Only use this constructor in PolyType apps
    public PocoWithConstructors(int x1, int x2);
}
```

Compare with `System.Text.Json.Serialization.JsonConstructorAttribute`.

### ParameterShapeAttribute

Configures aspects of a constructor parameter shape:

```C#
class PocoWithConstructors
{
    public PocoWithConstructors([ParameterShape(Name = "name")] string x1, [ParameterShape(Name = "age")] int x2);
}
```
