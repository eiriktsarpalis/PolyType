using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Gets the first attribute of the specified type.
    /// </summary>
    /// <typeparam name="TAttribute">The type of attribute to retrieve.</typeparam>
    /// <returns>The first attribute of the specified type, or <see langword="null"/> if not found.</returns>
    public TAttribute? GetAttribute<TAttribute>() where TAttribute : Attribute
    {
        Attribute[] allAttributes = _attributesFactory();
        foreach (Attribute attr in allAttributes)
        {
            if (attr is TAttribute typedAttr)
            {
                return typedAttr;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether an attribute of the specified type is defined.
    /// </summary>
    /// <typeparam name="TAttribute">The type of attribute to check.</typeparam>
    /// <returns><see langword="true"/> if an attribute of the specified type is defined; otherwise, <see langword="false"/>.</returns>
    public bool IsDefined<TAttribute>() where TAttribute : Attribute
    {
        Attribute[] allAttributes = _attributesFactory();
        foreach (Attribute attr in allAttributes)
        {
            if (attr is TAttribute)
            {
                return true;
            }
        }

        return false;
    }
}
