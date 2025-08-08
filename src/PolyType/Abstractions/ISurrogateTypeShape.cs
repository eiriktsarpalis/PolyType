namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a .NET type that employs a surrogate type.
/// </summary>
[InternalImplementationsOnly]
public interface ISurrogateTypeShape : ITypeShape
{
    /// <summary>
    /// Gets the shape of the surrogate type.
    /// </summary>
    ITypeShape SurrogateType { get; }
}

/// <summary>
/// Provides a strongly typed shape model for a .NET type that employs a surrogate type.
/// </summary>
/// <typeparam name="T">The type the shape describes.</typeparam>
/// <typeparam name="TSurrogate">The surrogate type being specified by the shape.</typeparam>
[InternalImplementationsOnly]
public interface ISurrogateTypeShape<T, TSurrogate> : ITypeShape<T>, ISurrogateTypeShape
{
    /// <summary>
    /// Gets a bidirectional mapper between <typeparamref name="T"/> and <typeparamref name="TSurrogate"/>.
    /// </summary>
    IMarshaler<T, TSurrogate> Marshaler { get; }

    /// <summary>
    /// Gets the shape of the element type of the nullable.
    /// </summary>
    new ITypeShape<TSurrogate> SurrogateType { get; }
}