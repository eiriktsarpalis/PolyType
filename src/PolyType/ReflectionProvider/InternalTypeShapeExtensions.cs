using PolyType.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PolyType.ReflectionProvider;

internal static class InternalTypeShapeExtensions
{
    [RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
    internal static ITypeShape? GetAssociatedTypeShape(this ITypeShape self, Type associatedType)
    {
        if (associatedType.IsGenericTypeDefinition && self.Type.GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            throw new ArgumentException($"Related type arity ({associatedType.GenericTypeArguments.Length}) mismatch with original type ({self.Type.GenericTypeArguments.Length}).");
        }

        Type closedType = associatedType.IsGenericTypeDefinition
            ? associatedType.MakeGenericType(self.Type.GenericTypeArguments)
            : associatedType;

        return self.Provider.GetShape(closedType);
    }
}
