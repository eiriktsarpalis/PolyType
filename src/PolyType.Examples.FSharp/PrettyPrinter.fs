namespace PolyType.Examples.FSharp

open System.IO
open PolyType

/// Provides a pretty printer for .NET types built on top of PolyType.
/// This is a lightweight F# wrapper around the C# implementation from PolyType.Examples.
module PrettyPrinter =
    
    /// Builds a pretty printer from the specified shape.
    let create<'T> (shape: ITypeShape<'T>) : ('T -> string) =
        let csharpPrinter = PolyType.Examples.PrettyPrinter.PrettyPrinter.Create(shape)
        fun value -> PolyType.Examples.PrettyPrinter.PrettyPrinter.Print(csharpPrinter, value)
    
    /// Builds a pretty printer from the specified shape provider.
    let createFromProvider<'T> (provider: ITypeShapeProvider) : ('T -> string) =
        let csharpPrinter = PolyType.Examples.PrettyPrinter.PrettyPrinter.Create<'T>(provider)
        fun value -> PolyType.Examples.PrettyPrinter.PrettyPrinter.Print(csharpPrinter, value)
    
    /// Pretty prints the specified value to a string using its shape.
    let print<'T> (shape: ITypeShape<'T>) (value: 'T) : string =
        let printer = create shape
        printer value
