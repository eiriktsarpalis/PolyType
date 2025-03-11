using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal abstract class ReflectionTypeShape<T>(ReflectionTypeShapeProvider provider) : ITypeShape<T>
{
    public abstract TypeShapeKind Kind { get; }
    public abstract object? Accept(ITypeShapeVisitor visitor, object? state = null);
    public ReflectionTypeShapeProvider Provider => provider;
    public Type Type => typeof(T);

    ITypeShapeProvider ITypeShape.Provider => provider;
    ICustomAttributeProvider? ITypeShape.AttributeProvider => typeof(T);
    object? ITypeShape.Invoke(ITypeShapeFunc func, object? state) => func.Invoke(this, state);

    public Func<object>? GetAssociatedTypeFactory(Type relatedType)
    {
        if (relatedType.IsGenericTypeDefinition && this.Type.GenericTypeArguments.Length != relatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            throw new ArgumentException($"Related type arity ({relatedType.GenericTypeArguments.Length}) mismatch with original type ({this.Type.GenericTypeArguments.Length}).");
        }

        Type closedType = relatedType.IsGenericTypeDefinition
            ? relatedType.MakeGenericType(this.Type.GenericTypeArguments)
            : relatedType;

        ConstructorInfo? ctor = closedType?.GetConstructor(Type.EmptyTypes);
        return ctor is null ? null : () => ctor.Invoke([]);
    }
}
