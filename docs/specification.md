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
- **Union** - <xref:PolyType.Abstractions.IUnionTypeShape> for polymorphic type hierarchies or discriminated union types
- **Function** - <xref:PolyType.Abstractions.IFunctionTypeShape> for delegate and F# function types

## Derivation Algorithm

Type shape derivation follows a priority-based algorithm that checks conditions in the following order:

### 1. Surrogate Types (Highest Priority)

**Condition**: A type is mapped to <xref:PolyType.Abstractions.ISurrogateTypeShape> when a custom marshaler is explicitly configured.

**Implementation Details**:
- Requires an <xref:PolyType.IMarshaler`2> implementation
- The marshaler provides bidirectional mapping between the original type and surrogate type
- Configured via the <xref:PolyType.GenerateShapeAttribute>, <xref:PolyType.TypeShapeAttribute>, or <xref:PolyType.TypeShapeExtensionAttribute> attributes' `Marshaler` parameter
- Takes precedence over all other shape kinds

### 2. Enum Types

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IEnumTypeShape> when:
- It is an enum type

**Specification**: A type is mapped to <xref:PolyType.Abstractions.IEnumTypeShape> when:
- It is an enum type

- The underlying numeric type maps to the underlying enum property of <xref:PolyType.Abstractions.IEnumTypeShape>
- Enum member names can be overridden using <xref:PolyType.PropertyShapeAttribute> attributes


### 3. Optional Types

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IOptionalTypeShape> when:
- It is @System.Nullable`1 (e.g., `int?`, `DateTime?`)
- It is an F# option type

**Specification**: A type is mapped to <xref:PolyType.Abstractions.IOptionalTypeShape> when:
- It is @System.Nullable`1 (e.g., `int?`, `DateTime?`)
- It is an F# option type

- For nullable value types: @System.Nullable.GetUnderlyingType* returns non-null
- For F# options: detected via F# reflection helpers
- Provides access to the element type and construction/deconstruction methods



### 4. Function Types

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IFunctionTypeShape> when:
- It is a delegate type

**Specification**: A type is mapped to <xref:PolyType.Abstractions.IFunctionTypeShape> when:
- It is a delegate type

- Provides access to parameter shapes and return type
- Supports both creation and invocation of delegate instances
- Handles F# function types through specialized detection



### 5. Union Types

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IUnionTypeShape> when:
- It has F# discriminated union metadata (detected via `FSharpReflectionHelpers`)
- It has <xref:PolyType.DerivedTypeShapeAttribute> attributes declaring union cases
- It has WCF @System.Runtime.Serialization.DataContractAttribute with @System.Runtime.Serialization.KnownTypeAttribute attributes

- Provides access to all union cases/derived types
- Each case has a unique identifier inferred from type name and optional discriminator values specified via attributes
- Supports both open and closed union hierarchies



### 6. Dictionary Types

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IDictionaryTypeShape> when it implements (checked in priority order):
- @System.Collections.Generic.IDictionary`2
- @System.Collections.Generic.IReadOnlyDictionary`2
- Non-generic @System.Collections.IDictionary

- Takes precedence over @System.Collections.Generic.IEnumerable`1 since dictionaries also implement enumerable
- For generic dictionaries: provides strongly-typed key and value types
- For non-generic @System.Collections.IDictionary: uses `object` for both key and value types
- Supports construction via collection constructors or factory methods



### 7. Enumerable Types

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IEnumerableTypeShape> when:
- It implements @System.Collections.Generic.IEnumerable`1 (except @System.String)
- It implements non-generic @System.Collections.IEnumerable, using `object` as the element type
- It implements @System.Collections.Generic.IAsyncEnumerable`1
- It is an array type (including multi-dimensional arrays)
- It is @System.Memory`1 or @System.ReadOnlyMemory`1

- @System.String is explicitly excluded despite implementing @System.Collections.Generic.IEnumerable`1
- Dictionary types are excluded since they're handled by dictionary mapping
- Provides element type information and construction capabilities



### 8. Object Types (Default)

**Condition**: A type is mapped to <xref:PolyType.Abstractions.IObjectTypeShape> when it doesn't match any of the above categories.

### Property Shape Resolution

Property shapes are resolved using the following criteria:

- **Inheritance**: Properties from base classes are included, with derived class properties taking precedence over base class properties with the same name
- **Accessibility**: Only properties with accessible getters or setters are included
- **Shadowing**: Derived class properties shadow base class properties with identical signatures
- **Field Mapping**: Public fields are mapped to property shapes with matching getter/setter semantics
- **Attribute Overrides**: <xref:PolyType.PropertyShapeAttribute> can customize property names and behavior

### Constructor Shape Resolution

Constructor shapes are resolved using the following algorithm:

- **Accessibility**: Only public constructors are considered by default
- **Disambiguation**: When multiple constructors are available, preference is given to constructors with <xref:PolyType.ConstructorShapeAttribute>
- **Parameter Population**: Constructor parameters are matched to properties by name (case-insensitive)
- **Property Setters**: Properties with accessible setters that don't match constructor parameters form part of the logical constructor signature
- **Argument State Type**: The chosen argument state type accommodates both constructor parameters and settable properties

### Method Shape Derivation

Method shapes are derived independently of type shape kinds and follow these rules:

- **Accessibility**: Only public methods are included by default
- **Instance Methods**: Non-static methods are mapped to instance method shapes
- **Static Methods**: Static methods are mapped to static method shapes  
- **Generic Methods**: Generic method definitions are supported with proper type parameter mapping
- **Overrides**: Method overrides in derived classes replace base class method shapes

### Event Shape Derivation

Event shapes are derived independently of type shape kinds and follow these rules:

- **Accessibility**: Only public events are included by default
- **Instance Events**: Non-static events are mapped to instance event shapes
- **Static Events**: Static events are mapped to static event shapes
- **Event Handler Types**: Event handler delegate types are mapped to their corresponding function shapes



## Special Cases and Considerations

### String Exclusion

The @System.String type is explicitly treated as an object type, not as @System.Collections.Generic.IEnumerable`1, despite implementing the enumerable interface. This prevents unintended character-level processing in serialization scenarios.

### Dictionary Priority

Dictionary detection has higher priority than enumerable detection because dictionary types also implement @System.Collections.Generic.IEnumerable`1. This ensures they are correctly categorized as dictionaries rather than enumerables.

### F# Interoperability

The derivation logic includes special handling for F# types:
- F# option types map to <xref:PolyType.Abstractions.IOptionalTypeShape>
- F# discriminated unions map to <xref:PolyType.Abstractions.IUnionTypeShape>
- F# function types map to <xref:PolyType.Abstractions.IFunctionTypeShape>
- Detection relies on F# metadata attributes and reflection helpers

## Primitive Type Mappings

| .NET Type | Shape Kind |
|-----------|------------|
| @System.Boolean | Object |
| @System.Byte | Object |
| @System.SByte | Object |
| @System.Int16 | Object |
| @System.UInt16 | Object |
| @System.Int32 | Object |
| @System.UInt32 | Object |
| @System.Int64 | Object |
| @System.UInt64 | Object |
| @System.Single | Object |
| @System.Double | Object |
| @System.Decimal | Object |
| @System.Char | Object |
| @System.String | Object |
| @System.DateTime | Object |
| @System.DateTimeOffset | Object |
| @System.TimeSpan | Object |
| @System.Guid | Object |