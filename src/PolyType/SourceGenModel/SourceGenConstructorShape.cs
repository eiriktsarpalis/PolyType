using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for a constructor shape.
/// </summary>
/// <typeparam name="TDeclaringType">The type being constructed.</typeparam>
/// <typeparam name="TArgumentState">The mutable argument state for the constructor.</typeparam>
public sealed class SourceGenConstructorShape<TDeclaringType, TArgumentState>(ITypeShapeProvider provider) : IConstructorShape<TDeclaringType, TArgumentState>(provider)
{
    private IReadOnlyList<IParameterShape>? _parameters;

    /// <inheritdoc />
    public override bool IsPublic => IsPublicSetter;

    /// <summary>
    /// Sets a value indicating whether the constructor is public.
    /// </summary>
    public required bool IsPublicSetter { private get; init; }

    /// <inheritdoc />
    public override IObjectTypeShape<TDeclaringType> DeclaringType => DeclaringTypeSetter;

    /// <summary>
    /// Sets the shape of the declaring type.
    /// </summary>
    public required IObjectTypeShape<TDeclaringType> DeclaringTypeSetter { private get; init; }

    /// <summary>
    /// Gets the number of parameters the constructor takes.
    /// </summary>
    public required int ParameterCount { get; init; }

    /// <summary>
    /// Gets the attribute provider for the constructor.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <summary>
    /// Gets the parameter shapes for the constructor.
    /// </summary>
    public Func<IEnumerable<IParameterShape>>? GetParametersFunc { get; init; }

    /// <summary>
    /// Gets the default constructor for the declaring type.
    /// </summary>
    public Func<TDeclaringType>? DefaultConstructorFunc { get; init; }

    /// <summary>
    /// Gets the argument state constructor for the constructor.
    /// </summary>
    public Func<TArgumentState>? ArgumentStateConstructorFunc { get; init; }

    /// <summary>
    /// Gets the parameterized constructor for the constructor.
    /// </summary>
    public Constructor<TArgumentState, TDeclaringType>? ParameterizedConstructorFunc { get; init; }

    /// <inheritdoc />
    public override IReadOnlyList<IParameterShape> Parameters => _parameters ??= (GetParametersFunc?.Invoke()).AsReadOnlyList();

    /// <inheritdoc />
    public override Func<TDeclaringType> GetDefaultConstructor()
        => DefaultConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a default constructor.");

    /// <inheritdoc />
    public override Func<TArgumentState> GetArgumentStateConstructor()
         => ArgumentStateConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a parameterized constructor.");

    /// <inheritdoc />
    public override Constructor<TArgumentState, TDeclaringType> GetParameterizedConstructor()
        => ParameterizedConstructorFunc ?? throw new InvalidOperationException("Constructor shape does not specify a parameterized constructor.");

    /// <inheritdoc />
    public override ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc?.Invoke();
}
