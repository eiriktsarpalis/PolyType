# PolyType.FastPathJson

`PolyType.FastPathJson` is a toy proof of concept for `PolyType.Roslyn`. **It is not intended for production use or a production release.**

The generator reuses the shared Roslyn type models to emit direct member access, constructor calls, and JSON traversal instead of runtime type shapes. The generated code implements `IJsonSerializable<T>` with static `Write` and `Read` operations.

The two projects have separate roles:

| Project | Target | Role |
|---------|--------|------|
| `PolyType.FastPathJson` | `net10.0` | Serialization contract, convenience methods, and reusable converter witnesses |
| `PolyType.FastPathJson.SourceGenerator` | `netstandard2.0` | Incremental generator that consumes the shared Roslyn models and emits direct member access, constructor calls, and JSON traversal |

The projects have no dependency on `PolyType.dll` or the built-in `PolyType.SourceGenerator`, and are not packaged.

Object readers match unescaped UTF-8 property names without allocating strings. Contiguous,
unescaped names borrow the reader's input; escaped or segmented names use `Utf8JsonReader.CopyString`
with reusable stack or pooled scratch space. Generated switches select by byte length and a
discriminating byte, then use `SequenceEqual` against UTF-8 literals so the JIT can compare
multiple bytes at once instead of checking every element of a full byte-list pattern.
When every known name fits the 256-byte stack buffer even with `\uXXXX` escaping, readers
omit pooled-buffer state and its `try`/`finally`. Oversized unknown names still undergo
validation in a separate slow path before their values are skipped.

## Usage

Reference `PolyType.FastPathJson` normally and reference `PolyType.FastPathJson.SourceGenerator` with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"`.

Use `[GenerateJson]` on a non-generic partial type:

```csharp
using PolyType.FastPathJson;

[GenerateJson]
public partial record Person(string Name, int Age);
```

Serialize and deserialize through the generated implementation:

```csharp
Person person = new("John", 39);
string json = FastPathJsonSerializer.Serialize(person);
Person? restored = FastPathJsonSerializer.Deserialize<Person>(json.AsSpan());
```

External types can use a separate witness:

```csharp
[GenerateJsonFor<List<Person>>]
public sealed partial class JsonWitness;
```

Pass the witness explicitly:

```csharp
string document = FastPathJsonSerializer.Serialize<List<Person>, JsonWitness>([person]);
```

## Native AOT size comparison

Two apps round-tripped the same 30-property POCO with nested objects, collections, and representative scalars, producing identical 1,145-byte JSON.

| Serializer | Native EXE bytes | MiB |
|------------|-----------------:|----:|
| `PolyType.Examples.JsonSerializer` with source-generated shapes | 2,875,904 | 2.743 |
| `PolyType.FastPathJson` | 2,114,560 | 2.017 |

The fast-path app was 761,344 bytes (743.5 KiB, 26.5%) smaller for this model.

Measured on 2026-10-27 using .NET 11 RC3 SDK `11.0.100-rc.3.26522.148`, `win-x64`, Release, `OptimizationPreference=Size`, `InvariantGlobalization=true`, and `StackTraceSupport=false`. Sizes exclude PDBs and represent whole executables, not isolated serializer libraries.
