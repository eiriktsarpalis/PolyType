using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace PolyType.Roslyn;

/// <summary>
/// A method data model wrapping an <see cref="IMethodSymbol"/>.
/// </summary>
public readonly struct MethodDataModel
{
    /// <summary>
    /// The name used to identify the method in the generated code.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The method symbol that this model represents.
    /// </summary>
    public required IMethodSymbol Method { get; init; }

    /// <summary>
    /// The type of the value returned by the method, or <see langword="null"/> if returning void, ValueTask, or Task.
    /// </summary>
    public required ITypeSymbol? ReturnedValueType { get; init; }

    /// <summary>
    /// The kind of return type for the method, indicating whether it returns void, a task, or a value task.
    /// </summary>
    public required MethodReturnTypeKind ReturnTypeKind { get; init; }

    /// <summary>
    /// The parameters of the method.
    /// </summary>
    public required ImmutableArray<ParameterDataModel> Parameters { get; init; }

    /// <summary>
    /// Whether the method is ambiguous due to diamond inheritance.
    /// </summary>
    public required bool IsAmbiguous { get; init; }
}

/// <summary>
/// Represents the different kinds of return types that a method can have.
/// </summary>
public enum MethodReturnTypeKind
{
    /// <summary>
    /// The return type is not recognized or does not fall into any other category.
    /// </summary>
    Unrecognized,

    /// <summary>
    /// The method returns void.
    /// </summary>
    Void,

    /// <summary>
    /// The method returns a <see cref="System.Threading.Tasks.ValueTask"/>.
    /// </summary>
    ValueTask,

    /// <summary>
    /// The method returns a <see cref="System.Threading.Tasks.Task"/>.
    /// </summary>
    Task,

    /// <summary>
    /// The method returns a <see cref="System.Threading.Tasks.ValueTask{TResult}"/>.
    /// </summary>
    ValueTaskOfT,

    /// <summary>
    /// The method returns a <see cref="System.Threading.Tasks.Task{TResult}"/>.
    /// </summary>
    TaskOfT,
}

/// <summary>
/// A resolved method symbol that includes additional metadata.
/// </summary>
public readonly struct ResolvedMethodSymbol
{
    /// <summary>
    /// Gets the resolved method symbol.
    /// </summary>
    public required IMethodSymbol MethodSymbol { get; init; }

    /// <summary>
    /// Gets a custom name to be applied to the method, if specified.
    /// </summary>
    public string? CustomName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the method is ambiguous due to diamond inheritance.
    /// </summary>
    public bool IsAmbiguous { get; init; }
}