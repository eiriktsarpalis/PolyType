﻿using System.Reflection;

namespace TypeShape;

/// <summary>
/// Provides a strongly-typed shape model for a given .NET constructor.
/// </summary>
public interface IConstructorShape
{
    /// <summary>
    /// The shape of the declaring type for the constructor.
    /// </summary>
    ITypeShape DeclaringType { get; }

    /// <summary>
    /// The total number of parameters required by the constructor.
    /// </summary>
    /// <remarks>
    /// This number can include both actual constructor parameters and 
    /// logical constructor parameters such as required or init-only properties.
    /// </remarks>
    int ParameterCount { get; }

    /// <summary>
    /// The provider used for method-level attribute resolution.
    /// </summary>
    ICustomAttributeProvider? AttributeProvider { get; }

    /// <summary>
    /// Creates an enumeration of strongly-typed models for the constructor's parameters.
    /// </summary>
    /// <returns>An enumeration of <see cref="IConstructorParameterShape"/> models.</returns>
    IEnumerable<IConstructorParameterShape> GetParameters();

    /// <summary>
    /// Accepts an <see cref="ITypeShapeVisitor"/> for strongly-typed traversal.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="state">The state parameter to pass to the underlying visitor.</param>
    /// <returns>The <see cref="object?"/> result returned by the visitor.</returns>
    object? Accept(ITypeShapeVisitor visitor, object? state);
}

/// <summary>
/// Provides a strongly-typed shape model for a given .NET constructor.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type of the underlying constructor.</typeparam>
/// <typeparam name="TArgumentState">The state type used for aggregating constructor arguments.</typeparam>
public interface IConstructorShape<TDeclaringType, TArgumentState> : IConstructorShape
{
    /// <summary>
    /// Creates a delegate wrapping a parameterless constructor, if applicable.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <see cref="ParameterCount"/> of the constructor is not zero.</exception>
    /// <returns>A parameterless delegate creating a default instance of <see cref="TDeclaringType"/>.</returns>
    Func<TDeclaringType> GetDefaultConstructor();

    /// <summary>
    /// Creates a constructor delegate parameterized on an argument state object.
    /// </summary>
    /// <returns>A parameterized delegate returning an instance of <see cref="TDeclaringType"/>.</returns>
    Constructor<TArgumentState, TDeclaringType> GetParameterizedConstructor();

    /// <summary>
    /// Creates a constructor delegate for creating a default argument state instance.
    /// </summary>
    /// <returns>An uninitialized <see cref="TArgumentState"/> value for building constructor parameters.</returns>
    Func<TArgumentState> GetArgumentStateConstructor();
}