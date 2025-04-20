using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal abstract class ReflectionTypeShape<T>(ReflectionTypeShapeProvider provider) : ITypeShape<T>
{
    public abstract TypeShapeKind Kind { get; }
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);
    public ReflectionTypeShapeProvider Provider => provider;
    public Type Type => typeof(T);

    ITypeShapeProvider ITypeShape.Provider => provider;
    ICustomAttributeProvider? ITypeShape.AttributeProvider => typeof(T);
    object? ITypeShape.Invoke(ITypeShapeFunc func, object? state) => func.Invoke(this, state);

    public ITypeShape? GetAssociatedTypeShape(Type associatedType)
    {
        if (associatedType.IsGenericTypeDefinition && this.Type.GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            throw new ArgumentException($"Related type arity ({associatedType.GenericTypeArguments.Length}) mismatch with original type ({this.Type.GenericTypeArguments.Length}).");
        }

        Type closedType = associatedType.IsGenericTypeDefinition
            ? associatedType.MakeGenericType(this.Type.GenericTypeArguments)
            : associatedType;

        return provider.GetShape(closedType);
    }
}
