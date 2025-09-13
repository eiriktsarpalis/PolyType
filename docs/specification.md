# Type Shape Derivation Specification

This document provides a comprehensive specification for how .NET types are mapped to PolyType shapes. Both built-in shape providers (source generator and reflection provider) implement identical derivation logic that maps arbitrary .NET types into type shapes of different kinds.

## Overview

PolyType categorizes .NET types into eight distinct shape kinds, each represented by a specific interface:

- **Object** - <xref:PolyType.Abstractions.IObjectTypeShape> for general object types with properties and constructors  
- **Enumerable** - <xref:PolyType.Abstractions.IEnumerableTypeShape> for collection types implementing @System.Collections.Generic.IEnumerable`1
- **Dictionary** - <xref:PolyType.Abstractions.IDictionaryTypeShape> for key-value collection types
- **Enum** - <xref:PolyType.Abstractions.IEnumTypeShape> for enumeration types
- **Optional** - <xref:PolyType.Abstractions.IOptionalTypeShape> for nullable value types and F# options
- **Surrogate** - <xref:PolyType.Abstractions.ISurrogateTypeShape> for types with custom marshaling
- **Union** - <xref:PolyType.Abstractions.IUnionTypeShape> for discriminated union types
- **Function** - <xref:PolyType.Abstractions.IFunctionTypeShape> for delegate and F# function types

## Derivation Algorithm

Type shape derivation follows a priority-based algorithm that checks conditions in the following order:

### 1. Surrogate Types (Highest Priority)

**Condition**: A type is mapped to <xref:PolyType.Abstractions.ISurrogateTypeShape> when a custom marshaler is explicitly configured.

**Implementation Details**:
- Requires an <xref:PolyType.IMarshaler`2> implementation
- The marshaler provides bidirectional mapping between the original type and surrogate type
- Configured via the <xref:PolyType.GenerateShapeAttribute> attribute's `Marshaler` parameter or type extensions
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

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IEnumTypeShape> when:
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

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IOptionalTypeShape> when:
- It is @System.Nullable`1 (e.g., `int?`, `DateTime?`)
- It is an F# option type with appropriate metadata

**Implementation Details**:
- For nullable value types: @System.Nullable.GetUnderlyingType* returns non-null
- For F# options: detected via F# reflection helpers
- Provides access to the element type and construction/deconstruction methods

**Examples**:
```csharp
int? nullableInt;           // Maps to IOptionalTypeShape<int?, int>
DateTime? nullableDate;     // Maps to IOptionalTypeShape<DateTime?, DateTime>
// F# option<'T> types are also supported
```

### 4. Function Types

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IFunctionTypeShape> when:
- @System.Delegate.IsAssignableFrom* returns `true`
- This includes @System.Func`1, @System.Action, and custom delegate types

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

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IUnionTypeShape> when (requires `allowUnionShapes = true`):
- It has F# discriminated union metadata (detected via `FSharpReflectionHelpers`)
- It has <xref:PolyType.DerivedTypeShapeAttribute> attributes declaring union cases
- It has WCF @System.Runtime.Serialization.DataContractAttribute with @System.Runtime.Serialization.KnownTypeAttribute attributes

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

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IDictionaryTypeShape> when it implements (checked in priority order):
- @System.Collections.Generic.IDictionary`2
- @System.Collections.Generic.IReadOnlyDictionary`2
- Non-generic @System.Collections.IDictionary

**Implementation Details**:
- Takes precedence over @System.Collections.Generic.IEnumerable`1 since dictionaries also implement enumerable
- For generic dictionaries: provides strongly-typed key and value types
- For non-generic @System.Collections.IDictionary: uses `object` for both key and value types
- Supports construction via collection constructors or factory methods

**Examples**:
```csharp
Dictionary<string, int>              // IDictionary<string, int>
ConcurrentDictionary<int, string>    // IDictionary<int, string>  
ReadOnlyDictionary<string, object>   // IReadOnlyDictionary<string, object>
Hashtable                           // IDictionary (non-generic)
```

### 7. Enumerable Types

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IEnumerableTypeShape> when:
- It implements @System.Collections.Generic.IEnumerable`1 (except @System.String)
- It implements non-generic @System.Collections.IEnumerable (except @System.String)  
- It implements @System.Collections.Generic.IAsyncEnumerable`1
- It is an array type (including multi-dimensional)
- It is @System.Memory`1 or @System.ReadOnlyMemory`1
- It is @System.Span`1 or @System.ReadOnlySpan`1

**Implementation Details**:
- @System.String is explicitly excluded despite implementing @System.Collections.Generic.IEnumerable`1
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

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IObjectTypeShape> when:
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

The @System.String type is explicitly treated as an object type, not as @System.Collections.Generic.IEnumerable`1, despite implementing the enumerable interface. This prevents unintended character-level processing in serialization scenarios.

### Dictionary Priority

Dictionary detection has higher priority than enumerable detection because dictionary types also implement @System.Collections.Generic.IEnumerable`1. This ensures they are correctly categorized as dictionaries rather than enumerables.

### Union Shape Prerequisites

Union type detection only occurs when `allowUnionShapes` is `true`. This parameter is used to prevent infinite recursion when resolving derived types within union hierarchies.

### Type Parameter Constraints

Generic type parameters and unbound generic types are not supported for shape generation. Only constructed generic types with concrete type arguments can be mapped to shapes.

### F# Interoperability

The derivation logic includes special handling for F# types:
- F# option types map to <xref:PolyType.Abstractions.IOptionalTypeShape>
- F# discriminated unions map to <xref:PolyType.Abstractions.IUnionTypeShape>
- F# function types map to <xref:PolyType.Abstractions.IFunctionTypeShape>
- Detection relies on F# metadata attributes and reflection helpers

## Validation and Testing

Both shape providers implement extensive test coverage to ensure derivation logic consistency:

- **Conformance Tests**: Verify that reflection and source generator providers produce equivalent shapes for the same input types
- **Edge Case Testing**: Cover boundary conditions like generic type constraints, accessibility, and inheritance hierarchies  
- **Integration Testing**: Validate end-to-end scenarios with real-world type hierarchies

The test suite includes over 49,000 test cases ensuring robust and consistent behavior across different .NET runtime versions and target frameworks.