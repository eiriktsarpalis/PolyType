# Performance

## Case Study: Writing a JSON serializer

The repo includes a [JSON serializer](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples/JsonSerializer) built on top of the `Utf8JsonWriter`/`Utf8JsonReader` primitives provided by System.Text.Json. At the time of writing, the full implementation is just under 1200 lines of code but exceeds STJ's built-in `JsonSerializer` both in terms of [supported types](https://github.com/eiriktsarpalis/PolyType/blob/main/tests/PolyType.Tests/JsonTests.cs) and performance.

Here's a [benchmark](https://github.com/eiriktsarpalis/PolyType/blob/main/tests/PolyType.Benchmarks/JsonBenchmark.cs) comparing `System.Text.Json` with the included PolyType implementation<sup><a href="#benchmark-environment">1</a></sup>:

### Serialization

| Method                          | Mean      | Ratio | Allocated | Alloc Ratio |
|-------------------------------- |----------:|------:|----------:|------------:|
| Serialize_StjReflection         | 140.33 ns |  1.00 |     312 B |        1.00 |
| Serialize_StjSourceGen          | 140.41 ns |  1.00 |     312 B |        1.00 |
| Serialize_StjSourceGen_FastPath |  68.16 ns |  0.49 |         - |        0.00 |
| Serialize_PolyTypeReflection    |  88.54 ns |  0.63 |         - |        0.00 |
| Serialize_PolyTypeSourceGen     |  91.95 ns |  0.66 |         - |        0.00 |

### Deserialization

| Method                         | Mean     | Ratio | Allocated | Alloc Ratio |
|------------------------------- |---------:|------:|----------:|------------:|
| Deserialize_StjReflection      | 548.4 ns |  1.00 |    1016 B |        1.00 |
| Deserialize_StjSourceGen       | 556.1 ns |  1.01 |     992 B |        0.98 |
| Deserialize_PolyTypeReflection | 269.7 ns |  0.49 |     440 B |        0.43 |
| Deserialize_PolyTypeSourceGen  | 260.4 ns |  0.47 |     440 B |        0.43 |

Even though both serializers target the same underlying reader and writer types, the PolyType implementation takes approximately 34-37% less time for serialization and 51-53% less time for deserialization in this benchmark, when compared with System.Text.Json's metadata serializer. As expected, fast-path serialization is still fastest since its implementation is fully inlined.

<a id="benchmark-environment"></a>

<sup>1</sup> Results collected using BenchmarkDotNet on an Apple M4 Pro (Arm64) running macOS 27.0.1. The benchmarks target .NET 11 and use SDK version `11.0.100-rc.1.26425.128`.

To rerun both JSON benchmark suites from the repository root:

```bash
dotnet run --project tests/PolyType.Benchmarks -c Release -- --filter '*Json*'
```
