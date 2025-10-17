namespace PolyType.Examples.FSharp

open System
open System.IO
open System.Numerics
open System.Collections.Generic
open PolyType
open PolyType.Abstractions
open PolyType.Utilities

/// A delegate that formats a pretty-printed value to a TextWriter.
type PrettyPrinter<'T> = delegate of TextWriter * int * 'T -> unit

/// Provides a pretty printer for .NET types built on top of PolyType.
module PrettyPrinter =
    
    let rec private formatTypeName (t: Type) =
        if t.IsGenericType then
            let paramNames = 
                t.GetGenericArguments() 
                |> Seq.map formatTypeName
                |> String.concat ", "
            sprintf "%s<%s>" (t.Name.Split('`').[0]) paramNames
        else
            t.Name
    
    let private writeLine (writer: TextWriter) (indentation: int) =
        writer.WriteLine()
        writer.Write(String(' ', 2 * indentation))
    
    let private writeStringLiteral (writer: TextWriter) (value: obj) =
        writer.Write('\"')
        writer.Write(value)
        writer.Write('\"')
    
    type private Builder(self: ITypeShapeFunc) =
        inherit TypeShapeVisitor()
        
        let defaultPrinters = 
            dict [
                typeof<bool>, box (PrettyPrinter<bool>(fun writer _ b -> 
                    writer.Write(if b then "true" else "false")))
                
                typeof<byte>, box (PrettyPrinter<byte>(fun writer _ i -> writer.Write(i)))
                typeof<uint16>, box (PrettyPrinter<uint16>(fun writer _ i -> writer.Write(i)))
                typeof<uint32>, box (PrettyPrinter<uint32>(fun writer _ i -> writer.Write(i)))
                typeof<uint64>, box (PrettyPrinter<uint64>(fun writer _ i -> writer.Write(i)))
                
                typeof<sbyte>, box (PrettyPrinter<sbyte>(fun writer _ i -> writer.Write(i)))
                typeof<int16>, box (PrettyPrinter<int16>(fun writer _ i -> writer.Write(i)))
                typeof<int32>, box (PrettyPrinter<int32>(fun writer _ i -> writer.Write(i)))
                typeof<int64>, box (PrettyPrinter<int64>(fun writer _ i -> writer.Write(i)))
                
                typeof<single>, box (PrettyPrinter<single>(fun writer _ i -> writer.Write(i)))
                typeof<double>, box (PrettyPrinter<double>(fun writer _ i -> writer.Write(i)))
                typeof<decimal>, box (PrettyPrinter<decimal>(fun writer _ i -> writer.Write(i)))
                typeof<BigInteger>, box (PrettyPrinter<BigInteger>(fun writer _ i -> writer.Write(i)))
                
                typeof<char>, box (PrettyPrinter<char>(fun writer _ c -> 
                    writer.Write('\'')
                    writer.Write(c)
                    writer.Write('\'')))
                
                typeof<string>, box (PrettyPrinter<string>(fun writer _ s -> 
                    if isNull s then
                        writer.Write("null")
                    else
                        writeStringLiteral writer s))
                
                typeof<DateTime>, box (PrettyPrinter<DateTime>(fun writer _ d -> writeStringLiteral writer d))
                typeof<DateTimeOffset>, box (PrettyPrinter<DateTimeOffset>(fun writer _ d -> writeStringLiteral writer d))
                typeof<TimeSpan>, box (PrettyPrinter<TimeSpan>(fun writer _ t -> writeStringLiteral writer t))
                typeof<Guid>, box (PrettyPrinter<Guid>(fun writer _ g -> writeStringLiteral writer g))
            ]
        
        member this.GetOrAddPrettyPrinter<'T>(typeShape: ITypeShape<'T>) : PrettyPrinter<'T> =
            self.Invoke(typeShape, null) :?> PrettyPrinter<'T>
        
        interface ITypeShapeFunc with
            member this.Invoke<'T>(typeShape: ITypeShape<'T>, _state: obj) : obj =
                match defaultPrinters.TryGetValue(typeShape.Type) with
                | true, printer -> printer
                | false, _ -> typeShape.Accept(this)
        
        override this.VisitObject<'T>(objectShape: IObjectTypeShape<'T>, _state: obj) : obj =
            let typeName = formatTypeName typeof<'T>
            let propertyPrinters = 
                objectShape.Properties
                |> Seq.filter (fun prop -> prop.HasGetter)
                |> Seq.map (fun prop -> prop.Accept(this))
                |> Seq.choose (fun x -> if isNull x then None else Some (x :?> PrettyPrinter<'T>))
                |> Seq.toArray
            
            box (PrettyPrinter<'T>(fun writer indentation value ->
                if box value |> isNull then
                    writer.Write("null")
                else
                    writer.Write("new ")
                    writer.Write(typeName)
                    
                    if propertyPrinters.Length = 0 then
                        writer.Write("()")
                    else
                        writeLine writer indentation
                        writer.Write('{')
                        for i in 0 .. propertyPrinters.Length - 1 do
                            writeLine writer (indentation + 1)
                            propertyPrinters.[i].Invoke(writer, indentation + 1, value)
                            if i < propertyPrinters.Length - 1 then
                                writer.Write(',')
                        writeLine writer indentation
                        writer.Write('}')))
        
        override this.VisitProperty<'TDeclaringType, 'TPropertyType>(property: IPropertyShape<'TDeclaringType, 'TPropertyType>, _state: obj) : obj =
            let getter = property.GetGetter()
            let propertyTypePrinter = this.GetOrAddPrettyPrinter(property.PropertyType)
            box (PrettyPrinter<'TDeclaringType>(fun writer indentation obj ->
                writer.Write(property.Name)
                writer.Write(" = ")
                let mutable objRef = obj
                propertyTypePrinter.Invoke(writer, indentation, getter.Invoke(&objRef))))
        
        override this.VisitEnumerable<'TEnumerable, 'TElement>(enumerableShape: IEnumerableTypeShape<'TEnumerable, 'TElement>, _state: obj) : obj =
            let enumerableGetter = enumerableShape.GetGetEnumerable()
            let elementPrinter = this.GetOrAddPrettyPrinter(enumerableShape.ElementType)
            let valuesArePrimitives = defaultPrinters.ContainsKey(typeof<'TElement>)
            
            box (PrettyPrinter<'TEnumerable>(fun writer indentation value ->
                if box value |> isNull then
                    writer.Write("null")
                else
                    writer.Write('[')
                    
                    let mutable containsElements = false
                    if valuesArePrimitives then
                        for element in enumerableGetter.Invoke(value) do
                            if containsElements then
                                writer.Write(", ")
                            elementPrinter.Invoke(writer, indentation, element)
                            containsElements <- true
                    else
                        for element in enumerableGetter.Invoke(value) do
                            if containsElements then
                                writer.Write(',')
                            writeLine writer (indentation + 1)
                            elementPrinter.Invoke(writer, indentation + 1, element)
                            containsElements <- true
                        writeLine writer indentation
                    
                    writer.Write(']')))
        
        override this.VisitDictionary<'TDictionary, 'TKey, 'TValue>(dictionaryShape: IDictionaryTypeShape<'TDictionary, 'TKey, 'TValue>, _state: obj) : obj =
            let typeName = formatTypeName typeof<'TDictionary>
            let dictionaryGetter = dictionaryShape.GetGetDictionary()
            let keyPrinter = this.GetOrAddPrettyPrinter(dictionaryShape.KeyType)
            let valuePrinter = this.GetOrAddPrettyPrinter(dictionaryShape.ValueType)
            
            box (PrettyPrinter<'TDictionary>(fun writer indentation value ->
                if box value |> isNull then
                    writer.Write("null")
                else
                    writer.Write("new ")
                    writer.Write(typeName)
                    
                    let dictionary = dictionaryGetter.Invoke(value)
                    
                    if dictionary.Count = 0 then
                        writer.Write("()")
                    else
                        writeLine writer indentation
                        writer.Write('{')
                        let mutable first = true
                        for kvp in dictionary do
                            if not first then
                                writer.Write(',')
                            writeLine writer (indentation + 1)
                            writer.Write('[')
                            keyPrinter.Invoke(writer, indentation + 1, kvp.Key)
                            writer.Write("] = ")
                            valuePrinter.Invoke(writer, indentation + 1, kvp.Value)
                            first <- false
                        writeLine writer indentation
                        writer.Write('}')))
        
        // Note: VisitEnum is not overridden due to F# compiler constraints issues with Enum types
        // The base implementation from TypeShapeVisitor will be used instead
        
        override this.VisitOptional<'TOptional, 'TElement>(optionalShape: IOptionalTypeShape<'TOptional, 'TElement>, _state: obj) : obj =
            let elementPrinter = this.GetOrAddPrettyPrinter(optionalShape.ElementType)
            let deconstructor = optionalShape.GetDeconstructor()
            box (PrettyPrinter<'TOptional>(fun writer indentation value ->
                let mutable element = Unchecked.defaultof<'TElement>
                if not (deconstructor.Invoke(value, &element)) then
                    writer.Write("null")
                else
                    elementPrinter.Invoke(writer, indentation, element)))
        
        override this.VisitSurrogate<'T, 'TSurrogate>(surrogateShape: ISurrogateTypeShape<'T, 'TSurrogate>, _state: obj) : obj =
            let surrogatePrinter = this.GetOrAddPrettyPrinter(surrogateShape.SurrogateType)
            let marshaler = surrogateShape.Marshaler
            box (PrettyPrinter<'T>(fun writer indentation t ->
                surrogatePrinter.Invoke(writer, indentation, marshaler.Marshal(t))))
        
        override this.VisitUnion<'TUnion>(unionShape: IUnionTypeShape<'TUnion>, _state: obj) : obj =
            let getUnionCaseIndex = unionShape.GetGetUnionCaseIndex()
            let baseCasePrinter = unionShape.BaseType.Accept(this) :?> PrettyPrinter<'TUnion>
            let unionCasePrinters = 
                unionShape.UnionCases
                |> Seq.map (fun unionCase -> unionCase.Accept(this) :?> PrettyPrinter<'TUnion>)
                |> Seq.toArray
            
            box (PrettyPrinter<'TUnion>(fun writer indentation value ->
                if box value |> isNull then
                    writer.Write("null")
                else
                    let mutable valueRef = value
                    let index = getUnionCaseIndex.Invoke(&valueRef)
                    let derivedPrinter = if index < 0 then baseCasePrinter else unionCasePrinters.[index]
                    derivedPrinter.Invoke(writer, indentation, value)))
        
        override this.VisitUnionCase<'TUnionCase, 'TUnion>(unionCaseShape: IUnionCaseShape<'TUnionCase, 'TUnion>, _state: obj) : obj =
            let underlying = unionCaseShape.UnionCaseType.Accept(this) :?> PrettyPrinter<'TUnionCase>
            let marshaler = unionCaseShape.Marshaler
            box (PrettyPrinter<'TUnion>(fun writer indentation value ->
                underlying.Invoke(writer, indentation, marshaler.Unmarshal(value))))
    
    type private DelayedPrettyPrinterFactory() =
        interface IDelayedValueFactory with
            member this.Create<'T>(_typeShape: ITypeShape<'T>) : DelayedValue =
                DelayedValue<PrettyPrinter<'T>>(Func<_, _>(fun (self: DelayedValue<PrettyPrinter<'T>>) -> 
                    PrettyPrinter<'T>(fun writer indentation value -> 
                        self.Result.Invoke(writer, indentation, value))))
    
    let private cache = 
        MultiProviderTypeCache(
            DelayedValueFactory = DelayedPrettyPrinterFactory(),
            ValueBuilderFactory = Func<_, _>(fun ctx -> Builder(ctx) :> ITypeShapeFunc))
    
    /// Builds a PrettyPrinter instance from the specified shape.
    let create<'T> (shape: ITypeShape<'T>) : PrettyPrinter<'T> =
        cache.GetOrAdd(shape) :?> PrettyPrinter<'T>
    
    /// Builds a PrettyPrinter instance from the specified shape provider.
    let createFromProvider<'T> (provider: ITypeShapeProvider) : PrettyPrinter<'T> =
        cache.GetOrAdd(typeof<'T>, provider) :?> PrettyPrinter<'T>
    
    /// Pretty prints the specified value to a string.
    let print<'T> (prettyPrinter: PrettyPrinter<'T>) (value: 'T) : string =
        use writer = new StringWriter()
        prettyPrinter.Invoke(writer, 0, value)
        writer.ToString()
