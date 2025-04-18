# Shape providers

This document provides a walkthrough of the built-in type shape providers. These are typically consumed by end users looking to use their types with libraries built on top of the PolyType core abstractions.

## Source Generator

We can use the built-in source generator to auto-generate shape metadata for a user-defined type like so:

```C#
using PolyType;

[GenerateShape]
partial record Person(string name, int age, List<Person> children);
```

This augments `Person` with an explicit implementation of `IShapeable<Person>`, which can be used an entry point by libraries targeting PolyType:

```C#
MyRandomGenerator.Generate<Person>(); // Compiles

public static class MyRandomGenerator
{
    public static T Generate<T>(int seed = 0) where T : IShapeable<T>;
}
```

The source generator also supports shape generation for third-party types using witness types:

```C#
[GenerateShape<Person[]>]
[GenerateShape<List<int>>]
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
var shape = (ITypeShape<Person>)provider.GetShape(typeof(Person));
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

By default, the reflection provider uses dynamic methods (Reflection.Emit) to speed up reflection, however this might not be desirable when running in certain platforms (e.g. blazor-wasm). It can be turned off using the relevant constructor parameter:

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

The `TypeShape` attribute can also be used to specify marshallers to surrogate types:

```C#
[TypeShape(Marshaller = typeof(EnvelopeMarshaller))]
record Envelope(string Value);

class EnvelopeMarshaller : IMarshaller<Envelope, string>
{
    public string? ToSurrogate(Envelope? envelope) => envelope?.Value;
    public Envelope? FromSurrogate(string? surrogateString) => surrogateString is null ? null : new(surrogateString);
}
```

The above configures `Envelope` to admit a string-based shape using the specified surrogate representation. In other words, it assumes a string schema instead of that of an object. Surrogates are used as a more versatile alternative compared to format-specific converters.

In the following example, we marshal the internal state of an object to a surrogate struct:

```C#
[TypeShape(Marshaller = typeof(Marshaller))]
public class PocoWithInternalState(int value1, string value2)
{
    private readonly int _value1 = value1;
    private readonly string _value2 = value2;

    public record struct Surrogate(int Value1, string Value2);

    public sealed class Marshaller : IMarshaller<PocoWithInternalState, Surrogate>
    {
        public Surrogate ToSurrogate(PocoWithInternalState? poco) => poco is null ? default : new(poco._value1, poco._value2);
        public PocoWithInternalState FromSurrogate(Surrogate surrogate) => new(surrogate._value1, surrogate._value2 ?? "");
    }
}
```

It's possible to define marshallers for generic types, provided that the type parameters of the marshaller match the type parameters of declaring type:

```C#
[TypeShape(Marshaller = typeof(Marshaller<>))]
public record MyPoco<T>(T Value);

public class Marshaller<T> : IMarshaller<MyPoco<T>, T>
{
    public T? ToSurrogate(MyPoco<T>? value) => value is null ? default : value.Value;
    public MyPoco<T>? FromSurrogate(T? value) => value is null ? null : new(value);
}

[GenerateShape<MyPoco<string>>]
public partial class Witness;
```

The above will configure `MyPoco<string>` with a marshaller of type `Marshaller<string>`. Nested generic marshallers are also supported:

```C#
[TypeShape(Marshaller = typeof(MyPoco<>.Marshaller))]
public record MyPoco<T>(T Value)
{
    public class Marshaller : IMarshaller<MyPoco<T>, T>
    {
        /* Implementation goes here */
    }
}
```

#### Associated types

The `TypeShape` attribute can also be used to specify associated types for the target type.
This provides a Reflection-free way to activate an associated type via its default constructor
given its @PolyType.Abstractions.ITypeShape.
For example, serializers may need to jump from a type shape to its converter.

[!code-csharp[](CSharpSamples/AssociatedTypes.cs#TypeShapeOneType)]

@PolyType.Abstractions.ITypeShape.GetAssociatedType*?displayProperty=nameWithType is the method to use to obtain a factory for instances of the associated type.
The @PolyType.SourceGenModel.SourceGenTypeShapeProvider implementation of this method requires that the associated types be pre-determined at compile time via attributes.
The @PolyType.ReflectionProvider.ReflectionTypeShapeProvider does _not_ require these attributes.
Thus, it can be valuable to test your associated types code with the source generation provider to ensure your code is AOT-compatible.

An associated type must have a public default constructor.
An associated type must have at least `internal` visibility to activate the associated type from within its same assembly,
but making the type `public` is highly recommended so that when the data type is used in other assemblies, the associated type can be activated from their context as well.

Registering associated types is particularly important when the associated type is generic, and the generic type arguments come from the target type.

[!code-csharp[](CSharpSamples/AssociatedTypes.cs#GenericAssociatedType)]

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
[DerivedTypeShape(typeof(Leaf), Name = "node", Tag = 6)]
abstract record BinTree;
abstract record Leaf : BinTree;
abstract record Node(int label, int left, int right) : BinTree
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
