# Type Shape Derivation Specification

This document provides a comprehensive specification for how .NET types are mapped to PolyType shapes. Both built-in shape providers (source generator and reflection provider) implement identical derivation logic that maps arbitrary .NET types into type shapes of different kinds.

## Overview

PolyType categorizes .NET types into eight distinct shape kinds, each represented by a specific interface:

- **Object** - `IObjectTypeShape` for general object types with properties and constructors  
- **Enumerable** - `IEnumerableTypeShape` for collection types implementing `IEnumerable<T>`
- **Dictionary** - `IDictionaryTypeShape` for key-value collection types
- **Enum** - `IEnumTypeShape` for enumeration types
- **Optional** - `IOptionalTypeShape` for nullable value types and F# options
- **Surrogate** - `ISurrogateTypeShape` for types with custom marshaling
- **Union** - `IUnionTypeShape` for discriminated union types
- **Function** - `IFunctionTypeShape` for delegate and F# function types

## Derivation Algorithm

Type shape derivation follows a priority-based algorithm that checks conditions in the following order:

### 1. Surrogate Types (Highest Priority)

**Condition**: A type is mapped to `ISurrogateTypeShape` when a custom marshaler is explicitly configured.

**Implementation Details**:
- Requires an `IMarshaler<T, TSurrogate>` implementation
- The marshaler provides bidirectional mapping between the original type and surrogate type
- Configured via the `GenerateShape` attribute's `Marshaler` parameter or type extensions
- Takes precedence over all other shape kinds

**Examples**:
```csharp
// Custom marshaler example
[GenerateShape(Marshaler = typeof(PersonToStringMarshaler))]
public partial record Person(string Name, int Age);

public class PersonToStringMarshaler : IMarshaler<Person, string>
{
    public string Marshal(Person value) => $"{value.Name}:{value.Age}";
    public Person Unmarshal(string value) => /* implementation */;
}
```

### 2. Enum Types

**Condition**: A type is mapped to `IEnumTypeShape` when:
- `type.IsEnum` returns `true`

**Implementation Details**:
- Includes the underlying numeric type (byte, int, long, etc.)
- Provides access to all enum members and their values
- Supports custom enum member naming through overridable methods

**Examples**:
```csharp
public enum Color { Red = 1, Green = 2, Blue = 3 }
public enum FileAccess : byte { Read = 1, Write = 2, ReadWrite = 3 }
```

### 3. Optional Types

**Condition**: A type is mapped to `IOptionalTypeShape` when:
- It is `Nullable<T>` (e.g., `int?`, `DateTime?`)
- It is an F# option type with appropriate metadata

**Implementation Details**:
- For nullable value types: `Nullable.GetUnderlyingType(type) != null`
- For F# options: detected via F# reflection helpers
- Provides access to the element type and construction/deconstruction methods

**Examples**:
```csharp
int? nullableInt;           // Maps to IOptionalTypeShape<int?, int>
DateTime? nullableDate;     // Maps to IOptionalTypeShape<DateTime?, DateTime>
// F# option<'T> types are also supported
```

### 4. Function Types

**Condition**: A type is mapped to `IFunctionTypeShape` when:
- `typeof(Delegate).IsAssignableFrom(type)` returns `true`
- This includes `Func<>`, `Action<>`, and custom delegate types

**Implementation Details**:
- Provides access to parameter shapes and return type
- Supports both creation and invocation of delegate instances
- Handles F# function types through specialized detection

**Examples**:
```csharp
Func<int, string>           // Maps to IFunctionTypeShape
Action<string, int>         // Maps to IFunctionTypeShape  
Predicate<Person>          // Maps to IFunctionTypeShape
MyCustomDelegate           // Maps to IFunctionTypeShape
```

### 5. Union Types

**Condition**: A type is mapped to `IUnionTypeShape` when (requires `allowUnionShapes = true`):
- It has F# discriminated union metadata (detected via `FSharpReflectionHelpers`)
- It has `[DerivedTypeShape]` attributes declaring union cases
- It has WCF `[DataContract]` with `[KnownType]` attributes

**Implementation Details**:
- Provides access to all union cases/derived types
- Each case has a unique identifier (name and optional numeric tag)
- Supports both open and closed union hierarchies

**Examples**:
```csharp
// Explicit union with DerivedTypeShape attributes
[DerivedTypeShape<Dog>]
[DerivedTypeShape<Cat>]  
public abstract partial record Animal;

// WCF-style unions
[DataContract]
[KnownType(typeof(Circle))]
[KnownType(typeof(Rectangle))]
public abstract class Shape { }
```

### 6. Dictionary Types

**Condition**: A type is mapped to `IDictionaryTypeShape` when it implements (checked in priority order):
- `IDictionary<TKey, TValue>`
- `IReadOnlyDictionary<TKey, TValue>`
- Non-generic `IDictionary`

**Implementation Details**:
- Takes precedence over `IEnumerable<T>` since dictionaries also implement enumerable
- For generic dictionaries: provides strongly-typed key and value types
- For non-generic `IDictionary`: uses `object` for both key and value types
- Supports construction via collection constructors or factory methods

**Examples**:
```csharp
Dictionary<string, int>              // IDictionary<string, int>
ConcurrentDictionary<int, string>    // IDictionary<int, string>  
ReadOnlyDictionary<string, object>   // IReadOnlyDictionary<string, object>
Hashtable                           // IDictionary (non-generic)
```

### 7. Enumerable Types

**Condition**: A type is mapped to `IEnumerableTypeShape` when:
- It implements `IEnumerable<T>` (except `string`)
- It implements non-generic `IEnumerable` (except `string`)  
- It implements `IAsyncEnumerable<T>`
- It is an array type (including multi-dimensional)
- It is `Memory<T>` or `ReadOnlyMemory<T>`
- It is `Span<T>` or `ReadOnlySpan<T>`

**Implementation Details**:
- `string` is explicitly excluded despite implementing `IEnumerable<char>`
- Dictionary types are excluded since they're handled by dictionary mapping
- Provides element type information and construction capabilities
- Supports various collection interfaces and span types

**Examples**:
```csharp
List<int>                    // IEnumerable<int>
int[]                        // Array of int
HashSet<string>              // IEnumerable<string>
IEnumerable<Person>          // IEnumerable<Person>
Memory<byte>                 // Treated as enumerable
ReadOnlySpan<char>           // Treated as enumerable
// string is NOT treated as IEnumerable<char>
```

### 8. Object Types (Default)

**Condition**: A type is mapped to `IObjectTypeShape` when:
- It doesn't match any of the above categories
- This is the fallback for all other types

**Implementation Details**:
- Provides access to properties, fields, and constructors
- Supports both mutable and immutable object patterns
- Can optionally disable member resolution for unsupported types
- Handles record types, classes, structs, and interfaces

**Examples**:
```csharp
public record Person(string Name, int Age);     // Object type
public class Customer { ... }                   // Object type  
public struct Point { public int X, Y; }        // Object type
public interface IService { ... }               // Object type
```

## Special Cases and Considerations

### String Exclusion

The `string` type is explicitly treated as an object type, not as `IEnumerable<char>`, despite implementing the enumerable interface. This prevents unintended character-level processing in serialization scenarios.

### Dictionary Priority

Dictionary detection has higher priority than enumerable detection because dictionary types also implement `IEnumerable<KeyValuePair<TKey, TValue>>`. This ensures they are correctly categorized as dictionaries rather than enumerables.

### Union Shape Prerequisites

Union type detection only occurs when `allowUnionShapes` is `true`. This parameter is used to prevent infinite recursion when resolving derived types within union hierarchies.

### Type Parameter Constraints

Generic type parameters and unbound generic types are not supported for shape generation. Only constructed generic types with concrete type arguments can be mapped to shapes.

### F# Interoperability

The derivation logic includes special handling for F# types:
- F# option types map to `IOptionalTypeShape`
- F# discriminated unions map to `IUnionTypeShape`
- F# function types map to `IFunctionTypeShape`
- Detection relies on F# metadata attributes and reflection helpers

## Validation and Testing

Both shape providers implement extensive test coverage to ensure derivation logic consistency:

- **Conformance Tests**: Verify that reflection and source generator providers produce equivalent shapes for the same input types
- **Edge Case Testing**: Cover boundary conditions like generic type constraints, accessibility, and inheritance hierarchies  
- **Integration Testing**: Validate end-to-end scenarios with real-world type hierarchies

The test suite includes over 49,000 test cases ensuring robust and consistent behavior across different .NET runtime versions and target frameworks.