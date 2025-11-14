using System.Diagnostics;
using System.Reflection;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a strongly typed shape model for a given .NET event.
/// </summary>
[InternalImplementationsOnly]
public interface IEventShape
{
    /// <summary>
    /// Gets the name of the event.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the event is static.
    /// </summary>
    bool IsStatic { get; }

    /// <summary>
    /// Gets a value indicating whether the add method is declared public.
    /// </summary>
    bool IsPublic { get; }

    /// <summary>
    /// Gets the shape of the declaring type.
    /// </summary>
    ITypeShape DeclaringType { get; }

    /// <summary>
    /// Gets the shape of the event handler type.
    /// </summary>
    IFunctionTypeShape HandlerType { get; }

    /// <summary>
    /// Gets the underlying <see cref="System.Reflection.EventInfo"/> representing the event, if available.
    /// </summary>
    EventInfo? EventInfo { get; }

    /// <summary>
    /// Gets the provider used for event-level attribute resolution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides fast attribute resolution when using the source generator,
    /// otherwise this is wrapping standard reflection-based attribute resolution.
    /// </para>
    /// <para>
    /// When using the source generator, the following categories of attributes are excluded to reduce trimmed application size:
    /// <list type="bullet">
    /// <item><description><c>System.Runtime.CompilerServices.*</c> - Compiler-generated attributes</description></item>
    /// <item><description><c>System.Runtime.InteropServices.*</c> - COM interop attributes</description></item>
    /// <item><description><c>System.Diagnostics.*</c> - Diagnostic attributes</description></item>
    /// <item><description><c>System.Reflection.DefaultMemberAttribute</c> - Default member metadata</description></item>
    /// <item><description><c>System.CLSCompliantAttribute</c> - CLS compliance marker</description></item>
    /// <item><description><c>Microsoft.FSharp.Core.*</c> - F# compiler generated attributes</description></item>
    /// <item><description>Attributes marked with unmet <see cref="ConditionalAttribute" /> annotations</description></item>
    /// </list>
    /// Users requiring complete attribute resolution can use the <see cref="EventInfo"/> property
    /// to access standard reflection-based attribute APIs, though this will be slower.
    /// </para>
    /// </remarks>
    IGenericCustomAttributeProvider AttributeProvider { get; }

    /// <summary>
    /// Accepts an <see cref="TypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object"/> result returned by the visitor.</returns>
    object? Accept(TypeShapeVisitor visitor, object? state = null);
}

/// <summary>
/// Provides a strongly typed shape model for a given .NET event.
/// </summary>
/// <typeparam name="TDeclaringType">The type declaring the event.</typeparam>
/// <typeparam name="TEventHandler">The type of the event handler.</typeparam>
[InternalImplementationsOnly]
public interface IEventShape<TDeclaringType, TEventHandler> : IEventShape
{
    /// <summary>
    /// Gets the shape of the declaring type.
    /// </summary>
    new ITypeShape<TDeclaringType> DeclaringType { get; }

    /// <summary>
    /// Gets a delegate that adds an event handler to the event.
    /// </summary>
    /// <returns>A setter delegate that adds an event handler.</returns>
    Setter<TDeclaringType?, TEventHandler> GetAddHandler();

    /// <summary>
    /// Gets a delegate that removes an event handler from the event.
    /// </summary>
    /// <returns>A setter delegate that removes an event handler.</returns>
    Setter<TDeclaringType?, TEventHandler> GetRemoveHandler();
}
