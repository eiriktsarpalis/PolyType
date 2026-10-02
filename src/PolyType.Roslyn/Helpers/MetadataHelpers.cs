using System.Reflection.Metadata;

namespace PolyType.Roslyn.Helpers;

internal static class MetadataHelpers
{
    public static CustomAttributeHandle GetAttribute(
        MetadataReader reader,
        CustomAttributeHandleCollection attributes,
        string @namespace,
        string name)
    {
        foreach (CustomAttributeHandle handle in attributes)
        {
            EntityHandle constructor = reader.GetCustomAttribute(handle).Constructor;
            EntityHandle declaringType = constructor.Kind switch
            {
                HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
                HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
                _ => default,
            };

            StringHandle typeName;
            StringHandle typeNamespace;
            switch (declaringType.Kind)
            {
                case HandleKind.TypeReference:
                    TypeReference reference = reader.GetTypeReference((TypeReferenceHandle)declaringType);
                    typeName = reference.Name;
                    typeNamespace = reference.Namespace;
                    break;

                case HandleKind.TypeDefinition:
                    TypeDefinition definition = reader.GetTypeDefinition((TypeDefinitionHandle)declaringType);
                    typeName = definition.Name;
                    typeNamespace = definition.Namespace;
                    break;

                default:
                    continue;
            }

            if (reader.StringComparer.Equals(typeName, name) &&
                reader.StringComparer.Equals(typeNamespace, @namespace))
            {
                return handle;
            }
        }

        return default;
    }
}
