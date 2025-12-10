# Performance

## Case Study: Writing a JSON serializer

The repo includes a [JSON serializer](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples/JsonSerializer) built on top of the `Utf8JsonWriter`/`Utf8JsonReader` primitives provided by System.Text.Json. At the time of writing, the full implementation is just under 1200 lines of code but exceeds STJ's built-in `JsonSerializer` both in terms of [supported types](https://github.com/eiriktsarpalis/PolyType/blob/main/tests/PolyType.Tests/JsonTests.cs) and performance.

Here's a [benchmark](https://github.com/eiriktsarpalis/PolyType/blob/main/tests/PolyType.Benchmarks/JsonBenchmark.cs) comparing `System.Text.Json` with the included PolyType implementation:

### Serialization

| Method                          | Mean      | Ratio | Allocated | Alloc Ratio |
|-------------------------------- |----------:|------:|----------:|------------:|
| Serialize_StjReflection         | 150.43 ns |  1.00 |     312 B |        1.00 |
| Serialize_StjSourceGen          | 151.31 ns |  1.01 |     312 B |        1.00 |
| Serialize_StjSourceGen_FastPath |  96.79 ns |  0.64 |         - |        0.00 |
| Serialize_PolyTypeReflection    | 113.19 ns |  0.75 |         - |        0.00 |
| Serialize_PolyTypeSourceGen     | 112.92 ns |  0.75 |         - |        0.00 |

### Deserialization

| Method                         | Mean     | Ratio | Allocated | Alloc Ratio |
|------------------------------- |---------:|------:|----------:|------------:|
| Deserialize_StjReflection      | 534.0 ns |  1.00 |    1016 B |        1.00 |
| Deserialize_StjSourceGen       | 534.6 ns |  1.00 |     992 B |        0.98 |
| Deserialize_PolyTypeReflection | 273.1 ns |  0.51 |     440 B |        0.43 |
| Deserialize_PolyTypeSourceGen  | 266.3 ns |  0.50 |     440 B |        0.43 |

Even though both serializers target the same underlying reader and writer types, the PolyType implementation is ~75% faster for serialization and ~100% faster for deserialization, when compared with System.Text.Json's metadata serializer. As expected, fast-path serialization is still fastest since its implementation is fully inlined.
