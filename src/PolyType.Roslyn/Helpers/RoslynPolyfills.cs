using Microsoft.CodeAnalysis;
using System.Reflection;

namespace PolyType.Roslyn.Helpers;

internal static class RoslynPolyfills
{
    private static readonly Func<IPropertySymbol, bool>? s_isRequiredProperty =
        CreatePropertyAccessor<IPropertySymbol, bool>(typeof(IPropertySymbol).GetProperty("IsRequired"));
    private static readonly Func<IFieldSymbol, bool>? s_isRequiredField =
        CreatePropertyAccessor<IFieldSymbol, bool>(typeof(IFieldSymbol).GetProperty("IsRequired"));
    private static readonly Func<ISymbol, int>? s_metadataTokenAccessor =
        CreatePropertyAccessor<ISymbol, int>(typeof(ISymbol).GetProperty("MetadataToken"));

    extension(IPropertySymbol property)
    {
        public bool IsRequired => s_isRequiredProperty?.Invoke(property) ?? false;
    }

    extension(IFieldSymbol fieldInfo)
    {
        public bool IsRequired => s_isRequiredField?.Invoke(fieldInfo) ?? false;
    }

    extension(ISymbol symbol)
    {
        public int MetadataToken => s_metadataTokenAccessor?.Invoke(symbol) ?? 0;
    }

    private static Func<T, TProperty>? CreatePropertyAccessor<T, TProperty>(PropertyInfo? propertyInfo)
    {
        return propertyInfo?.GetMethod is null ? null : (Func<T, TProperty>)Delegate.CreateDelegate(typeof(Func<T, TProperty>), propertyInfo.GetMethod);
    }
}
