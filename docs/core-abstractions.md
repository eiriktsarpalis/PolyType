# Core Abstractions

This document provides a walkthrough of the core type abstractions found in PolyType. This includes `ITypeShape`, `IPropertyShape` and the visitor types for accessing them. These are typically consumed by library authors looking to build datatype-generic components. Unless otherwise stated, all APIs are found in the `PolyType.Abstractions` namespace.

## The `ITypeShape` interface

The `ITypeShape` interface defines a reflection-like representation for a given .NET type. The type hierarchy that it creates encapsulates all information necessary to perform strongly typed traversal of its type graph.

To illustrate the idea, consider the following APIs modelling objects with properties:

```C#
namespace PolyType.Abstractions;

public partial interface IObjectTypeShape<TDeclaringType> : ITypeShape
{
    IReadOnlyList<IPropertyShape> Properties { get; }
}

public partial interface IPropertyShape<TDeclaringType, TPropertyType> : IPropertyShape
{
    ITypeShape<TPropertyType> PropertyType { get; }
    Func<TDeclaringType, TPropertyType> GetGetter();
    bool HasGetter { get; }
}
```

This model is fairly similar to `System.Type` and `System.Reflection.PropertyInfo`, with the notable difference that both models are generic and the property shape is capable of producing a strongly typed getter delegate. It can be traversed using the following generic visitor type:

```C#
public abstract partial class TypeShapeVisitor
{
    object? VisitObject<TDeclaringType>(IObjectTypeShape<TDeclaringType> objectShape, object? state = null);
    object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> typeShape, object? state = null);
}

public partial interface ITypeShape
{
    object? Accept(TypeShapeVisitor visitor, object? state = null);
}

public partial interface IPropertyShape
{
    object? Accept(TypeShapeVisitor visitor, object? state = null);
}
```

Here's a simple visitor used to construct delegates counting the number nodes in an object graph:

```C#
partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? _)
    {
        // Generate counter delegates for each individual property or field:
        Func<T, int>[] propertyCounters = objectShape.Properties
            .Where(prop => prop.HasGetter)
            .Select(prop => (Func<T, int>)prop.Accept(this)!)
            .ToArray();

        // Compose into a counter delegate for the current type.
        return new Func<T?, int>(value =>
        {
            if (value is null)
                return 0;

            int count = 1;
            foreach (Func<T, int> propertyCounter in propertyCounters)
                count += propertyCounter(value);

            return count;
        });
    }

    public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? _)
    {
        Func<TDeclaringType, TPropertyType> getter = propertyShape.GetGetter(); // extract the getter delegate
        var propertyTypeCounter = (Func<TPropertyType, int>)propertyShape.PropertyType.Accept(this)!; // extract the counter for the property shape
        return new Func<TDeclaringType, int>(obj => propertyTypeCounter(getter(obj))); // combine into a property-specific counter delegate
    }
}
```

Given an `ITypeShape<T>` instance we can now construct a counter delegate like so:

```C#
ITypeShape<MyPoco> shape = provider.GetTypeShape<MyPoco>();
CounterVisitor visitor = new();
var pocoCounter = (Func<MyPoco, int>)shape.Accept(visitor)!;

pocoCounter(new MyPoco("x", "y")); // 3
pocoCounter(new MyPoco("x", null)); // 2
pocoCounter(new MyPoco(null, null)); // 1
pocoCounter(null); // 0

record MyPoco(string? x, string? y);
```

It should be noted that the visitor is only used when constructing, or _folding_ the counter delegate but not when the delegate itself is being invoked. At the same time, traversing the type graph via the visitor requires casting of the intermediate delegates, however the traversal of the object graph via the resultant delegate is fully type-safe and doesn't require any casting.

> [!NOTE]
> In technical terms, `ITypeShape` encodes a [GADT representation](https://en.wikipedia.org/wiki/Generalized_algebraic_data_type) over .NET types and `TypeShapeVisitor` encodes a pattern match over the GADT. This technique was originally described in [this publication](https://www.microsoft.com/research/publication/generalized-algebraic-data-types-and-object-oriented-programming/).
>
> The casting requirement for visitors is a known restriction of this approach, and possible extensions to the C# type system that allow type-safe pattern matching on GADTs are discussed in the paper.

### Collection type shapes

A collection type in this context refers to any type implementing `IEnumerable`, and this is further refined into enumerable and dictionary shapes:

```C#
public interface IEnumerableTypeShape<TEnumerable, TElement> : ITypeShape<TEnumerable>
{
    ITypeShape<TElement> ElementType { get; }

    Func<TEnumerable, IEnumerable<TElement>> GetGetEnumerable();
}

public interface IDictionaryTypeShape<TDictionary, TKey, TValue> : ITypeShape<TDictionary>
{
    ITypeShape<TKey> KeyType { get; }
    ITypeShape<TValue> ValueType { get; }

    Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> GetGetDictionary();
}
```

A collection type is classed as a dictionary if it implements one of the known dictionary interfaces. Non-generic collections use `object` as the element, key and value types. As before, enumerable shapes can be unpacked by the relevant methods of `TypeShapeVisitor`:

```C#
public abstract partial class TypeShapeVisitor
{
    object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null);
    object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null);
}
```

Using the above we can now extend `CounterVisitor` so that collection types are supported:

```C#
partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? _)
    {
        var elementCounter = (Func<TElement, int>)enumerableShape.ElementType.Accept(this)!;
        Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();
        return new Func<TEnumerable, int>(enumerable =>
        {
            if (enumerable is null) return 0;
            
            int count = 0;
            foreach (TElement element in getEnumerable(enumerable))
                count += elementCounter(element);

            return count;
        });
    }

    public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? _)
    {
        var keyCounter = (Func<TKey, int>)dictionaryShape.KeyType.Accept(this);
        var valueCounter = (Func<TValue, int>)dictionaryShape.ValueType.Accept(this);
        Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary = dictionaryShape.GetGetDictionary();
        return new Func<TDictionary, int>(dictionary =>
        {
            if (dictionary is null) return 0;
            
            int count = 0;
            foreach (var kvp in getDictionary(dictionary))
            {
                count += keyCounter(kvp.Key);
                count += valueCounter(kvp.Value);
            }

            return count;
        });
    }
}
```

### Enum types

Enum types are classed as a special type shape:

```C#
public interface IEnumTypeShape<TEnum, TUnderlying> : ITypeShape<TEnum> where TEnum : struct, Enum
{
    public ITypeShape<TUnderlying> UnderlyingType { get; }
}
```

The `TUnderlying` represents the underlying numeric representation used by the enum in question. As before, `TypeShapeVisitor` exposes relevant methods for consuming the new shapes:

```C#
public abstract partial class TypeShapeVisitor
{
    object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null) where TEnum : struct, Enum;
}
```

Like before we can extend `CounterVisitor` to enum types like so:

```C#
partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> _, object? _)
    {
        return new Func<TEnum, int>(_ => 1);
    }
}
```

### Optional types

An optional type is any container type encapsulating zero or one values of a given type. The most common example is `System.Nullable<T>` but it also includes the [F# option types](https://learn.microsoft.com/dotnet/fsharp/language-reference/options). It does not include nullable reference types since they constitute a compile-time annotation as opposed to being a real .NET type. Optional types map to the following shape:

```C#
public interface IOptionalTypeShape<TOptional, TElement> : ITypeShape<TOptional>
{
    // The shape of the value encapsulated by the optional type.
    ITypeShape<TElement> ElementType { get; }

    // Constructor delegates for the empty and populated cases.
    Func<TOptional> GetNoneConstructor();
    Func<TElement, TOptional> GetSomeConstructor();

    // Deconstructor delegate for optional values.
    OptionDeconstructor<TOptional, TElement> GetDeconstructor();
}

public delegate bool OptionDeconstructor<TOptional, TElement>(TOptional optional, out TElement value);
```

In the case of `Nullable<T>`, the type `int?` maps to an optional shape with `TOptional` set to `int?` and `TElement` set to `int`. The relevant `TypeShapeVisitor` method looks as follows:

```C#
public abstract partial class TypeShapeVisitor
{
    object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null);
}
```

We can extend `CounterVisitor` to optional types like so:

```C#
partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? _)
    {
        var elementCounter = (Func<TElement, int>)optionalShape.ElementType.Accept(this);
        var deconstructor = optionalShape.GetDeconstructor();
        return new Func<TOptional, int>(optional => deconstructor(optional, out TElement element) ? elementCounter(element) : 0);
    }
}
```

### Union types

PolyType supports union types through the `IUnionTypeShape` abstraction. Currently two kinds of union types are supported:

1. Polymorphic class or interface hierarchies declared via `DerivedTypeShape` attribute annotations and
2. F# [discriminated union](https://learn.microsoft.com/dotnet/fsharp/language-reference/discriminated-unions) types.

The shape abstraction for union types looks as follows:

```C#
public interface IUnionTypeShape<TUnion> : ITypeShape<TUnion>
{
    // The list of all registered union cases and their shapes.
    IReadOnlyList<IUnionCaseShape> UnionCases { get; }

    // The underlying shape for the base type, used as the fallback case.
    ITypeShape<TUnion> BaseType { get; }

    // Gets a delegate used to compute the union case index for a given value, or -1 if none is found.
    Func<TUnion, int> GetGetUnionCaseIndex();
}

public interface IUnionCaseShape<TUnionCase, TUnion> : IUnionCaseShape
    where TUnionCase : TUnion
{
    // A unique string identifier for the union case.
    string Name { get; }

    // A unique integer identifier for the union case.
    int Tag { get; }

    // The underlying shape for the current union case.
    ITypeShape<TUnionCase> Type { get; }
}
```

And as before, `TypeShapeVisitor` exposes relevant methods for the two types:

```C#
public abstract partial class TypeShapeVisitor
{
    object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null);
    object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null)
        where TUnionCase : TUnion;
}
```

Putting it all together, here's how we can extend our counter example to support union types:

```C#
partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? _)
    {
        var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
        var baseTypeCounter = (Func<TUnion, int>)unionShape.BaseType.Accept(this);
        var unionCaseCounters = unionShape.UnionCases
            .Select(unionCase => (Func<TUnion, int>)unionCase.Accept(this))
            .ToArray();

        return new Func<TUnion, int>(union =>
        {
            int index = getUnionCaseIndex(union);
            Func<TUnion, int> counter = index < 0 ? baseTypeCounter : unionCaseCounters[index];
            return counter(union);
        });
    }

    public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? _)
    {
        var caseCounter = (Func<TUnionCase, int>)unionCaseShape.Type.Accept(this)!;
        return new Func<TUnion, int>(union => caseCounter((TUnionCase)union!));
    }
}
```

### Surrogate types

PolyType lets users customize the shape of a given type by marshaling its data to a surrogate type. This is done by declaring an implementation of the `IMarshaler<T, TSurrogate>` interface on the type, which defines a bidirectional mapping between the instances of the type itself and the surrogate. Such types are mapped to the following abstraction:

```C#
public interface ISurrogateTypeShape<T, TSurrogate> : ITypeShape<T>
{
    // The shape of the surrogate type
    ITypeShape<TSurrogate> SurrogateType { get; }
    
    // The bidirectional mapping between T and TSurrogate
    IMarshaler<T, TSurrogate> Marshaler { get; }
}

public interface IMarshaler<T, TSurrogate>
{
    TSurrogate? Marshal(T? value);
    T? Unmarshal(TSurrogate? value);
}
```

And corresponding visitor method

```C#
public abstract partial class TypeShapeVisitor
{
    object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null);
}
```

We can extend the counter example to surrogate types as follows:

```C#
partial class CounterVisitor : TypeShapeVisitor
{
    public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? _)
    {
        var surrogateCounter = (Func<TSurrogate, int>)surrogateShape.SurrogateType.Accept(this)!;
        var marshaler = surrogateShape.Marshaler;
        return new Func<T, int>(t => surrogateCounter(marshaler.Marshal(t)));
    }
}
```

### Function types

PolyType models delegate types as well as F# function types using the `IFunctionTypeShape` interface:

```C#
public partial interface IFunctionTypeShape<TFunction, TArguments, TResult> : ITypeShape<TFunction>
{
    ITypeShape<TResult> ReturnType { get; }
    IReadOnlyList<IParameterShape> Parameters { get; }
}
```

Visitors accessing function shapes can be used to invoke instances of type `TFunction` or to create new instances of type `TFunction` by wrapping a generic `Func<TArgumentState, TResult>` delegate. For more information on how function or method shapes work, please refer to the [method shapes](#method-shapes) section below.


To recap, the `ITypeShape` model splits .NET types into seven separate kinds:

* `IObjectTypeShape` instances which may or may not define properties,
* `IEnumerableTypeShape` instances describing enumerable types,
* `IDictionaryTypeShape` instances describing dictionary types,
* `IEnumTypeShape` instances describing enum types,
* `IOptionalTypeShape` instances describing optional types such as `Nullable<T>` or F# option types,
* `IUnionTypeShape` instances describing union types such as polymorphic type hierarchies or F# discriminated unions, and
* `ISurrogateTypeShape` instances that delegate their shape declaration to surrogate types.
* `IFunctionTypeShape` instances describing delegate types, F# function types, or other single-method interfaces.

## Constructing and mutating types

The APIs described so far facilitate algorithms that perform object traversal such as serializers, formatters and validators. They do not suffice when it comes to writing algorithms that perform object construction or mutation such as deserializers, mappers and random value generators. This section describes the constructs used for writing this class of algorithms.

### Property setters

The `IPropertyShape` interface exposes strongly typed setter delegates:

```C#
public interface IPropertyShape<TDeclaringType, TPropertyType>
{
    Setter<TDeclaringType, TPropertyType> GetSetter();
    bool HasSetter { get; }
}

public delegate void Setter<TDeclaringType, TPropertyType>(ref TDeclaringType obj, TPropertyType value);
```

The setter is defined using a special delegate that accepts the declaring type by reference, ensuring that it has the expected behavior when working with value types. To illustrate how this works, here is a toy example that sets all properties to their default value:

```C#
public delegate void Mutator<T>(ref T obj);

class MutatorVisitor : TypeShapeVisitor
{
    public override object? VisitObject(IObjectTypeShape<T> objectShape, object? _)
    {
        Mutator<T>[] propertyMutators = objectShape.Properties
            .Where(prop => prop.HasSetter)
            .Select(prop => (Mutator<T>)prop.Accept(this)!)
            .ToArray();

        return new Mutator<T>(ref T value => foreach (var mutator in propertyMutators) mutator(ref value));
    }

    public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? _)
    {
        Setter<TDeclaringType, TPropertyType> setter = propertyShape.GetSetter();
        return new Mutator<TDeclaringType>(ref TDeclaringType obj => setter(ref obj, default(TPropertyType)!));
    }
}
```

which can be consumed as follows:

```C#
ITypeShape<MyPoco> shape = provider.GetTypeShape<MyPoco>();
MutatorVisitor visitor = new();
var mutator = (Mutator<MyPoco>)shape.Accept(visitor)!;

var value = new MyPoco { X = "X" };
mutator(ref value);
Console.WriteLine(value); // MyPoco { X =  }

struct MyPoco
{
    public string? X { get; set; }
}
```

### Constructor shapes

While property setters should suffice when mutating existing objects, constructing a new instance from scratch is somewhat more complicated, particularly for types that only expose parameterized constructors or are immutable. PolyType models constructors using the `IConstructorShape` abstraction which can be obtained as follows:

```C#
public partial interface IObjectTypeShape<T>
{
    IConstructorShape? Constructor { get; }
}

public partial interface IConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape
{
    Func<TArgumentState> GetArgumentStateConstructor();
    Func<TArgumentState, TDeclaringType> GetParameterizedConstructor();
}

public abstract partial class TypeShapeVisitor
{
    object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> shape, object? state = null);
}
```

The constructor shape specifies two type parameters: `TDeclaringType` represents the declaring type of the constructor while `TArgumentState` represents an opaque, mutable token that encapsulates all parameters that will be passed to constructor. The choice of `TArgumentState` is up to the particular type shape provider implementation, but typically a value tuple is used:

```C#
record MyPoco(int x = 42, string y);

class MyPocoConstructorShape : IConstructorShape<MyPoco, (int, string)>
{
    public Func<(int, string)> GetArgumentStateConstructor() => () => (42, null!);
    public Func<(int, string), MyPoco> GetParameterizedConstructor() => state => new MyPoco(state.Item1, state.Item2);
}
```

The two delegates define the means for creating a default instance of the mutable state token and constructing an instance of the declaring type from a populated token, respectively. Separately, there needs to be a mechanism for populating the state token which is achieved using the `IParameterShape` interface:

```C#
public partial interface IConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape
{
    IReadOnlyList<IParameterShape> Parameters { get; }
}

public partial interface IParameterShape<TArgumentState, TParameterType> : IParameterShape
{
    ITypeShape<TParameterType> ParameterType { get; }
    Setter<TArgumentState, TParameterType> GetSetter();
}

public abstract partial class TypeShapeVisitor
{
    object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> shape, object? state = null);
}
```

Which exposes strongly typed setters for each of the constructor parameters. Putting it all together, here is toy implementation of a visitor that recursively constructs an object graph using constructor shapes:

```C#
class EmptyConstructorVisitor : TypeShapeVisitor
{
    private delegate void ParameterSetter<T>(ref T object);

    public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? _)
    {
        return objectShape.Constructor is null
            ? new Func<T>(() => default) // Just return the default if no ctor is found
            : objectShape.Constructor.Accept(this);
    }

    public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? _)
    {
        Func<TArgumentState> argumentStateCtor = constructorShape.GetArgumentStateConstructor();
        Func<TArgumentState, TDeclaringType> ctor = constructorShape.GetParameterizedConstructor();
        ParameterSetter<TArgumentState>[] parameterSetters = constructorShape.Parameters
            .Select(param => (ParameterSetter<TArgumentState>)param.Accept(this)!)
            .ToArray();

        return new Func<TDeclaringType>(() =>
        {
            TArgumentState state = argumentStateCtor();
            foreach (ParameterSetter<TArgumentState> parameterSetter in parameterSetters)
                parameterSetter(ref state);

            return ctor(state);
        });
    }

    public override object? VisitParameter<TArgumentState, TParameter>(IParameterShape<TArgumentState, TParameter> parameter, object? _)
    {
        var parameterFactory = (Func<TParameter>)parameter.ParameterType.Accept(this);
        Setter<TArgumentState, TParameter> setter = parameter.GetSetter();
        return new ParameterSetter<TArgumentState>(ref TArgumentState state => setter(ref state, parameterFactory()));
    }
}
```

We can now use the visitor to construct an empty instance factory:

```C#
ITypeShape<MyPoco> shape = provider.GetTypeShape<MyPoco>();
EmptyConstructorVisitor visitor = new();
var factory = (Func<MyPoco>)shape.Accept(visitor)!;

MyPoco value = factory();
Console.WriteLine(value); // MyPoco { x = , y = 0 }

record MyPoco(int x, string y);
```

### Constructing collections

Collection types are constructed somewhat differently compared to regular POCOs, using one of the following strategies:

* The collection is mutable and can be populated following the conventions of [C# collection initializers](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers).
* The collection can be constructed from a span of entries. Types declaring factories via the `CollectionBuilderAttribute` or immutable collections map to this strategy.
* The collection type is not constructible.

These strategies are surfaced via the `CollectionConstructionStrategy` enum:

```C#
[Flags]
public enum CollectionConstructionStrategy
{
    None = 0,
    Mutable = 1,
    Parameterized = 2
}
```

Which is exposed in the relevant shape types as follows:

```C#
public partial interface IEnumerableTypeShape<TEnumerable, TElement>
{
    CollectionConstructionStrategy ConstructionStrategy { get; }

    // Implemented by CollectionConstructionStrategy.Mutable types
    MutableCollectionConstructor<TKey, TEnumerable> GetDefaultConstructor();
    EnumerableAppender<TEnumerable, TElement> GetAppender();

    // Implemented by CollectionConstructionStrategy.Parameterized types
    ParameterizedCollectionConstructor<TKey, TElement, TEnumerable> GetParameterizedConstructor();
}

public delegate TEnumerable MutableCollectionConstructor<TKey, TEnumerable>(in CollectionConstructionOptions<TKey> options = default)
public delegate TEnumerable ParameterizedCollectionConstructor<TKey, TElement, TDeclaringType>(ReadOnlySpan<TElement> values, in CollectionConstructionOptions<TKey> options = default);
```

Putting it all together, we can extend `EmptyConstructorVisitor` to collection types like so:

```C#
class EmptyConstructorVisitor : TypeShapeVisitor
{
    public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> typeShape, object? _)
    {
        const int size = 10;
        var elementFactory = (Func<TElement>)typeShape.ElementType.Accept(this);
        switch (typeShape.ConstructionStrategy)
        {
            case CollectionConstructionStrategy.Mutable:
                var defaultCtor = typeShape.GetDefaultConstructor();
                var appender = typeShape.GetAppender();
                return new Func<TEnumerable>(() =>
                {
                    TEnumerable value = defaultCtor();
                    for (int i = 0; i < size; i++) appender(ref value, elementFactory());
                    return value;
                });

            case CollectionConstructionStrategy.Parameterized:
                var parameterizedCtor = typeShape.GetParameterizedConstructor();
                return new Func<TEnumerable>(() =>
                {
                    var buffer = new TElement[size];
                    for (int i = 0; i < size; i++) buffer[i] = elementFactory();
                    return parameterizedCtor(buffer);
                });

            default:
                // No constructor, just return the default.
                return new Func<TEnumerable>(() => default!);
        }
    }
}
```

The constructor-returning methods on the enumerable and dictionary shapes take an optional <xref:PolyType.Abstractions.CollectionConstructionOptions`1> value.
This struct can specify an @System.Collections.Generic.IEqualityComparer`1 or @System.Collections.Generic.IComparer`1 to be provided when constructing the collection to override the default comparer.
This can be useful for keyed collections (i.e. sorted or hashed) when you're performing a structural copy or deserializing untrusted data and need to use a secure hash algorithm.

## Method shapes

PolyType exposes an `IMethodShape` abstraction that provides strongly typed representations of .NET methods. This enables automatic shape generation for all parameter and return types in a method signature, making it easy to implement RPC-like libraries and other method invocation scenarios. Method shapes are accessible via the `Methods` property on `ITypeShape`:

```C#
public partial interface ITypeShape
{
    IReadOnlyList<IMethodShape> Methods { get; }
}
```

The core `IMethodShape` interface looks as follows:

```C#
public partial interface IMethodShape<TDeclaringType, TArgumentState, TResult> : IMethodShape
    where TArgumentState : IArgumentState
{
    ITypeShape<TResult> ReturnType { get; }
    IReadOnlyList<IParameterShape> Parameters { get; }
    
    Func<TArgumentState> GetArgumentStateConstructor();
    MethodInvoker<TDeclaringType, TArgumentState, TResult> GetMethodInvoker();
}

public delegate ValueTask<TResult> MethodInvoker<TDeclaringType, TArgumentState, TResult>(
    ref TDeclaringType? instance, 
    ref TArgumentState argumentState);
```

Similar to constructor shapes, method shapes use an opaque `TArgumentState` type to encapsulate method parameters, and expose strongly typed setters via the `IParameterShape` abstraction. The visitor pattern supports method shapes:

```C#
public abstract partial class TypeShapeVisitor
{
    object? VisitMethod<TDeclaringType, TArgumentState, TResult>(IMethodShape<TDeclaringType, TArgumentState, TResult> methodShape, object? state = null);
}
```

Here's a simple example that creates a weakly typed logging wrapper around method calls:

```C#
partial class LoggingVisitor : TypeShapeVisitor
{
    public override object? VisitMethod<TDeclaringType, TArgumentState, TResult>(
        IMethodShape<TDeclaringType, TArgumentState, TResult> methodShape, object? state)
    {
        StrongBox<TDeclaringType?> instance = new(methodShape.IsStatic ? default : (TDeclaringType)state!);
        var parameterSetters = methodShape.Parameters
            .Select(param => (Setter<TArgumentState, IReadOnlyDictionary<string, object?>>)param.Accept(this, null)!)
            .ToArray();

        var argumentStateCtor = methodShape.GetArgumentStateConstructor();
        var invoker = methodShape.GetMethodInvoker();

        return new Func<IReadOnlyDictionary<string, object?>, ValueTask<object?>>(async arguments =>
        {
            Console.WriteLine($"Invoking {methodShape.Name}");
            TArgumentState argumentState = argumentStateCtor();
            foreach (var parameterSetter in parameterSetters)
            {
                parameterSetter(ref argumentState, arguments);
            }

            TResult result = await invoker(ref instance.Value, ref argumentState);
            Console.WriteLine($"Completed {methodShape.Name} with result {result}");
            return result;
        });
    }

    public override object? VisitParameter<TArgumentState, TParameterType>(
        IParameterShape<TArgumentState, TParameterType> parameterShape, object? state)
    {
        Setter<TArgumentState, TParameterType> setter = parameterShape.GetSetter();
        return new Setter<TArgumentState, IReadOnlyDictionary<string, object?>>((ref TArgumentState argumentState, IReadOnlyDictionary<string, object?> parameters) =>
        {
            if (parameters.TryGetValue(parameterShape.Name, out object? value))
            {
                setter(ref argumentState, (TParameterType)value!);
            }
        });
    }
}
```

This logging wrapper can be applied to any method shape:

```C#
ITypeShape<Calculator> shape = TypeShapeResolver.Resolve<Calculator>();
IMethodShape addMethodShape = shape.Methods.First(m => m.Name == "Add");
var addMethod = (Func<IReadOnlyDictionary<string, object?>, ValueTask<object?>>)addMethodShape.Accept(new LoggingVisitor(), new Calculator())!;
await addMethod(new Dictionary<string, object?> { { "x", 2 }, { "y", 3 } });
// Invoking Add
// Completed Add with result 5

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
partial class Calculator
{
    public static int Add(int x, int y) => x + y;
    public async Task<int> MultiplyAsync(int x, int y)
    {
        await Task.Delay(100);
        return x * y;
    }
}
```

### Function type shapes

The `IFunctionTypeShape` abstraction has many similarities with the `IMethodShape` abstraction, exposing the same facilities enabling generic function invocation:

```C#
public partial interface IFunctionTypeShape<TFunction, TArgumentState, TResult> : ITypeShape<TFunction>
    where TArgumentState : IArgumentState
{
    ITypeShape<TResult> ReturnType { get; }
    IReadOnlyList<IParameterShape> Parameters { get; }
    
    Func<TArgumentState> GetArgumentStateConstructor();
    MethodInvoker<TFunction, TArgumentState, TResult> GetMethodInvoker();
}
```

Additionally, the abstraction exposes facilities for creating `TFunction` instances by wrapping generic delegates:

```C#
public partial interface IFunctionTypeShape<TFunction, TArgumentState, TResult>
{
    bool IsAsync { get; }
    TFunction FromDelegate(RefFunc<TArgumentState, TResult> innerFunc);
    TFunction FromAsyncDelegate(RefFunc<TArgumentState, ValueTask<TResult>> innerFunc);
}
```

#### Example: building a decorated delegate

In some cases you want to return a strongly typed delegate of the same shape after adding cross-cutting behavior:

```C#
partial class DecoratorVisitor : TypeShapeVisitor
{
    public override object? VisitFunction<TFunction, TArgumentState, TResult>(
        IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state)
    {
        var invoker = functionShape.GetFunctionInvoker();
        if (!functionShape.IsAsync)
        {
            return new Func<TFunction, TFunction>(inner => functionShape.FromDelegate((ref TArgumentState arg) =>
            {
                Console.WriteLine($"Before {functionShape.Type.Name}");
                ValueTask<TResult> resultTask = invoker(ref inner, ref arg);
                Debug.Assert(resultTask.IsCompleted, "The underlying function is synchronous.");
                Console.WriteLine($"After {functionShape.Type.Name}");
                return resultTask.Result;
            }));
        }

        return new Func<TFunction, TFunction>(inner => functionShape.FromAsyncDelegate((ref TArgumentState arg) =>
        {
            Console.WriteLine($"Before {functionShape.Type.Name}");
            var resultTask = invoker(ref inner, ref arg);
            return Complete(resultTask);
            async ValueTask<TResult> Complete(ValueTask<TResult> resultTask)
            {
                TResult result = await resultTask.ConfigureAwait(false);
                Console.WriteLine($"After {functionShape.Type.Name}");
                return result;
            }
        }));
    }
}
```

Which can be used to decorate any delegate type:

```C#
// Usage
ITypeShape<Adder> addShape = ...;
var decorator = (Func<Adder, Adder>)addShape.Accept(new DecoratorVisitor())!;
Adder decorated = decorator((x, y) => x + y);
int sum = decorated(1, 2); // Writes logs, returns 3

delegate int Adder(int x, int y);
```

This concludes the tutorial for the core PolyType programming model. For more detailed examples, please refer to the [`PolyType.Examples`](https://github.com/eiriktsarpalis/PolyType/tree/main/src/PolyType.Examples) project folder.
