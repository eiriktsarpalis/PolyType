# PolyType

PolyType is a practical generic programming library for .NET. It facilitates the rapid development of feature-complete, high-performance libraries that interact with user-defined types. This includes serializers, structured loggers, mappers, validators, parsers, random generators, and equality comparers. Its built-in source generator ensures that any library built on top of PolyType gets [Native AOT support for free](https://eiriktsarpalis.wordpress.com/2024/10/22/source-generators-for-free/).

The project is a port of the [TypeShape](https://github.com/eiriktsarpalis/TypeShape) library for F#, adapted to patterns and idioms available in C#. The name PolyType is a reference to [polytypic programming](https://en.wikipedia.org/wiki/Polymorphism_(computer_science)#Polytypism), another term for generic programming.

See the [project website](https://eiriktsarpalis.github.io/PolyType) for additional background and [API documentation](https://eiriktsarpalis.github.io/PolyType/api/PolyType.html).

The package includes the `author-polytype-library` agent skill at `skills/author-polytype-library/SKILL.md`, covering visitor-based construction, caching, recursive types, and public API design for library authors. Restoring the package makes the file available in the NuGet cache; it does not install the skill into your repository.