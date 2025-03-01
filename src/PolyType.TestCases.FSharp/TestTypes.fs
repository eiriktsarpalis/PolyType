namespace PolyType.Tests.FSharp

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