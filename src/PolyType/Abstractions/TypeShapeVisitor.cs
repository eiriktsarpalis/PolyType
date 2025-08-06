using System.Runtime.CompilerServices;

namespace PolyType.Abstractions;

/// <summary>
/// Provides a visitor for strongly-typed traversal of .NET types.
/// </summary>
/// <remarks>
/// Full methods are <see langword="virtual"/>, and will throw <see cref="NotImplementedException"/> if not overridden.
/// </remarks>
public abstract class TypeShapeVisitor
{
    /// <summary>
    /// Visits an <see cref="IObjectTypeShape{T}"/> representing a simple type or object.
    /// </summary>
    /// <typeparam name="T">The object type represented by the shape instance.</typeparam>
    /// <param name="objectShape">The type shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IPropertyShape{TDeclaringType, TPropertyType}"/> instance.
    /// </summary>
    /// <typeparam name="TDeclaringType">The declaring type of the visited property.</typeparam>
    /// <typeparam name="TPropertyType">The property type of the visited property.</typeparam>
    /// <param name="propertyShape">The property shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IConstructorShape{TDeclaringType,TArgumentState}"/> instance.
    /// </summary>
    /// <typeparam name="TDeclaringType">The declaring type of the visited constructor.</typeparam>
    /// <typeparam name="TArgumentState">The constructor argument state type used for aggregating constructor arguments.</typeparam>
    /// <param name="constructorShape">The constructor shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
        where TArgumentState : IArgumentState
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IMethodShape{TDeclaringType, TArgumentState,TReturnType}"/> instance.
    /// </summary>
    /// <typeparam name="TDeclaringType">The declaring type for the method.</typeparam>
    /// <typeparam name="TArgumentState">The method argument state type used for aggregating method arguments.</typeparam>
    /// <typeparam name="TResult">The return type of the visited method.</typeparam>
    /// <param name="methodShape">The method shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitMethod<TDeclaringType, TArgumentState, TResult>(IMethodShape<TDeclaringType, TArgumentState, TResult> methodShape, object? state = null)
        where TArgumentState : IArgumentState
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IParameterShape{TArgumentState,TParameterType}"/> instance.
    /// </summary>
    /// <typeparam name="TArgumentState">The constructor argument state type used for aggregating constructor arguments.</typeparam>
    /// <typeparam name="TParameterType">The type of the visited constructor parameter.</typeparam>
    /// <param name="parameterShape">The parameter shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null)
        where TArgumentState : IArgumentState
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IEnumTypeShape{TEnum,TUnderlying}"/> instance.
    /// </summary>
    /// <typeparam name="TEnum">The type of visited enum.</typeparam>
    /// <typeparam name="TUnderlying">The underlying type used by the enum.</typeparam>
    /// <param name="enumShape">The enum shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
        where TEnum : struct, Enum
        where TUnderlying : unmanaged
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IOptionalTypeShape{TOptional, TElement}"/> instance representing optional types.
    /// </summary>
    /// <typeparam name="TOptional">The optional type described by the shape.</typeparam>
    /// <typeparam name="TElement">The type of the value encapsulated by the option type.</typeparam>
    /// <param name="optionalShape">The optional shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null)
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IEnumerableTypeShape{TEnumerable,TElement}"/> instance representing an enumerable type.
    /// </summary>
    /// <typeparam name="TEnumerable">The type of the visited enumerable.</typeparam>
    /// <typeparam name="TElement">The element type of the visited enumerable.</typeparam>
    /// <param name="enumerableShape">The enumerable shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IDictionaryTypeShape{TDictionary, TKey, TValue}"/> instance representing a dictionary type.
    /// </summary>
    /// <typeparam name="TDictionary">The type of the visited dictionary.</typeparam>
    /// <typeparam name="TKey">The key type of the visited dictionary.</typeparam>
    /// <typeparam name="TValue">The value type of the visited dictionary.</typeparam>
    /// <param name="dictionaryShape">The dictionary shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
        where TKey : notnull
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="ISurrogateTypeShape{T, TSurrogate}"/> instance representing a type employing a surrogate type.
    /// </summary>
    /// <typeparam name="T">The type represented by the shape instance.</typeparam>
    /// <typeparam name="TSurrogate">The surrogate type used by the shape.</typeparam>
    /// <param name="surrogateShape">The surrogate shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IUnionTypeShape{TUnion}"/> instance representing a union type.
    /// </summary>
    /// <typeparam name="TUnion">The type of the visited union.</typeparam>
    /// <param name="unionShape">The union shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
        => ThrowNotImplementedException();

    /// <summary>
    /// Visits an <see cref="IUnionCaseShape{TUnionCase, TUnion}"/> instance representing a union case.
    /// </summary>
    /// <typeparam name="TUnionCase">The type of the visited union case.</typeparam>
    /// <typeparam name="TUnion">The type of the visited union.</typeparam>
    /// <param name="unionCaseShape">The union case shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    public virtual object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null) where TUnionCase : TUnion
        => ThrowNotImplementedException();

    private object? ThrowNotImplementedException([CallerMemberName] string? methodName = null)
        => throw new NotImplementedException($"The visitor method {GetType().Name}.{methodName} has not been implemented.");
}
