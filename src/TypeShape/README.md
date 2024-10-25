# typeshape-csharp

Contains a proof-of-concept port of the [TypeShape](https://github.com/eiriktsarpalis/TypeShape) library, adapted to patterns and idioms available in C#. The library provides a type model that facilitates development of high-performance datatype-generic components such as serializers, loggers, transformers and validators. At its core, the programming model employs a [variation on the visitor pattern](https://www.microsoft.com/research/publication/generalized-algebraic-data-types-and-object-oriented-programming/) that enables strongly-typed traversal of arbitrary type graphs: it can be used to generate object traversal algorithms that incur zero allocation cost. See the [project website](https://eiriktsarpalis.github.io/typeshape-csharp) for additional guides and [API documentation](https://eiriktsarpalis.github.io/typeshape-csharp/api/TypeShape.html).

The project includes two shape model providers: one [reflection derived](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape/ReflectionProvider) and one [source generated](https://github.com/eiriktsarpalis/typeshape-csharp/tree/main/src/TypeShape.SourceGenerator). It follows that any datatype-generic application built on top of the shape model gets trim safety/NativeAOT support for free once it targets source generated models.

## Using the library

Users can extract the shape model for a given type either using the built-in source generator:

```C#
ITypeShape<MyPoco> shape = TypeShapeProvider.Resolve<MyPoco>();

[GenerateShape] // Auto-generates a static abstract factory for ITypeShape<MyPoco>
public partial record MyPoco(string x, string y);
```

For types not accessible in the current compilation, the implementation can be generated using a separate witness type:

```C#
ITypeShape<MyPoco[]> shape = TypeShapeProvider.Resolve<MyPoco[], Witness>();
ITypeShape<MyPoco[][]> shape = TypeShapeProvider.Resolve<MyPoco[][], Witness>();

// Generates factories for both ITypeShape<MyPoco[]> and ITypeShape<MyPoco[][]>
[GenerateShape<MyPoco[]>]
[GenerateShape<MyPoco[][]>]
public partial class Witness;
```

The library also provides a reflection-based provider:

```C#
using TypeShape.ReflectionProvider;

ITypeShape<MyPoco> shape = ReflectionTypeShapeProvider.Default.GetShape<MyPoco>();
public record MyPoco(string x, string y);
```

In both cases the providers will generate a strongly typed datatype model for `MyPoco`. Models for types can be fed into datatype-generic consumers that can be declared using TypeShape's visitor pattern.

## Example: Writing a datatype-generic counter

The simplest possible example of a datatype-generic programming is counting the number of nodes that exist in a given object graph. This can be implemented by extending the `TypeShapeVisitor` class:

```C#
public sealed partial class CounterVisitor : TypeShapeVisitor
{
    // For the sake of simplicity, ignore collection types and just focus on properties/fields.
    public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state)
    {
        // Recursively generate counters for each individual property/field:
        Func<T, int>[] propertyCounters = objectShape.GetProperties()
            .Where(prop => prop.HasGetter)
            .Select(prop => (Func<T, int>)prop.Accept(this)!)
            .ToArray();

        // Compose into a counter for the current type.
        return new Func<T?, int>(value =>
        {
            if (value is null)
                return 0;

            int count = 1; // the current node itself
            foreach (Func<T, int> propertyCounter in propertyCounters)
                count += propertyCounter(value);

            return count;
        });
    }

    public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state)
    {
        Getter<TDeclaringType, TPropertyType> getter = propertyShape.GetGetter(); // extract the getter delegate
        var propertyTypeCounter = (Func<TPropertyType, int>)propertyShape.PropertyType.Accept(this)!; // extract the counter for the property type
        return new Func<TDeclaringType, int>(obj => propertyTypeCounter(getter(ref obj))); // compose to a property-specific counter
    }
}
```

We can now define a counter factory using the visitor:

```C#
public static class Counter
{
    private readonly static CounterVisitor s_visitor = new();

    public static Func<T?, int> CreateCounter<T>() where T : IShapeable<T>
    {
        ITypeShape<T> typeShape = T.GetShape();
        return (Func<T?, int>)typeShape.Accept(s_visitor)!;
    }
}
```

That we can then apply to the shape of our POCO like so:

```C#
Func<MyPoco?, int> pocoCounter = Counter.CreateCounter<MyPoco>();

[GenerateShape]
public partial record MyPoco(string? x, string? y);
```

In essence, TypeShape uses the visitor to fold a strongly typed `Func<MyPoco?, int>` counter delegate,
but the delegate itself doesn't depend on the visitor once invoked: it only defines a chain of strongly typed
delegate invocations that are cheap to invoke once constructed:

```C#
pocoCounter(new MyPoco("x", "y")); // 3
pocoCounter(new MyPoco("x", null)); // 2
pocoCounter(new MyPoco(null, null)); // 1
pocoCounter(null); // 0
```

For more details, please consult the [README file](https://github.com/eiriktsarpalis/typeshape-csharp#readme) at the project page.
