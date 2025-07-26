using PolyType.Abstractions;
using System.Collections;

namespace PolyType.Examples.Utilities;

/// <summary>
/// A helper type for tracking duplicate properties assigned to a mutable object.
/// </summary>
public struct DuplicatePropertyValidator
{
    private readonly IReadOnlyList<IPropertyShape> _properties;
    private readonly Func<IPropertyShape, Exception> _throwOnDuplicateProperty;
    private readonly uint _length;
    private ulong _smallSet;
    private readonly BitArray? _largeSet;

    /// <summary>
    /// Creates a new instance of the <see cref="DuplicatePropertyValidator"/> struct.
    /// </summary>
    /// <param name="properties">The list of properties to track.</param>
    /// <param name="throwOnDuplicateProperty">A function to call when a duplicate property is detected.</param>
    public DuplicatePropertyValidator(
        IReadOnlyList<IPropertyShape> properties,
        Func<IPropertyShape, Exception>? throwOnDuplicateProperty = null)
    {
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _throwOnDuplicateProperty = throwOnDuplicateProperty ?? (static prop => new ArgumentException($"Duplicate property: {prop.Name}"));
        _length = (uint)properties.Count;
        if (_length > 64)
        {
            _largeSet = new BitArray((int)_length);
        }
    }

    /// <summary>
    /// Marks the property as read or throws an exception if the property has already been added.
    /// </summary>
    /// <param name="propertyIndex">The index of the property we're marking as read.</param>
    public void MarkAsRead(int propertyIndex)
    {
        if (!TryMarkAsRead(propertyIndex))
        {
            ThrowDuplicateProperty(propertyIndex);
        }
    }

    /// <summary>
    /// Attempts to mark the property as read without throwing an exception.
    /// </summary>
    /// <param name="propertyIndex">The index of the property to mark as read.</param>
    public bool TryMarkAsRead(int propertyIndex)
    {
        if ((uint)propertyIndex >= _length)
        {
            Throw();
            static void Throw() => throw new ArgumentOutOfRangeException(nameof(propertyIndex), "Index is out of range.");
        }

        bool isUnset;
        if (_largeSet is BitArray bitArray)
        {
            isUnset = !bitArray[propertyIndex];
            bitArray[propertyIndex] = true;
        }
        else
        {
            ulong flag = 1UL << propertyIndex;
            isUnset = (_smallSet & flag) == 0;
            _smallSet |= flag;
        }

        return isUnset;
    }

    private void ThrowDuplicateProperty(int propertyIndex)
    {
        IPropertyShape property = _properties[propertyIndex];
        throw _throwOnDuplicateProperty(property);
    }
}
