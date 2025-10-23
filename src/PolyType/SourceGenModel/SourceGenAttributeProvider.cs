using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source-generated implementation of <see cref="ICustomAttributeProvider"/> that
/// returns attributes using a compile-time generated delegate.
/// </summary>
public sealed class SourceGenAttributeProvider : ICustomAttributeProvider
{
    private readonly Func<Attribute[]> _attributesFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGenAttributeProvider"/> class.
    /// </summary>
    /// <param name="attributesFactory">A delegate that returns the array of attributes.</param>
    public SourceGenAttributeProvider(Func<Attribute[]> attributesFactory)
    {
        _attributesFactory = attributesFactory ?? throw new ArgumentNullException(nameof(attributesFactory));
    }

    /// <inheritdoc/>
    public object[] GetCustomAttributes(bool inherit)
    {
        return _attributesFactory();
    }

    /// <inheritdoc/>
    public object[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        if (attributeType is null)
        {
            throw new ArgumentNullException(nameof(attributeType));
        }

        Attribute[] allAttributes = _attributesFactory();
        if (allAttributes.Length == 0)
        {
            return Array.Empty<object>();
        }

        List<object>? filtered = null;
        foreach (Attribute attr in allAttributes)
        {
            if (attributeType.IsInstanceOfType(attr))
            {
                (filtered ??= new List<object>()).Add(attr);
            }
        }

        return filtered?.ToArray() ?? Array.Empty<object>();
    }

    /// <inheritdoc/>
    public bool IsDefined(Type attributeType, bool inherit)
    {
        if (attributeType is null)
        {
            throw new ArgumentNullException(nameof(attributeType));
        }

        Attribute[] allAttributes = _attributesFactory();
        foreach (Attribute attr in allAttributes)
        {
            if (attributeType.IsInstanceOfType(attr))
            {
                return true;
            }
        }

        return false;
    }
}
