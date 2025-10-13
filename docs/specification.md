# Specification

This document details how .NET types are mapped to type shapes. Both built-in shape providers (source generator and reflection provider) implement equivalent derivation logics that map arbitrary .NET types into type shapes of different kinds.

## Overview

PolyType classifies .NET types into eight distinct type shape kinds, each represented by a specific interface:

- **Object** - <xref:PolyType.Abstractions.IObjectTypeShape> for general object types with properties and constructors.
- **Enumerable** - <xref:PolyType.Abstractions.IEnumerableTypeShape> for collection types implementing <xref:System.Collections.Generic.IEnumerable`1>
- **Dictionary** - <xref:PolyType.Abstractions.IDictionaryTypeShape> for key-value collection types.
- **Enum** - <xref:PolyType.Abstractions.IEnumTypeShape> for enum types.
- **Optional** - <xref:PolyType.Abstractions.IOptionalTypeShape> for nullable value types and F# options.
- **Surrogate** - <xref:PolyType.Abstractions.ISurrogateTypeShape> for types that define a marshaller to a surrogate type.
- **Union** - <xref:PolyType.Abstractions.IUnionTypeShape> for polymorphic type hierarchies or discriminated union types.
- **Function** - <xref:PolyType.Abstractions.IFunctionTypeShape> for delegate and F# function types.

## Derivation Algorithm

PolyType maps types into individual shape kinds using the following rules:

### Enum Types

A type is mapped to <xref:PolyType.Abstractions.IEnumTypeShape> if and only if it is an enum type.

### Optional Types

A type is mapped to <xref:PolyType.Abstractions.IOptionalTypeShape> when:

1. It is <xref:System.Nullable`1> (e.g., `int?`, `DateTime?`) or
2. It is an F# option type

### Function Types

A type is mapped to <xref:PolyType.Abstractions.IFunctionTypeShape> when:

1. It is a delegate type or
2. It is an F# function type (i.e. any type deriving from [`FSharpFunc<T,R>`](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-fsharpfunc-2.html)).

### Surrogate Types

A type is mapped to <xref:PolyType.Abstractions.ISurrogateTypeShape> if and only if the type has been given an <xref:PolyType.IMarshaler`2> implementation to a surrogate type.
This is typically configured via the `Marshaller` property on the <xref:PolyType.TypeShapeAttribute>, <xref:PolyType.GenerateShapeAttribute>, or <xref:PolyType.TypeShapeExtensionAttribute>
attributes and doing so overrides the built-in shape kind inferred for the type.

### Union Types

A type is mapped to <xref:PolyType.Abstractions.IUnionTypeShape> when:

1. It is a class with <xref:PolyType.DerivedTypeShapeAttribute> annotations or
2. It has [`DataContractAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.datacontractattribute) with [`KnownTypeAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.knowntypeattribute) annotations or
3. It is an F# union type.

### Dictionary Types

A type is mapped to <xref:PolyType.Abstractions.IDictionaryTypeShape> when it implements:

1. <xref:System.Collections.Generic.IDictionary`2> or
2. <xref:System.Collections.Generic.IReadOnlyDictionary`2> or
3. The non-generic <xref:System.Collections.IDictionary>

Types implementing <xref:System.Collections.IDictionary> use `object` to represent both key and value type shapes.

#### Construction Strategy

The construction strategy for dictionary types is inferred using the following priority:

1. Types with public parameterless constructors that additionally expose `Add` and indexer methods, or implement one of the mutable `IDictionary` interfaces are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.Mutable>.
2. Immutable or frozen dictionary types are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.Parameterized>.
3. Types with public constructors accepting `ReadOnlySpan<KeyValuePair<K,V>>` or `IEnumerable<KeyValuePair<K,V>>` parameters are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.Parameterized>.
4. Types annotated with [`CollectionBuilderAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.collectionbuilderattribute) are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.Parameterized>.
5. Dictionary types not matching the above are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.None>.

PolyType will automatically select constructor or factory method overloads that additionally accept `IEqualityComparer<Key>` or `IComparer<Key>` and map those to the corresponding settings in the <xref:PolyType.Abstractions.CollectionConstructionOptions`1> used by the constructor delegate.
For parameterless constructors it will additionally look for `int capacity` parameters that map to the <xref:PolyType.Abstractions.CollectionConstructionOptions`1.Capacity> property.

### Enumerable Types

A non-dictionary type is mapped to <xref:PolyType.Abstractions.IEnumerableTypeShape> when:

1. It implements <xref:System.Collections.Generic.IEnumerable`1> (except <xref:System.String>) or
2. It implements non-generic <xref:System.Collections.IEnumerable> (using `object` as the element type) or
3. It implements [`IAsyncEnumerable<T>`](https://learn.microsoft.com/dotnet/api/system.collections.generic.iasyncenumerable-1) or
4. It is an array type (including multi-dimensional arrays) or
5. It is <xref:System.Memory`1> or <xref:System.ReadOnlyMemory`1>.

#### Construction Strategy

The construction strategy for enumerable types is inferred using the following priority:

1. Types with public parameterless constructors that additionally expose `Add` methods, or implement one of the mutable `ICollection` interfaces are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.Mutable>.
2. Immutable or frozen collection types are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.Parameterized>.
3. Types with public constructors accepting `ReadOnlySpan<TElement>` or `IEnumerable<TElement>` parameters are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.Parameterized>.
4. Types annotated with [`CollectionBuilderAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.collectionbuilderattribute) are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.Parameterized>.
5. Enumerable types not matching the above are classified as <xref:PolyType.Abstractions.CollectionConstructionStrategy.None>.

### Object Types

Types not matching any of the above categories map to <xref:PolyType.Abstractions.IObjectTypeShape>. This includes primitive types and other irreducible values such as `string`, `DateTimeOffset`, or `Guid` which map to the type shape trivially without including any property or constructor shapes.

#### Property Shape Resolution

Property shapes are resolved using the following criteria:

- Any public property or field is included as a property shape, unless explicitly ignored using a <xref:PolyType.PropertyShapeAttribute>.
- Non-public members are not included, unless they have been explicitly annotated with a <xref:PolyType.PropertyShapeAttribute>.
- Types annotated with [`DataContractAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.datacontractattribute) only include members annotated with [`DataMemberAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.datamemberattribute) or <xref:PolyType.PropertyShapeAttribute>.
- Members from base types are included, with derived class members taking precedence over base class members with the same name (following the shadowing semantics of C#).
- Members whose type does not support being a generic parameter (pointers, ref structs) are always skipped.

Read-only fields and init-only properties do not expose a setter delegate.

#### Constructor Shape Resolution

Constructor shapes are resolved using the following algorithm:

- Only public constructors are considered by default.
- Prefer constructors annotated with <xref:PolyType.ConstructorShapeAttribute>, even if non-public.
- If the type defines multiple public constructors, pick one that:
    1. Minimizes the number of required parameters not corresponding to any property shapes, then
    2. Maximizes the number of parameters that match read-only properties/fields, and then
    3. Minimizes the total number of constructor parameters.

  Parameters correspond to a property shape if and only if they are of the same type and have matching names up to Pascal case/camel case equivalence.

If the selected constructor is parameterless and additionally there are no required or init-only properties defined on the type,
it is mapped to an <xref:PolyType.Abstractions.IConstructorShape> that is parameterless and which relies on property shape setters
to be populated.

Otherwise, the constructor gets mapped to a parameterized <xref:PolyType.Abstractions.IConstructorShape>.
The logical signature of a parameterized constructor includes parameters and _all_ settable members not corresponding to a constructor parameter,
meaning that parameterized constructors *DO NOT* require additional binding from the object's property setter delegates.

#### Tuples

Tuple and value tuple types map to object shapes. Long tuple types, that is tuples with more than 7 elements are represented in IL using [nested tuples](https://learn.microsoft.com/dotnet/api/system.valuetuple-8). PolyType flattens long tuple representations so that all elements are accessible transparently from the outer tuple.

### Method Shapes

Method shapes may be included in type shapes of any kind. By default, types do _not_ include any method shapes and need to be opted in either by

- Configuring the `IncludeMethods` property in either of the <xref:PolyType.TypeShapeAttribute>, <xref:PolyType.GenerateShapeAttribute>, or <xref:PolyType.TypeShapeExtensionAttribute> or
- Explicitly annotating a method with the <xref:PolyType.MethodShapeAttribute>.

The `IncludeMethods` approach only supports public methods, while <xref:PolyType.MethodShapeAttribute> can be used to opt in non-public methods.

### Event Shapes

Event shapes may be included in type shapes of any kind. By default, types do _not_ include any event shapes and need to be opted in either by

- Configuring the `IncludeMethods` property in either of the <xref:PolyType.TypeShapeAttribute>, <xref:PolyType.GenerateShapeAttribute>, or <xref:PolyType.TypeShapeExtensionAttribute> or
- Explicitly annotating an event with the <xref:PolyType.EventShapeAttribute>.