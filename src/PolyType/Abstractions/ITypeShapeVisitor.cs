﻿namespace PolyType.Abstractions;

/// <summary>
/// Provides a visitor for strongly-typed traversal of .NET types.
/// </summary>
public interface ITypeShapeVisitor
{
    /// <summary>
    /// Visits an <see cref="IObjectTypeShape{T}"/> representing a simple type or object.
    /// </summary>
    /// <typeparam name="T">The object type represented by the shape instance.</typeparam>
    /// <param name="objectShape">The type shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null);

    /// <summary>
    /// Visits an <see cref="IPropertyShape{TDeclaringType, TPropertyType}"/> instance.
    /// </summary>
    /// <typeparam name="TDeclaringType">The declaring type of the visited property.</typeparam>
    /// <typeparam name="TPropertyType">The property type of the visited property.</typeparam>
    /// <param name="propertyShape">The property shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null);

    /// <summary>
    /// Visits an <see cref="IConstructorParameterShape{TDeclaringType, TArgumentState}"/> instance.
    /// </summary>
    /// <typeparam name="TDeclaringType">The declaring type of the visited constructor.</typeparam>
    /// <typeparam name="TArgumentState">The state type used for aggregating constructor arguments.</typeparam>
    /// <param name="constructorShape">The constructor shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null);

    /// <summary>
    /// Visits an <see cref="IConstructorParameterShape{TArgumentState, TParameterType}"/> instance.
    /// </summary>
    /// <typeparam name="TArgumentState">The constructor argument state type used for aggregating constructor arguments.</typeparam>
    /// <typeparam name="TParameterType">The type of the visited constructor parameter.</typeparam>
    /// <param name="parameterShape">The parameter shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    object? VisitConstructorParameter<TArgumentState, TParameterType>(IConstructorParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null);

    /// <summary>
    /// Visits an <see cref="IEnumTypeShape{TEnum,TUnderlying}"/> instance.
    /// </summary>
    /// <typeparam name="TEnum">The type of visited enum.</typeparam>
    /// <typeparam name="TUnderlying">The underlying type used by the enum.</typeparam>
    /// <param name="enumShape">The enum shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
        where TEnum : struct, Enum;

    /// <summary>
    /// Visits an <see cref="INullableTypeShape{T}"/> instance representing the <see cref="Nullable{T}"/> type.
    /// </summary>
    /// <typeparam name="T">The element type of the visited nullable.</typeparam>
    /// <param name="nullableShape">The nullable shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    object? VisitNullable<T>(INullableTypeShape<T> nullableShape, object? state = null)
        where T : struct;

    /// <summary>
    /// Visits an <see cref="IEnumerableTypeShape{TEnumerable,TElement}"/> instance representing an enumerable type.
    /// </summary>
    /// <typeparam name="TEnumerable">The type of the visited enumerable.</typeparam>
    /// <typeparam name="TElement">The element type of the visited enumerable.</typeparam>
    /// <param name="enumerableShape">The enumerable shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null);

    /// <summary>
    /// Visits an <see cref="IDictionaryTypeShape{TDictionary, TKey, TValue}"/> instance representing a dictionary type.
    /// </summary>
    /// <typeparam name="TDictionary">The type of the visited dictionary.</typeparam>
    /// <typeparam name="TKey">The key type of the visited dictionary.</typeparam>
    /// <typeparam name="TValue">The value type of the visited dictionary.</typeparam>
    /// <param name="dictionaryShape">The dictionary shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
        where TKey : notnull;

    /// <summary>
    /// Visits an <see cref="ISurrogateTypeShape{T, TSurrogate}"/> instance representing a type employing a surrogate type.
    /// </summary>
    /// <typeparam name="T">The type represented by the shape instance.</typeparam>
    /// <typeparam name="TSurrogate">The surrogate type used by the shape.</typeparam>
    /// <param name="surrogateShape">The surrogate shape to visit.</param>
    /// <param name="state">Defines user-provided state.</param>
    /// <returns>The result produced by the visitor.</returns>
    object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null);
}
