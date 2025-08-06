using System.Diagnostics.CodeAnalysis;

namespace PolyType.Abstractions;

/// <summary>
/// Delegate representing a property/field getter.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type from which to get a value.</typeparam>
/// <typeparam name="TPropertyType">The property type of the underlying getter.</typeparam>
/// <param name="obj">The instance from which to get the value.</param>
/// <returns>The value returned by the getter.</returns>
public delegate TPropertyType Getter<TDeclaringType, TPropertyType>(ref TDeclaringType obj);

/// <summary>
/// Delegate representing a property/field setter.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type on which to set a value.</typeparam>
/// <typeparam name="TPropertyType">The property type of the underlying setter.</typeparam>
/// <param name="obj">The instance on which to set the value.</param>
/// <param name="value">The value to be set to the instance.</param>
public delegate void Setter<TDeclaringType, TPropertyType>(ref TDeclaringType obj, TPropertyType value);

/// <summary>
/// Delegate representing a parameterized constructor.
/// </summary>
/// <typeparam name="TArgumentState">Type of the state object containing all constructor arguments.</typeparam>
/// <typeparam name="TDeclaringType">Type of the object to be constructed.</typeparam>
/// <param name="state">State object containing all constructor arguments.</param>
/// <returns>The instance created by the constructor.</returns>
public delegate TDeclaringType Constructor<TArgumentState, TDeclaringType>(ref TArgumentState state);

/// <summary>
/// Delegate representing a parameterized method invocation.
/// </summary>
/// <typeparam name="TDeclaringType">The declaring type containing the method.</typeparam>
/// <typeparam name="TArgumentState">Type of the state object containing all method arguments.</typeparam>
/// <typeparam name="TResult">Type of the value returned by the method.</typeparam>
/// <param name="target">The instance on which the method should be invoked, or <see langword="default"/> if targeting static methods.</param>
/// <param name="state">State object containing all method arguments.</param>
/// <returns>The value returned by the method.</returns>
public delegate ValueTask<TResult> MethodInvoker<TDeclaringType, TArgumentState, TResult>(ref TDeclaringType? target, ref TArgumentState state);

/// <summary>
/// Delegate representing a default constructor for a mutable collection.
/// </summary>
/// <typeparam name="TKey">The type of keys or elements in the collection.</typeparam>
/// <typeparam name="TDeclaringType">The type of the collection to be constructed.</typeparam>
/// <param name="options">An optional set of parameters used to create the collection.</param>
/// <returns>A newly constructed mutable collection instance.</returns>
public delegate TDeclaringType MutableCollectionConstructor<TKey, TDeclaringType>(in CollectionConstructionOptions<TKey> options = default);

/// <summary>
/// Delegate that constructs a collection from a span of values.
/// </summary>
/// <typeparam name="TKey">The type to be compared within the <typeparamref name="TElement"/>.</typeparam>
/// <typeparam name="TElement">The element type of the span parameters.</typeparam>
/// <typeparam name="TDeclaringType">The type of the value produced by the constructor.</typeparam>
/// <param name="values">The span of values used to create the instance.</param>
/// <param name="options">An optional set of parameters used to create the collection.</param>
/// <returns>A newly constructed collection using the specified values.</returns>
public delegate TDeclaringType ParameterizedCollectionConstructor<TKey, TElement, TDeclaringType>(ReadOnlySpan<TElement> values, in CollectionConstructionOptions<TKey> options = default);

/// <summary>
/// Delegate that appends an element to a mutable enumerable collection.
/// </summary>
/// <typeparam name="TEnumerable">The type of the enumerable collection to append to.</typeparam>
/// <typeparam name="TElement">The type of the element to append.</typeparam>
/// <param name="enumerable">The enumerable collection instance to append the element to.</param>
/// <param name="value">The element to append to the collection.</param>
/// <returns><see langword="true"/> if the append operation was successful, <see langword="false"/> otherwise.</returns>
public delegate bool EnumerableAppender<TEnumerable, TElement>(ref TEnumerable enumerable, TElement value);

/// <summary>
/// Delegate that inserts a key-value pair into a mutable dictionary.
/// </summary>
/// <typeparam name="TDictionary">The type of the dictionary to insert into.</typeparam>
/// <typeparam name="TKey">The type of the key to insert.</typeparam>
/// <typeparam name="TValue">The type of the value to insert.</typeparam>
/// <param name="dictionary">The dictionary instance to insert the key-value pair into.</param>
/// <param name="key">The key to insert.</param>
/// <param name="value">The value to insert.</param>
/// <returns><see langword="true"/> if the insertion was successful, <see langword="false"/> otherwise.</returns>
public delegate bool DictionaryInserter<TDictionary, TKey, TValue>(ref TDictionary dictionary, TKey key, TValue value);

/// <summary>
/// Delegate deconstructing an optional type.
/// </summary>
/// <typeparam name="TOptional">The optional type to deconstruct.</typeparam>
/// <typeparam name="TElement">The value encapsulated by the option type.</typeparam>
/// <param name="optional">The optional value to deconstruct.</param>
/// <param name="value">The value potentially contained in <paramref name="optional"/>.</param>
/// <returns><see langword="true"/> if the <paramref name="optional"/> contains a value, or <see langword="false"/> if it does not.</returns>
public delegate bool OptionDeconstructor<TOptional, TElement>(TOptional? optional, [MaybeNullWhen(false)] out TElement value);
