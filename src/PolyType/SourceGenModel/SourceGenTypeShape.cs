using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for type shapes.
/// </summary>
/// <typeparam name="T">The type that the shape describes.</typeparam>
public abstract class SourceGenTypeShape<T> : ITypeShape<T>
{
    /// <summary>
    /// Gets the <see cref="TypeShapeKind"/> that the current shape supports.
    /// </summary>
    public abstract TypeShapeKind Kind { get; }

    /// <summary>
    /// Gets the provider used to generate this instance.
    /// </summary>
    public required ITypeShapeProvider Provider { get; init; }

    Type ITypeShape.Type => typeof(T);
    ICustomAttributeProvider? ITypeShape.AttributeProvider => typeof(T);

    /// <summary>
    /// Gets the factory method for creating method shapes.
    /// </summary>
    public Func<IEnumerable<IMethodShape>>? CreateMethodsFunc { get; init; }

    /// <summary>
    /// Gets the shape of an associated type, by its name.
    /// </summary>
    public Func<string, ITypeShape?>? AssociatedTypeShapes { get; init; }

    /// <summary>
    /// Accepts an <see cref="TypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);

    /// <inheritdoc/>
    object? ITypeShape.Invoke(ITypeShapeFunc func, object? state) => func.Invoke(this, state);

    /// <inheritdoc/>
    public IReadOnlyList<IMethodShape> Methods => _methods ?? CommonHelpers.ExchangeIfNull(ref _methods, (CreateMethodsFunc?.Invoke()).AsReadOnlyList());

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IReadOnlyList<IMethodShape>? _methods;

    /// <inheritdoc/>
    public ITypeShape? GetAssociatedTypeShape(Type associatedType)
    {
        if (associatedType.IsGenericTypeDefinition && typeof(T).GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            throw new ArgumentException("Type is not a generic type definition or does not have an equal count of generic type parameters with this type shape.");
        }

        StringBuilder builder = new();
        ConstructStableName(associatedType, builder);
        return AssociatedTypeShapes?.Invoke(builder.ToString());
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
