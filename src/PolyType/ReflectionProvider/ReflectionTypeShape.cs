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

    public Func<object>? GetRelatedTypeFactory(
        Type relatedType)
    {
        if (!this.Type.IsGenericType)
        {
            throw new InvalidOperationException();
        }

        if (!relatedType.IsGenericTypeDefinition || relatedType.GenericTypeArguments.Length != this.Type.GenericTypeArguments.Length)
        {
            throw new ArgumentException();
        }

        if (relatedType.MakeGenericType(this.Type.GenericTypeArguments)?.GetConstructor(Type.EmptyTypes) is not ConstructorInfo ctor)
        {
            return null;
        }

        return () => ctor.Invoke([]);
    }
}
