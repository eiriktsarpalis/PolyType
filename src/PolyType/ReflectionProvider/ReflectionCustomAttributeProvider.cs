using PolyType.Abstractions;
using PolyType.SourceGenModel.Helpers;
using PolyType.Utilities;
using System.Reflection;

namespace PolyType.ReflectionProvider;

internal sealed class ReflectionCustomAttributeProvider(ICustomAttributeProvider underlyingProvider) : IGenericCustomAttributeProvider
{
    public static IGenericCustomAttributeProvider Create(ICustomAttributeProvider? underlyingProvider) =>
        underlyingProvider is null ? EmptyCustomAttributeProvider.Instance : new ReflectionCustomAttributeProvider(underlyingProvider);

    public TAttribute? GetCustomAttribute<TAttribute>(bool inherit = true) where TAttribute : Attribute
    {
        object[] attributes = underlyingProvider.GetCustomAttributes(typeof(TAttribute), inherit);
        return attributes.Length > 0 ? (TAttribute?)attributes[0] : null;
    }

    public IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(bool inherit = true) where TAttribute : Attribute
    {
        foreach (var attr in underlyingProvider.GetCustomAttributes(typeof(TAttribute), inherit))
        {
            yield return (TAttribute)attr;
        }
    }

    public bool IsDefined<TAttribute>(bool inherit = true) where TAttribute : Attribute =>
        underlyingProvider.IsDefined(typeof(TAttribute), inherit);

    public object[] GetCustomAttributes(bool inherit) => underlyingProvider.GetCustomAttributes(inherit);
    public object[] GetCustomAttributes(Type attributeType, bool inherit) => underlyingProvider.GetCustomAttributes(attributeType, inherit);
    public bool IsDefined(Type attributeType, bool inherit) => underlyingProvider.IsDefined(attributeType, inherit);
}