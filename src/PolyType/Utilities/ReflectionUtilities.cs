using System.Reflection;
using System.Text;

namespace PolyType.Utilities;

/// <summary>
/// Defines a set of common reflection utilities for use by PolyType applications.
/// </summary>
public static class ReflectionUtilities
{
    /// <summary>
    /// Determines if the specified attribute is defined by the attribute provider.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to attempt to look up.</typeparam>
    /// <param name="attributeProvider">The custom attribute provider to look up from.</param>
    /// <param name="inherit">Whether to look for inherited attributes.</param>
    /// <returns><see langword="true"/> is the attribute is defined, or <see langword="false"/> otherwise.</returns>
    public static bool IsDefined<TAttribute>(this ICustomAttributeProvider attributeProvider, bool inherit = false)
        where TAttribute : Attribute
    {
        Throw.IfNull(attributeProvider);
        return attributeProvider.IsDefined(typeof(TAttribute), inherit);
    }

    /// <summary>
    /// Looks up the specified attribute provider for an attribute of the given type.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to attempt to look up.</typeparam>
    /// <param name="attributeProvider">The custom attribute provider to look up from.</param>
    /// <param name="inherit">Whether to look for inherited attributes.</param>
    /// <returns>The first occurrence of the attribute if found, or <see langword="null" /> otherwise.</returns>
    public static TAttribute? GetCustomAttribute<TAttribute>(this ICustomAttributeProvider attributeProvider, bool inherit = false)
        where TAttribute : Attribute
    {
        Throw.IfNull(attributeProvider);
        return attributeProvider.GetCustomAttributes(typeof(TAttribute), inherit).OfType<TAttribute>().FirstOrDefault();
    }

    /// <summary>
    /// Looks up the specified attribute provider for attributes of the given type.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to attempt to look up.</typeparam>
    /// <param name="attributeProvider">The custom attribute provider to look up from.</param>
    /// <param name="inherit">Whether to look for inherited attributes.</param>
    /// <returns>An enumerable containing all instances of the attribute defined on the attribute provider.</returns>
    public static IEnumerable<TAttribute> GetCustomAttributes<TAttribute>(this ICustomAttributeProvider attributeProvider, bool inherit = false)
        where TAttribute : Attribute
    {
        Throw.IfNull(attributeProvider);
        return attributeProvider.GetCustomAttributes(typeof(TAttribute), inherit).OfType<TAttribute>();
    }

    /// <summary>
    /// Returns a name suitable for auto-deriving DerivedTypeShapeAttribute.Name that includes type arguments separated by underscores.
    /// </summary>
    /// <param name="type">The type to format.</param>
    /// <returns>The formatted name with type arguments separated by underscores.</returns>
    /// <remarks>
    /// Examples:
    /// - Cow&lt;SolidHoof&gt; → Cow_SolidHoof
    /// - Cow&lt;List&lt;SolidHoof&gt;&gt; → Cow_List_SolidHoof
    /// </remarks>
    public static string GetDerivedTypeShapeName(Type type)
    {
        Throw.IfNull(type);
        StringBuilder builder = new StringBuilder();
        Type? skipDeclaringType = type.DeclaringType;
        FormatTypeWithUnderscores(type, builder, skipDeclaringType);
        return builder.ToString();

        static void FormatTypeWithUnderscores(Type type, StringBuilder builder, Type? skipDeclaringType)
        {
            if (type.IsArray)
            {
                FormatTypeWithUnderscores(type.GetElementType()!, builder, skipDeclaringType);
                builder.Append("Array");
                if (type.GetArrayRank() > 1)
                {
                    builder.Append(type.GetArrayRank());
                }

                return;
            }

            if (type.IsPointer)
            {
                FormatTypeWithUnderscores(type.GetElementType()!, builder, skipDeclaringType);
                builder.Append("Pointer");
                return;
            }

            if (type.IsGenericParameter)
            {
                builder.Append(type.Name);
                return;
            }

            // For nested types, include declaring type unless it matches the skip type
            if (type.DeclaringType is { } declaringType && declaringType != skipDeclaringType)
            {
                FormatTypeWithUnderscores(declaringType, builder, skipDeclaringType);
                builder.Append('_');
            }

            // Get the base name without generic arity marker
            string name = type.Name;
            int backtickIndex = name.IndexOf('`');
            if (backtickIndex >= 0)
            {
                name = name.Substring(0, backtickIndex);
            }

            builder.Append(name);

            // Append type arguments separated by underscores
            if (type.IsGenericType)
            {
                Type[] typeArgs = type.GetGenericArguments();
                // For nested types, filter out parent type arguments
                int startIndex = type.DeclaringType?.GetGenericArguments().Length ?? 0;
                for (int i = startIndex; i < typeArgs.Length; i++)
                {
                    builder.Append('_');
                    FormatTypeWithUnderscores(typeArgs[i], builder, skipDeclaringType);
                }
            }
        }
    }
}
