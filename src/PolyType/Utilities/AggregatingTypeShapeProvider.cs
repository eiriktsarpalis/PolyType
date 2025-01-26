using PolyType.Abstractions;

namespace PolyType.Utilities;

/// <summary>
/// An <see cref="ITypeShapeProvider"/> that returns the first shape found
/// from iterating over a given list of providers.
/// </summary>
public sealed class AggregatingTypeShapeProvider : ITypeShapeProvider
{
    private readonly ITypeShapeProvider[] _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregatingTypeShapeProvider"/> class.
    /// </summary>
    /// <param name="providers">The sequence of providers to solicit for type shapes.</param>
    public AggregatingTypeShapeProvider(params ITypeShapeProvider[] providers)
    {
        if (providers is null)
        {
            throw new ArgumentNullException(nameof(providers));
        }

        foreach (ITypeShapeProvider provider in providers)
        {
            if (provider is null)
            {
                throw new ArgumentException("One or more providers are null.", nameof(providers));
            }
        }

        _providers = providers;
    }

    /// <summary>
    /// Gets the list of providers making up this aggregate provider.
    /// </summary>
    public IReadOnlyList<ITypeShapeProvider> Providers => _providers;

    /// <inheritdoc/>
    public ITypeShape? GetShape(Type type)
    {
        foreach (ITypeShapeProvider provider in _providers)
        {
            if (provider.GetShape(type) is ITypeShape shape)
            {
                return shape;
            }
        }

        return null;
    }
}
