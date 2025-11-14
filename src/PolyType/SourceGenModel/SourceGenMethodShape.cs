using PolyType.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace PolyType.SourceGenModel;

/// <summary>
/// Source generator model for method shapes.
/// </summary>
/// <typeparam name="TDeclaringType">The type declaring the method.</typeparam>
/// <typeparam name="TArgumentState">The mutable state type used for aggregating method arguments.</typeparam>
/// <typeparam name="TResult">The return type of the underlying method.</typeparam>
[DebuggerTypeProxy(typeof(PolyType.Debugging.MethodShapeDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class SourceGenMethodShape<TDeclaringType, TArgumentState, TResult> : IMethodShape<TDeclaringType, TArgumentState, TResult>
    where TArgumentState : IArgumentState
{
    /// <inheritdoc/>
    public required string Name { get; init; }

    /// <inheritdoc/>
    public bool IsPublic { get; init; }

    /// <inheritdoc/>
    public bool IsStatic { get; init; }

    /// <inheritdoc/>
    public bool IsVoidLike { get; init; }

    /// <inheritdoc/>
    public bool IsAsync { get; init; }

    /// <inheritdoc/>
    public required ITypeShape<TDeclaringType> DeclaringType { get; init; }

    /// <inheritdoc/>
    public required ITypeShape<TResult> ReturnType { get; init; }

    /// <summary>
    /// Gets a factory method for creating parameter shapes.
    /// </summary>
    public Func<IEnumerable<IParameterShape>>? CreateParametersFunc { get; init; }

    /// <summary>
    /// Gets a constructor delegate for the custom attribute provider of the method.
    /// </summary>
    public Func<SourceGenAttributeInfo[]>? AttributeFactory { get; init; }

    /// <summary>
    /// Gets the method base resolver factory.
    /// </summary>
    public Func<MethodBase?>? MethodBaseFunc { get; init; }

    /// <summary>
    /// Gets a delegate for creating argument state constructor.
    /// </summary>
    public required Func<TArgumentState> ArgumentStateConstructor { get; init; }

    /// <summary>
    /// Gets a delegate for invoking the method.
    /// </summary>
    public required MethodInvoker<TDeclaringType?, TArgumentState, TResult> MethodInvoker { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<IParameterShape> Parameters => _parameters ?? CommonHelpers.ExchangeIfNull(ref _parameters, (CreateParametersFunc?.Invoke()).AsReadOnlyList());

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IReadOnlyList<IParameterShape>? _parameters;

    /// <inheritdoc />
    public MethodBase? MethodBase => _methodBase ??= MethodBaseFunc?.Invoke();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private MethodBase? _methodBase;

    /// <inheritdoc />
    public IGenericCustomAttributeProvider AttributeProvider => _attributeProvider ??= SourceGenCustomAttributeProvider.Create(AttributeFactory);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IGenericCustomAttributeProvider? _attributeProvider;

    ITypeShape IMethodShape.DeclaringType => DeclaringType;

    ITypeShape IMethodShape.ReturnType => ReturnType;

    object? IMethodShape.Accept(TypeShapeVisitor visitor, object? state) => visitor.VisitMethod(this, state);

    Func<TArgumentState> IMethodShape<TDeclaringType, TArgumentState, TResult>.GetArgumentStateConstructor() => ArgumentStateConstructor;

    MethodInvoker<TDeclaringType?, TArgumentState, TResult> IMethodShape<TDeclaringType, TArgumentState, TResult>.GetMethodInvoker() => MethodInvoker;

    private string DebuggerDisplay => $"{ReturnType} {Name}({string.Join(", ", Parameters.Select(p => $"{p.ParameterType} {p.Name}"))})";
}