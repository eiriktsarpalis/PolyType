using PolyType.Abstractions;
using System.Reflection;
using System.Text;

namespace PolyType.SourceGenModel;

internal static class InternalTypeShapeExtensions
{
    public static ITypeShape? GetAssociatedTypeShape<T>(this ITypeShape<T> self, Func<string, ITypeShape?>? associatedTypeShapes, Type associatedType)
    {
        if (associatedType.IsGenericTypeDefinition && typeof(T).GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            throw new ArgumentException("Type is not a generic type definition or does not have an equal count of generic type parameters with this type shape.");
        }

        StringBuilder builder = new();
        ConstructStableName(associatedType, builder);
        return associatedTypeShapes?.Invoke(builder.ToString());
    }

    private static void ConstructStableName(Type type, StringBuilder builder)
    {
        // The string created here must match the string created in the source generator (Parser.CreateAssociatedTypeId).
        if (type.DeclaringType is not null)
        {
            ConstructStableName(type.DeclaringType, builder);
            builder.Append('+');
        }
        else if (!string.IsNullOrEmpty(type.Namespace))
        {
            builder.Append(type.Namespace);
            builder.Append('.');
        }

        if (type.IsGenericType)
        {
            string nameNoArity = type.Name[..type.Name.IndexOf('`')];
            builder.Append(nameNoArity);
            builder.Append('<');
            if (type.IsGenericTypeDefinition)
            {
                builder.Append(',', GetOwnGenericTypeParameterCount(type) - 1);
            }
            else
            {
                for (int i = 0; i < type.GenericTypeArguments.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    ConstructStableName(type.GenericTypeArguments[i], builder);
                }
            }

            builder.Append('>');
        }
        else
        {
            builder.Append(type.Name);
        }

        static int GetOwnGenericTypeParameterCount(Type type)
            => type.DeclaringType?.IsGenericType is true
                ? type.GetTypeInfo().GenericTypeParameters.Length - type.DeclaringType.GetTypeInfo().GenericTypeParameters.Length
                : type.GetTypeInfo().GenericTypeParameters.Length;
    }
}
