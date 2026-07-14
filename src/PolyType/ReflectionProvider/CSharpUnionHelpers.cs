using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal static class CSharpUnionHelpers
{
    private const string UnionAttributeFullName = "System.Runtime.CompilerServices.UnionAttribute";
    private const string IUnionFullName = "System.IUnion";

    public static bool IsCSharpUnion(Type type)
    {
        bool hasUnionAttribute = false;
        foreach (CustomAttributeData attr in type.GetCustomAttributesData())
        {
            if (attr.AttributeType.FullName == UnionAttributeFullName)
            {
                hasUnionAttribute = true;
                break;
            }
        }

        if (!hasUnionAttribute)
        {
            return false;
        }

        return FindIUnionInterface(type) is not null;
    }

    public static CSharpUnionCaseInfo[] GetCaseInfos(Type unionType, out Func<object, object?> valueAccessor)
    {
        // Resolve IUnion.Value property via reflection
        Type? iUnionInterface = FindIUnionInterface(unionType)
            ?? throw new InvalidOperationException($"Type '{unionType}' does not implement IUnion.");

        PropertyInfo valueProperty = iUnionInterface.GetProperty("Value")
            ?? throw new InvalidOperationException($"IUnion interface on '{unionType}' does not have a Value property.");

        // Build a fast accessor delegate for IUnion.Value
        MethodInfo getter = valueProperty.GetGetMethod()!;
        valueAccessor = obj => getter.Invoke(obj, null);

        // Extract case types from single-parameter public constructors
        var caseInfos = new List<CSharpUnionCaseInfo>();
        int index = 0;

        foreach (ConstructorInfo ctor in unionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            ParameterInfo[] parameters = ctor.GetParameters();
            if (parameters.Length == 1)
            {
                Type caseType = parameters[0].ParameterType;
                string name = caseType.Name;
                caseInfos.Add(new CSharpUnionCaseInfo(caseType, name, index, index, IsTagSpecified: false, MarshalConstructor: ctor, ValueAccessor: valueAccessor));
                index++;
            }
        }

        return caseInfos.ToArray();
    }

    private static Type? FindIUnionInterface(Type type)
    {
        foreach (Type iface in type.GetInterfaces())
        {
            if (iface.FullName == IUnionFullName)
            {
                return iface;
            }
        }

        return null;
    }
}
