# .NET 10 UnsafeAccessorTypeAttribute Testing Guide

This document outlines how to test the .NET 10 `UnsafeAccessorTypeAttribute` support once .NET 10 becomes available.

## Prerequisites

- .NET 10 SDK (10.0.100 or later)
- The `UnsafeAccessorTypeAttribute` should be available in `System.Runtime.CompilerServices`

## What Was Changed

### 1. Framework Detection
- Added `Net100 = 100` to the `TargetFramework` enum
- Updated `ResolveTargetFramework()` to detect .NET 10 by checking for `System.Runtime.CompilerServices.UnsafeAccessorTypeAttribute`

### 2. Unsafe Accessor Logic
- **Methods**: Static methods on non-generic types can now use unsafe accessors on .NET 10+
- **Events**: Static events on non-generic types can now use unsafe accessors on .NET 10+

### 3. Code Generation
- Static method/event accessors now emit `[UnsafeAccessorType(typeof(...))]` attribute
- Removed the `Debug.Assert` that prevented static method accessors

## Testing Scenarios

### Test 1: Static Method with Private Access
```csharp
[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
public partial class TestClass
{
    [MethodShape]
    private static int PrivateStaticMethod(int x) => x * 2;
}
```

**Expected**: Generated code should use `UnsafeAccessor` with `UnsafeAccessorType` for accessing the private static method.

### Test 2: Static Event with Private Access
```csharp
[GenerateShape]
public partial class TestClass
{
    [EventShape]
    private static event EventHandler? PrivateStaticEvent;
}
```

**Expected**: Generated code should use `UnsafeAccessor` with `UnsafeAccessorType` for accessing the private static event.

### Test 3: Generic Type (Should Fall Back to Reflection)
```csharp
[GenerateShape]
public partial class GenericClass<T>
{
    [MethodShape]
    private static void PrivateStaticMethod() { }
}
```

**Expected**: Should use reflection-based accessor (not unsafe accessor) because the containing type is generic.

### Test 4: .NET 9 Target (Should Use Reflection)
When targeting .NET 9 or earlier, static methods should fall back to reflection even if they are private.

## Verification Steps

1. **Build the project** targeting .NET 10:
   ```bash
   dotnet build -c Release
   ```

2. **Run unit tests**:
   ```bash
   dotnet test tests/PolyType.SourceGenerator.UnitTests/
   ```

3. **Inspect generated code** for a test case with private static methods:
   - Look for `[UnsafeAccessorType(typeof(...))]` attribute
   - Verify no reflection-based workarounds are used

4. **Run integration tests**:
   ```bash
   dotnet test tests/PolyType.Tests/
   ```

5. **Test AOT compilation**:
   ```bash
   dotnet publish applications/SerializationApp.AOT/ -c Release
   ./applications/SerializationApp.AOT/bin/Release/net10.0/linux-x64/publish/SerializationApp.AOT
   ```

## Expected Generated Code

For a private static method on .NET 10+:

```csharp
[global::System.Runtime.CompilerServices.UnsafeAccessorType(typeof(TestClass))]
[global::System.Runtime.CompilerServices.UnsafeAccessor(
    global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, 
    Name = "PrivateStaticMethod")]
private static extern int __MethodAccessor_TestClass_0_PrivateStaticMethod(int x);
```

For the same on .NET 9 or earlier, it should generate reflection-based code:

```csharp
private static global::System.Reflection.MethodInfo? __s___MethodAccessor_TestClass_0_PrivateStaticMethod_MethodInfo;
private static int __MethodAccessor_TestClass_0_PrivateStaticMethod(int x)
{
    global::System.Reflection.MethodInfo methodInfo = __s___MethodAccessor_TestClass_0_PrivateStaticMethod_MethodInfo 
        ??= typeof(TestClass).GetMethod("PrivateStaticMethod", AllBindingFlags, null, new global::System.Type[] { typeof(int) }, null)!;
    object?[] paramArray = new object?[] { x };
    return (int)methodInfo.Invoke(null, paramArray)!;
}
```

## References

- Issue: https://github.com/eiriktsarpalis/PolyType/issues/220
- .NET Runtime PR: https://github.com/dotnet/runtime/pull/114881
