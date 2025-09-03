using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

/// <summary>
/// Represents the data model for a delegate type.
/// </summary>
public class DelegateDataModel : TypeDataModel
{
    /// <inheritdoc/>
    public override TypeDataKind Kind => TypeDataKind.Delegate;

    /// <summary>
    /// The method symbol representing the 'Invoke' method of the delegate.
    /// </summary>
    public required IMethodSymbol InvokeMethod { get; init; }

    /// <summary>
    /// The type of the value returned by the delegate, or <see langword="null"/> if returning void, ValueTask, or Task.
    /// </summary>
    public required ITypeSymbol? ReturnedValueType { get; init; }

    /// <summary>
    /// The kind of return type for the delegate, indicating whether it returns void, a task, or a value task.
    /// </summary>
    public required MethodReturnTypeKind ReturnTypeKind { get; init; }

    /// <summary>
    /// The parameters of the delegate.
    /// </summary>
    public required ImmutableArray<ParameterDataModel> Parameters { get; init; }
}
