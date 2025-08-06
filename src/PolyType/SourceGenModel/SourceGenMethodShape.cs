using System.Reflection;
using PolyType.Abstractions;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for method shapes.
/// </summary>
/// <typeparam name="TDeclaringType">The type declaring the method.</typeparam>
/// <typeparam name="TArgumentState">The mutable state type used for aggregating method arguments.</typeparam>
/// <typeparam name="TResult">The return type of the underlying method.</typeparam>
public sealed class SourceGenMethodShape<TDeclaringType, TArgumentState, TResult> : IMethodShape<TDeclaringType, TArgumentState, TResult>
    where TArgumentState : IArgumentState
{
    /// <summary>
    /// Gets the name of the method.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the method is declared public.
    /// </summary>
    public required bool IsPublic { get; init; }

    /// <summary>
    /// Gets a value indicating whether the method is static.
    /// </summary>
    public required bool IsStatic { get; init; }

    /// <summary>
    /// Gets a value indicating whether the underlying method returns void, Task, or ValueTask.
    /// </summary>
    public required bool IsVoidLike { get; init; }

    /// <summary>
    /// Gets a value indicating whether the underlying method returns a Task or ValueTask.
    /// </summary>
    public required bool IsAsync { get; init; }

    /// <summary>
    /// Gets the shape of the declaring type for the method.
    /// </summary>
    public required ITypeShape<TDeclaringType> DeclaringType { get; init; }

    /// <summary>
    /// Gets the shape of the return type of the method.
    /// </summary>
    public required ITypeShape<TResult> ReturnType { get; init; }

    /// <summary>
    /// Gets a factory method for creating parameter shapes.
    /// </summary>
    public Func<IEnumerable<IParameterShape>>? CreateParametersFunc { get; init; }

    /// <summary>
    /// Gets a constructor delegate for the custom attribute provider of the method.
    /// </summary>
    public Func<ICustomAttributeProvider?>? AttributeProviderFunc { get; init; }

    /// <summary>
    /// Gets a delegate for creating argument state constructor.
    /// </summary>
    public required Func<TArgumentState> ArgumentStateConstructor { get; init; }

    /// <summary>
    /// Gets a delegate for invoking the method.
    /// </summary>
    public required MethodInvoker<TDeclaringType, TArgumentState, TResult> MethodInvoker { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<IParameterShape> Parameters => _parameters ?? CommonHelpers.ExchangeIfNull(ref _parameters, (CreateParametersFunc?.Invoke()).AsReadOnlyList());

    private IReadOnlyList<IParameterShape>? _parameters;

    /// <inheritdoc/>
    ITypeShape IMethodShape.DeclaringType => DeclaringType;

    /// <inheritdoc/>
    ITypeShape IMethodShape.ReturnType => ReturnType;

    /// <inheritdoc/>
    public ICustomAttributeProvider? AttributeProvider => AttributeProviderFunc?.Invoke();

    /// <inheritdoc/>
    public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitMethod(this, state);

    /// <inheritdoc/>
    public Func<TArgumentState> GetArgumentStateConstructor() => ArgumentStateConstructor;

    /// <inheritdoc/>
    public MethodInvoker<TDeclaringType, TArgumentState, TResult> GetMethodInvoker() => MethodInvoker;
}