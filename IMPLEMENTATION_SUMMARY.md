# Implementation Summary: .NET 10 UnsafeAccessorTypeAttribute Support

## Overview
This implementation adds support for .NET 10's new `UnsafeAccessorTypeAttribute`, which enables unsafe accessors for static methods and events. This resolves issue #220.

## Files Changed

### 1. `src/PolyType.Roslyn/TargetFramework.cs`
**Change**: Added new enum value for .NET 10
```csharp
/// <summary>
/// .NET 10 or later.
/// </summary>
Net100 = 100,
```

### 2. `src/PolyType.Roslyn/KnownSymbols.cs`
**Change**: Added .NET 10 detection logic
```csharp
INamedTypeSymbol? unsafeAccessorTypeAttribute = Compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.UnsafeAccessorTypeAttribute");
if (unsafeAccessorTypeAttribute is not null &&
    SymbolEqualityComparer.Default.Equals(unsafeAccessorTypeAttribute.ContainingAssembly, CoreLibAssembly))
{
    return TargetFramework.Net100;
}
```
**Logic**: Detects .NET 10 by checking for the presence of `UnsafeAccessorTypeAttribute` in the core library.

### 3. `src/PolyType.SourceGenerator/Parser/Parser.ModelMapper.cs`
**Changes**: Updated `CanUseUnsafeAccessors` logic for both methods and events

#### Methods (line 562-569):
```csharp
CanUseUnsafeAccessors = _knownSymbols.TargetFramework switch
{
    // .NET 10 or later supports unsafe accessors for static methods via UnsafeAccessorTypeAttribute
    var target when target >= TargetFramework.Net100 => !m.Method.ContainingType.IsGenericType,
    // .NET 8 or later supports unsafe accessors for instance methods of non-generic types.
    var target when target >= TargetFramework.Net80 => !m.Method.ContainingType.IsGenericType && !m.Method.IsStatic,
    _ => false
},
```

#### Events (line 589-596):
```csharp
CanUseUnsafeAccessors = _knownSymbols.TargetFramework switch
{
    // .NET 10 or later supports unsafe accessors for static events via UnsafeAccessorTypeAttribute.
    var target when target >= TargetFramework.Net100 => !e.Event.ContainingType.IsGenericType,
    // .NET 8 or later supports unsafe accessors for instance events of non-generic types.
    var target when target >= TargetFramework.Net80 => !e.Event.ContainingType.IsGenericType && !e.Event.IsStatic,
    _ => false
},
```

**Logic**: 
- .NET 10+: Allows unsafe accessors for static methods/events (non-generic types only)
- .NET 8-9: Allows unsafe accessors for instance methods/events only (non-generic types only)
- Earlier: Uses reflection fallback

### 4. `src/PolyType.SourceGenerator/SourceFormatter/SourceFormatter.Methods.cs`
**Change**: Updated `FormatMethodAccessor` to emit `UnsafeAccessorTypeAttribute` for static methods

```csharp
string methodRefPrefix = method.ReturnsByRef ? "ref " : "";
if (method.IsStatic)
{
    // .NET 10+ supports static method accessors using UnsafeAccessorTypeAttribute
    writer.WriteLine($"""
        [global::System.Runtime.CompilerServices.UnsafeAccessorType(typeof({method.DeclaringType.FullyQualifiedName}))]
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(method.UnderlyingMethodName)})]
        private static extern {methodRefPrefix}{method.UnderlyingReturnType.FullyQualifiedName} {accessorName}({allParameters});
        """);
}
else
{
    writer.WriteLine($"""
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(method.UnderlyingMethodName)})]
        private static extern {methodRefPrefix}{method.UnderlyingReturnType.FullyQualifiedName} {accessorName}({allParameters});
        """);
}
```

**Logic**: Static methods now get both `UnsafeAccessorType` and `UnsafeAccessor` attributes. Also removed the `Debug.Assert` that blocked static methods.

### 5. `src/PolyType.SourceGenerator/SourceFormatter/SourceFormatter.Events.cs`
**Change**: Updated `FormatEventAccessor` to emit `UnsafeAccessorTypeAttribute` for static events

```csharp
if (eventModel.IsStatic)
{
    // .NET 10+ supports static event accessors using UnsafeAccessorTypeAttribute
    writer.WriteLine($"""
        [global::System.Runtime.CompilerServices.UnsafeAccessorType(typeof({eventModel.DeclaringType.FullyQualifiedName}))]
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(methodName)})]
        private static extern void {accessorName}({eventModel.HandlerType.FullyQualifiedName} handler);
        """);
}
```

**Logic**: Static events now get both `UnsafeAccessorType` and `UnsafeAccessor` attributes.

## Key Design Decisions

1. **Framework Detection**: Uses the presence of `UnsafeAccessorTypeAttribute` itself to detect .NET 10, which is appropriate since that's the feature being leveraged.

2. **Backwards Compatibility**: The switch statements use pattern matching with guards, so .NET 8 and 9 behavior remains unchanged.

3. **Generic Type Restriction**: Maintains the restriction that unsafe accessors only work for non-generic types (for both static and instance members).

4. **Fallback to Reflection**: When unsafe accessors cannot be used (generic types, older frameworks), the code falls back to reflection-based access.

## Testing Status

- **Build**: Cannot be tested until .NET 10 SDK is released
- **Unit Tests**: Existing tests will validate backwards compatibility when .NET 10 is available
- **Manual Testing**: `NET10_TESTING.md` provides comprehensive testing scenarios

## References

- Issue: https://github.com/eiriktsarpalis/PolyType/issues/220
- .NET Runtime PR: https://github.com/dotnet/runtime/pull/114881
- UnsafeAccessorTypeAttribute documentation: (Available when .NET 10 is released)
