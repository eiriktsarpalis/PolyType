# Source generator

PolyType ships a Roslyn [incremental source generator](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md) that emits shape metadata at compile time. This article describes the internal strategy of the generator and how shapes are discovered at run time. For information on how to annotate your types and consume generated shapes, see [Shape providers](shape-providers.md).

## Pipeline overview

The source generator is implemented by the `PolyType.SourceGenerator` project and follows a three-stage pipeline:

1. **Parsing** — The `Parser` class traverses the Roslyn compilation, collecting every type annotated with <xref:PolyType.GenerateShapeAttribute> or <xref:PolyType.GenerateShapeForAttribute`1>. It uses the `PolyType.Roslyn` library to extract `TypeDataModel` objects from `ITypeSymbol`s, then maps them into incremental-generator-safe model types (records with equatable collections).

2. **Model mapping** — The parser maps each `TypeDataModel` into a `TypeShapeModel` stored inside a `TypeShapeProviderModel`. These models capture every detail needed for code emission — property shapes, constructor shapes, collection semantics, enum values, and more — without retaining any Roslyn symbol references.

3. **Source formatting** — The `SourceFormatter` class walks the `TypeShapeProviderModel` and emits C# source files: one main file for the `SourceGenTypeShapeProvider` implementation and one file per provided type shape.

## Generated output

The generator produces a single <xref:PolyType.SourceGenModel.SourceGenTypeShapeProvider> subclass per project. This class:

- Exposes a `Default` singleton property.
- Overrides `GetTypeShape(Type)` with a `switch` over the string representation of each generated type, delegating to per-type shape accessors.
- Contains per-type shape fields and factory methods for constructors, properties, and collection shapes.

For each type declaration annotated with `[GenerateShapeFor]`, the generator also emits a `GeneratedTypeShapeProvider` static property on the witness type, giving library consumers direct access to the provider singleton.

## Generated type shapes

The provider emits one source file per type in the transitive type graph. Each file contains a lazily initialized property returning an `ITypeShape<T>`, backed by a factory method whose structure depends on the shape kind. The following sections illustrate the key shape kinds.

### Object shapes

For classes, records, and structs, the generator emits a <xref:PolyType.SourceGenModel.SourceGenObjectTypeShape`1> with factory delegates for properties and constructors. Given:

```csharp
[GenerateShape]
partial record Person(string Name, int Age);
```

the generated factory method looks approximately like:

```csharp
private ITypeShape<Person> __Create_Person()
{
    return new SourceGenObjectTypeShape<Person>
    {
        PropertiesFactory = __CreateProperties_Person,
        ConstructorFactory = __CreateConstructor_Person,
        IsRecordType = true,
        Provider = this,
    };
}
```

The `PropertiesFactory` returns an array of <xref:PolyType.SourceGenModel.SourceGenPropertyShape`2> instances, each carrying strongly typed getter and setter delegates:

```csharp
new SourceGenPropertyShape<Person, string>
{
    Name = "Name",
    Getter = static (ref Person obj) => obj.Name,
    Setter = static (ref Person obj, string value) => obj.Name = value,
    PropertyType = String, // Reference to the ITypeShape<string> property
    // ...
},
```

For inaccessible members (private fields/properties), the generator emits [`[UnsafeAccessor]`](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.unsafeaccessorattribute) declarations on frameworks that support them, or reflection-based fallbacks otherwise.

### Enumerable shapes

Collection types such as `List<T>`, arrays, and `IEnumerable<T>` implementations are emitted as <xref:PolyType.SourceGenModel.SourceGenEnumerableTypeShape`2>. For example, `List<int>` generates:

```csharp
private ITypeShape<List<int>> __Create_List_Int32()
{
    return new SourceGenEnumerableTypeShape<List<int>, int>
    {
        ElementTypeFactory = () => Int32,
        ConstructionStrategy = CollectionConstructionStrategy.Mutable,
        DefaultConstructor = static (in CollectionConstructionOptions<int> options)
            => new List<int>(),
        GetEnumerable = static obj => obj,
        Appender = static (ref List<int> obj, int value)
            => { obj.Add(value); return true; },
        Provider = this,
    };
}
```

The generated shape captures the construction strategy (mutable, parameterized, or none), element type, an enumeration delegate, and an append delegate where applicable.

### Dictionary shapes

Dictionary types like `Dictionary<TKey, TValue>` are emitted as <xref:PolyType.SourceGenModel.SourceGenDictionaryTypeShape`3>, following a similar pattern to enumerables but with separate key and value type references and key-value insertion delegates.

### Enum shapes

Enum types are emitted as <xref:PolyType.SourceGenModel.SourceGenEnumTypeShape`2>, which captures the underlying type and a pre-built member dictionary:

```csharp
private ITypeShape<DayOfWeek> __Create_DayOfWeek()
{
    return new SourceGenEnumTypeShape<DayOfWeek, int>
    {
        UnderlyingTypeFactory = () => Int32,
        Members = new Dictionary<string, int>
        {
            ["Sunday"] = 0,
            ["Monday"] = 1,
            // ...
        },
        Provider = this,
    };
}
```

## Shape discovery

At run time, shapes are discovered differently depending on the target framework and on whether the type was annotated directly or through a witness type. The generator adapts its output to each target framework accordingly.

### Static resolution on .NET 8 or later

On .NET 8 or later, the target supports [static abstract interface members](https://learn.microsoft.com/dotnet/csharp/whats-new/tutorials/static-virtual-interface-members), so the generator augments each annotated type with an `IShapeable<T>` implementation. Given:

```csharp
[GenerateShape]
partial record Person(string Name, int Age);
```

the generator emits:

```csharp
partial record Person : IShapeable<Person>
{
    static ITypeShape<Person> IShapeable<Person>.GetTypeShape()
        => __GeneratedTypeShapeProvider__.Default.Person;
}
```

Consumers resolve shapes through the <xref:PolyType.IShapeable`1> constraint using the statically typed `Resolve` APIs:

```csharp
// For types annotated with [GenerateShape]:
ITypeShape<Person> shape = TypeShapeResolver.Resolve<Person>();

// For types generated via a witness (e.g. [GenerateShapeFor<Person[]>] on Witness):
ITypeShape<Person[]> shape = TypeShapeResolver.Resolve<Person[], Witness>();
```

These methods call directly into the source-generated `IShapeable<T>.GetTypeShape()` implementation, requiring no reflection and carrying no trimming or AOT concerns.

### Dynamic resolution on older frameworks

On older frameworks (net472, netstandard2.0), `IShapeable<T>` is not available because static abstract interface members are not supported. Instead, the generator emits a <xref:PolyType.Abstractions.TypeShapeProviderAttribute> on the annotated type, pointing to a nested `ITypeShapeProvider` implementation:

```csharp
[TypeShapeProvider(typeof(__LocalTypeShapeProvider__))]
partial record Person
{
    private sealed class __LocalTypeShapeProvider__ : ITypeShapeProvider
    {
        ITypeShape? ITypeShapeProvider.GetTypeShape(Type type)
        {
            if (type == typeof(Person))
                return __GeneratedTypeShapeProvider__.Default.Person;

            return null;
        }
    }
}
```

The `TypeShapeProviderAttribute` links `Person` to its nested provider class, which simply delegates to the project-wide `SourceGenTypeShapeProvider` singleton. Consumers on these frameworks discover shapes at run time using reflection via <xref:PolyType.Abstractions.TypeShapeResolver.ResolveDynamic``1>:

```csharp
// For types annotated with [GenerateShape]:
ITypeShape<Person>? shape = TypeShapeResolver.ResolveDynamic<Person>();

// For types generated via a witness:
ITypeShape<Person[]>? shape = TypeShapeResolver.ResolveDynamic<Person[], Witness>();

// Throwing variants:
ITypeShape<Person> shape = TypeShapeResolver.ResolveDynamicOrThrow<Person>();
```

`ResolveDynamic` uses reflection to look for a <xref:PolyType.Abstractions.TypeShapeProviderAttribute> on the provider type (the type itself for `[GenerateShape]`, or the witness type for `[GenerateShapeFor]`). When found, it instantiates the referenced `ITypeShapeProvider` and calls `GetTypeShape`. On .NET 8 or later, `ResolveDynamic` also checks for `IShapeable<T>` implementations as a forward-compatibility measure.

#### Trimmer safety

Even though `ResolveDynamic` uses reflection internally, the pattern is fully trimmer-safe. This works because the source generator emits a `typeof(...)` expression in the `TypeShapeProviderAttribute` constructor argument, which the ILC trimmer can follow statically. The <xref:PolyType.Abstractions.TypeShapeProviderAttribute> class annotates its `Type` parameter with [`[DynamicallyAccessedMembers]`](https://learn.microsoft.com/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembersattribute), requesting preservation of the provider type's public parameterless constructor and interface implementations. The trimmer honors these annotations, so `Activator.CreateInstance` and the subsequent `ITypeShapeProvider` cast both succeed at run time — no `RequiresUnreferencedCode` warning is necessary.

> [!NOTE]
> On .NET 8 specifically, the `ResolveDynamic` methods are annotated with `[RequiresDynamicCode]`. This is because the `IShapeable<T>` forward-compatibility path uses `MakeGenericMethod`, which may require runtime code generation in .NET 8 Native AOT. Starting with .NET 9, the AOT toolchain [recognizes `MakeGenericMethod` calls](https://github.com/dotnet/runtime/issues/119440#issuecomment-3269894751) where the type arguments come from `typeof(T)` on generic type parameters and pre-compiles the necessary instantiations, so the `[RequiresDynamicCode]` annotation is no longer required.

For API reference details, see <xref:PolyType.Abstractions.TypeShapeResolver>.
