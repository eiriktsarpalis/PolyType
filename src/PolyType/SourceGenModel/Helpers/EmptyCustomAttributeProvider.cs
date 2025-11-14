using PolyType.Abstractions;

namespace PolyType.SourceGenModel.Helpers;

internal sealed class EmptyCustomAttributeProvider : IGenericCustomAttributeProvider
{
    public static EmptyCustomAttributeProvider Instance { get; } = new();

    public TAttribute? GetCustomAttribute<TAttribute>(bool inherit = true) where TAttribute : Attribute => null;
    public IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(bool inherit = true) where TAttribute : Attribute => [];
    public object[] GetCustomAttributes(bool inherit) => [];
    public object[] GetCustomAttributes(Type attributeType, bool inherit) => [];
    public bool IsDefined<TAttribute>(bool inherit = true) where TAttribute : Attribute => false;
    public bool IsDefined(Type attributeType, bool inherit) => false;
}
