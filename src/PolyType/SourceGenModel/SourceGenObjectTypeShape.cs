using PolyType.Abstractions;
using System.Reflection;
using System.Text;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for object type shapes.
/// </summary>
/// <typeparam name="TObject">The type whose shape is described.</typeparam>
public sealed class SourceGenObjectTypeShape<TObject> : SourceGenTypeShape<TObject>, IObjectTypeShape<TObject>
{
    /// <summary>
    /// Gets a value indicating whether the type represents a record.
    /// </summary>
    public required bool IsRecordType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type represents a tuple.
    /// </summary>
    public required bool IsTupleType { get; init; }

    /// <summary>
    /// Gets the factory method for creating property shapes.
    /// </summary>
    public Func<IEnumerable<IPropertyShape>>? CreatePropertiesFunc { get; init; }

    /// <summary>
    /// Gets the factory method for creating constructor shapes.
    /// </summary>
    public Func<IConstructorShape>? CreateConstructorFunc { get; init; }

    /// <summary>
    /// Gets the shape of an associated type, by its name.
    /// </summary>
    public Func<string, ITypeShape?>? AssociatedTypeShapes { get; init; }

    /// <inheritdoc/>
    public override TypeShapeKind Kind => TypeShapeKind.Object;

    /// <inheritdoc/>
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    /// <inheritdoc/>
    public override ITypeShape? GetAssociatedTypeShape(Type associatedType)
    {
        if (associatedType.IsGenericTypeDefinition && typeof(TObject).GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
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

    IReadOnlyList<IPropertyShape> IObjectTypeShape.Properties => _properties ??= (CreatePropertiesFunc?.Invoke()).AsReadOnlyList();
    private IReadOnlyList<IPropertyShape>? _properties;

    IConstructorShape? IObjectTypeShape.Constructor
    {
        get
        {
            if (!_isConstructorResolved)
            {
                _constructor = CreateConstructorFunc?.Invoke();
                Volatile.Write(ref _isConstructorResolved, true);
            }

            return _constructor;
        }
    }

    private bool _isConstructorResolved;
    private IConstructorShape? _constructor;
}
