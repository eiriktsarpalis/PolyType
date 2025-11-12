using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Extends <see cref="ICustomAttributeProvider"/> with generic methods for attribute resolution.
/// </summary>
public interface IGenericCustomAttributeProvider : ICustomAttributeProvider
{
    /// <summary>
    /// Retrieves a custom attribute defined on this member.
    /// </summary>
    /// <typeparam name="TAttribute">The type of attribute to search for.</typeparam>
    /// <param name="inherit">When <see langword="true"/>, look up the hierarchy chain for the inherited custom attribute.</param>
    /// <returns>
    /// A custom attribute that matches <typeparamref name="TAttribute"/>,
    /// or <see langword="null"/> if no such attribute is found.
    /// </returns>
    TAttribute? GetCustomAttribute<TAttribute>(bool inherit = true) where TAttribute : Attribute;

    /// <summary>
    /// Retrieves a collection of custom attributes of a specified type that are applied to a specified member.
    /// </summary>
    /// <typeparam name="TAttribute">The type of attribute to search for.</typeparam>
    /// <param name="inherit">When <see langword="true"/>, look up the hierarchy chain for the inherited custom attribute.</param>
    /// <returns>
    /// A collection of the custom attributes that are applied to element and that match <typeparamref name="TAttribute"/>,
    /// or an empty collection if no such attributes exist.
    /// </returns>
    IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(bool inherit = true) where TAttribute : Attribute;

    /// <summary>
    /// Checks if a particular custom attribute is defined on this member.
    /// </summary>
    /// <typeparam name="TAttribute">The type of attribute to search for.</typeparam>
    /// <param name="inherit">When <see langword="true"/>, look up the hierarchy chain for the inherited custom attribute.</param>
    /// <returns><see langword="true"/> if an instance of the attribute was found, or <see langword="false"/> otherwise.</returns>
    bool IsDefined<TAttribute>(bool inherit = true) where TAttribute : Attribute;
}