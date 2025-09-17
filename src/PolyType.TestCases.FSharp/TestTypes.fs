namespace PolyType.Tests.FSharp

open System.Threading.Tasks

type FSharpRecord =
    {
        IntProperty: int
        StringProperty: string
        BoolProperty: bool
    }

[<Struct>]
type FSharpStructRecord =
    {
        IntProperty: int
        StringProperty: string
        BoolProperty: bool
    }

type GenericFSharpRecord<'T> =
    {
        GenericProperty: 'T
    }

[<Struct>]
type GenericFSharpStructRecord<'T> =
    {
        GenericProperty: 'T
    }

type FSharpRecordWithCollections =
    {
        IntArray: int[] option
        StringList: string list
        BoolSet: Set<bool>
        IntMap: Map<string, int>
        Tuple : (int * string * bool) voption
        StructTuple : struct(int * string * bool)
    }
with
    static member Create() =
        {
            IntArray = Some [|1; 2; 3|]
            StringList = ["a"; "b"; "c"]
            BoolSet = Set.ofList [true; false]
            IntMap = Map.ofList [("a", 1); ("b", 2); ("c", 3)]
            Tuple = ValueSome(42, "str", true)
            StructTuple = struct(42, "str", true) 
        }


type FSharpClass(stringProperty: string, intProperty : int) =
    member _.StringProperty = stringProperty
    member _.IntProperty = intProperty

[<Struct>]
type FSharpStruct(stringProperty: string, intProperty : int) =
    member _.StringProperty = stringProperty
    member _.IntProperty = intProperty

type GenericFSharpClass<'T>(value: 'T) =
    member _.Value = value

[<Struct>]
type GenericFSharpStruct<'T>(value: 'T) =
    member _.Value = value

type FSharpUnion =
    | A of bar:string * baz:int
    | B
    | C of foo:int

type FSharpSingleCaseUnion = Case of int

type FSharpEnumUnion = A | B | C

type GenericFSharpUnion<'T> =
    | A of 'T
    | B
    | C of foo:int

[<Struct>]
type FSharpStructUnion =
    | A of bar:string * baz:int
    | B
    | C of foo:int

[<Struct>]
type FSharpSingleCaseStructUnion = Case of int

[<Struct>]
type FSharpEnumStructUnion = A | B | C

[<Struct>]
type GenericFSharpStructUnion<'T> =
    | A of 'T
    | B
    | C of foo:int

/// Recursive union encoding the untyped lambda calculus
type FSharpExpr =
    | Var of string
    | App of FSharpExpr * FSharpExpr
    | Lam of string * FSharpExpr
with
    static member True = Lam("x", Lam("y", Var "x"))
    static member False = Lam("x", Lam("y", Var "y"))
    static member Y = 
        Lam("f", App(
            Lam ("x", App(Var "f", App(Var "x", Var "x"))),
            Lam ("x", App(Var "f", App(Var "x", Var "x")))))

[<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
type NullaryUnion =
    | A
    | B of int

module FSharpValues =

    let unit = ()

module FSharpFunctions =

    let simpleFunc : int -> int = (); fun x -> x + 1
    let curriedFunc : int -> int -> int = (); fun x y -> x + y
    let unitAcceptingFunc : unit -> int = (); fun () -> 42
    let curriedUnitAcceptingFunc : unit -> int -> unit -> int = (); fun () x () -> x + 1
    let unitReturningFunc : int -> unit = (); fun x -> ()
    let curriedUnitReturningFunc : int -> int -> unit = (); fun x y -> ()
    let tupleAcceptingFunc : int * int -> int = (); fun (x, y) -> x + y
    let taskFunc : int -> Task<int> = (); fun x -> task { let! _ = Async.Sleep(10) in return x + 1 }
    let curriedTaskFunc : int -> int -> int -> Task<int> = (); fun x y z -> task { let! _ = Async.Sleep(10) in return x + y + z }