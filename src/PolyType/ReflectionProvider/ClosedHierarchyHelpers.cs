using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal static class ClosedHierarchyHelpers
{
    private const string ClosedSubtypeAttributeFullName = "System.Runtime.CompilerServices.ClosedSubtypeAttribute";
    private const string ClosedAttributeFullName = "System.Runtime.CompilerServices.ClosedAttribute";

    public static bool HasClosedSubtypeAttributes(Type type)
    {
        foreach (CustomAttributeData attr in type.GetCustomAttributesData())
        {
            if (attr.AttributeType.FullName == ClosedSubtypeAttributeFullName)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsClosed(Type type)
    {
        foreach (CustomAttributeData attr in type.GetCustomAttributesData())
        {
            if (attr.AttributeType.FullName == ClosedAttributeFullName)
            {
                return true;
            }
        }

        return false;
    }

    public static Type[] GetClosedSubtypes(Type type)
    {
        var subtypes = new List<Type>();
        foreach (CustomAttributeData attr in type.GetCustomAttributesData())
        {
            if (attr.AttributeType.FullName == ClosedSubtypeAttributeFullName &&
                attr.ConstructorArguments is [{ Value: Type subtypeType }])
            {
                subtypes.Add(subtypeType);
            }
        }

        return subtypes.ToArray();
    }

    [RequiresUnreferencedCode("Assembly.GetTypes() may not return all types in trimmed applications.")]
    public static Type[] FindDirectSubtypesInAssembly(Type baseType)
    {
        var subtypes = new List<Type>();
        try
        {
            foreach (Type candidate in baseType.Assembly.GetTypes())
            {
                if (candidate != baseType && !candidate.IsAbstract && baseType.IsAssignableFrom(candidate))
                {
                    if (candidate.BaseType == baseType ||
                        (baseType.IsInterface && IsDirectInterfaceImplementation(candidate, baseType)))
                    {
                        subtypes.Add(candidate);
                    }
                }
            }
        }
        catch (ReflectionTypeLoadException)
        {
            // Assembly scanning may fail for various reasons; return what we have
        }

        return subtypes.ToArray();
    }

    public static DerivedTypeInfo[] GetClosedSubtypeDerivedInfos(Type baseType)
    {
        Type[] subtypes = GetClosedSubtypes(baseType);
        return BuildDerivedInfos(subtypes);
    }

    [RequiresUnreferencedCode("Assembly.GetTypes() may not return all types in trimmed applications.")]
    public static DerivedTypeInfo[] GetAssemblyScanDerivedInfos(Type baseType)
    {
        Type[] subtypes = FindDirectSubtypesInAssembly(baseType);
        return BuildDerivedInfos(subtypes);
    }

    public static DerivedTypeInfo[] GetInferredDerivedTypeInfos(Type baseType)
    {
        Type[] subtypes = GetClosedSubtypes(baseType);

        if (subtypes.Length == 0)
        {
            subtypes = FindDirectSubtypesInAssembly(baseType);
        }

        return BuildDerivedInfos(subtypes);
    }

    private static DerivedTypeInfo[] BuildDerivedInfos(Type[] subtypes)
    {
        var infos = new DerivedTypeInfo[subtypes.Length];
        for (int i = 0; i < subtypes.Length; i++)
        {
            Type subtype = subtypes[i];
            infos[i] = new DerivedTypeInfo(subtype, subtype.Name, Tag: i, Index: i, IsTagSpecified: false);
        }

        return infos;
    }

    private static bool IsDirectInterfaceImplementation(Type candidate, Type interfaceType)
    {
        if (candidate.BaseType is { } baseType && interfaceType.IsAssignableFrom(baseType))
        {
            return false;
        }

        return true;
    }
}
