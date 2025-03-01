namespace PolyType;

#pragma warning disable SA1602 // Enumeration items should be documented

/// <summary>
/// Polyfills the F# source construct flags enum
/// https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-sourceconstructflags.html
/// </summary>
internal enum FSharpSourceConstructFlags
{
    None = 0,
    SumType = 1,
    RecordType = 2,
    ObjectType = 3,
    Field = 4,
    Exception = 5,
    Closure = 6,
    Module = 7,
    UnionCase = 8,
    Value = 9,
    KindMask = 31,
    NonPublicRepresentation = 32,
}