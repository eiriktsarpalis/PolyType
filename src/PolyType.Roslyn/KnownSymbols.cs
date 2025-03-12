﻿using Microsoft.CodeAnalysis;

namespace PolyType.Roslyn;

/// <summary>
/// Provides a caching layer for common known symbols wrapping a <see cref="Compilation"/> instance.
/// </summary>
/// <param name="compilation">The compilation from which information is being queried.</param>
public class KnownSymbols(Compilation compilation)
{
    /// <summary>
    /// The name of the property on TypeShapeAttribute that stores associated types.
    /// </summary>
    public const string TypeShapeAssociatedTypesPropertyName = "AssociatedTypes";

    /// <summary>
    /// The compilation from which information is being queried.
    /// </summary>
    public Compilation Compilation { get; } = compilation;

    /// <summary>
    /// The assembly symbol for the core library.
    /// </summary>
    public IAssemblySymbol CoreLibAssembly => _CoreLibAssembly ??= Compilation.GetSpecialType(SpecialType.System_Int32).ContainingAssembly;
    private IAssemblySymbol? _CoreLibAssembly;

    /// <summary>
    /// Gets the symbol for TypeShapeExtensionAttribute.
    /// </summary>
    public INamedTypeSymbol TypeShapeExtensionAttribute => _TypeShapeExtensionAttribute ??= Compilation.GetTypeByMetadataName("PolyType.TypeShapeExtensionAttribute") ?? throw new InvalidOperationException("TypeShapeExtensionAttribute not found.");
    private INamedTypeSymbol? _TypeShapeExtensionAttribute;

    /// <summary>
    /// Gets the symbol for AssociatedTypeAttributeAttribute.
    /// </summary>
    public INamedTypeSymbol AssociatedTypeAttributeAttribute => _AssociatedTypeAttributeAttribute ??= Compilation.GetTypeByMetadataName("PolyType.AssociatedTypeAttributeAttribute") ?? throw new InvalidOperationException("AssociatedTypeAttributeAttribute not found.");
    private INamedTypeSymbol? _AssociatedTypeAttributeAttribute;

    /// <summary>
    /// The type symbol for <see cref="System.Reflection.MemberInfo"/>.
    /// </summary>
    public INamedTypeSymbol? MemberInfoType => GetOrResolveType("System.Reflection.MemberInfo", ref _MemberInfoType);
    private Option<INamedTypeSymbol?> _MemberInfoType;

    /// <summary>
    /// The type symbol for <see cref="System.Exception"/>.
    /// </summary>
    public INamedTypeSymbol? ExceptionType => GetOrResolveType("System.Exception", ref _ExceptionType);
    private Option<INamedTypeSymbol?> _ExceptionType;

    /// <summary>
    /// The type symbol for <see cref="System.Threading.Tasks.Task"/>.
    /// </summary>
    public INamedTypeSymbol? TaskType => GetOrResolveType("System.Threading.Tasks.Task", ref _TaskType);
    private Option<INamedTypeSymbol?> _TaskType;

    /// <summary>
    /// The type symbol for <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    public INamedTypeSymbol? IReadOnlyDictionaryOfTKeyTValue => GetOrResolveType("System.Collections.Generic.IReadOnlyDictionary`2", ref _IReadOnlyDictionaryOfTKeyTValue);
    private Option<INamedTypeSymbol?> _IReadOnlyDictionaryOfTKeyTValue;

    /// <summary>
    /// The type symbol for <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    public INamedTypeSymbol? IDictionaryOfTKeyTValue => GetOrResolveType("System.Collections.Generic.IDictionary`2", ref _IDictionaryOfTKeyTValue);
    private Option<INamedTypeSymbol?> _IDictionaryOfTKeyTValue;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.IDictionary"/>.
    /// </summary>
    public INamedTypeSymbol? IDictionary => GetOrResolveType("System.Collections.IDictionary", ref _IDictionary);
    private Option<INamedTypeSymbol?> _IDictionary;

    /// <summary>
    /// The type symbol for <see cref="IEnumerable{T}"/>.
    /// </summary>
    public INamedTypeSymbol IEnumerableOfT => _IEnumerableOfT ??= Compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T);
    private INamedTypeSymbol? _IEnumerableOfT;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.IEnumerable"/>.
    /// </summary>
    public INamedTypeSymbol IEnumerable => _IEnumerable ??= Compilation.GetSpecialType(SpecialType.System_Collections_IEnumerable);
    private INamedTypeSymbol? _IEnumerable;

    /// <summary>
    /// The type symbol for <see cref="Span{T}"/>.
    /// </summary>
    public INamedTypeSymbol? SpanOfT => GetOrResolveType("System.Span`1", ref _SpanOfT);
    private Option<INamedTypeSymbol?> _SpanOfT;

    /// <summary>
    /// The type symbol for <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public INamedTypeSymbol? ReadOnlySpanOfT => GetOrResolveType("System.ReadOnlySpan`1", ref _ReadOnlySpanOfT);
    private Option<INamedTypeSymbol?> _ReadOnlySpanOfT;

    /// <summary>
    /// The type symbol for <see cref="Memory{T}"/>.
    /// </summary>
    public INamedTypeSymbol? MemoryOfT => GetOrResolveType("System.Memory`1", ref _MemoryOfT);
    private Option<INamedTypeSymbol?> _MemoryOfT;

    /// <summary>
    /// The type symbol for <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    public INamedTypeSymbol? ReadOnlyMemoryOfT => GetOrResolveType("System.ReadOnlyMemory`1", ref _ReadOnlyMemoryOfT);
    private Option<INamedTypeSymbol?> _ReadOnlyMemoryOfT;

    /// <summary>
    /// The type symbol for <see cref="List{T}"/>.
    /// </summary>
    public INamedTypeSymbol? ListOfT => GetOrResolveType("System.Collections.Generic.List`1", ref _ListOfT);
    private Option<INamedTypeSymbol?> _ListOfT;

    /// <summary>
    /// The type symbol for <see cref="HashSet{T}"/>.
    /// </summary>
    public INamedTypeSymbol? HashSetOfT => GetOrResolveType("System.Collections.Generic.HashSet`1", ref _HashSetOfT);
    private Option<INamedTypeSymbol?> _HashSetOfT;

    /// <summary>
    /// The type symbol for <see cref="KeyValuePair{TKey, TValue}"/>.
    /// </summary>
    public INamedTypeSymbol? KeyValuePairOfKV => GetOrResolveType("System.Collections.Generic.KeyValuePair`2", ref _KeyValuePairOfKV);
    private Option<INamedTypeSymbol?> _KeyValuePairOfKV;

    /// <summary>
    /// The type symbol for <see cref="Dictionary{TKey, TValue}"/>.
    /// </summary>
    public INamedTypeSymbol? DictionaryOfTKeyTValue => GetOrResolveType("System.Collections.Generic.Dictionary`2", ref _DictionaryOfTKeyTValue);
    private Option<INamedTypeSymbol?> _DictionaryOfTKeyTValue;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.IList"/>.
    /// </summary>
    public INamedTypeSymbol? IList => GetOrResolveType("System.Collections.IList", ref _IList);
    private Option<INamedTypeSymbol?> _IList;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.Immutable.ImmutableArray{T}"/>.
    /// </summary>
    public INamedTypeSymbol? ImmutableArray => GetOrResolveType("System.Collections.Immutable.ImmutableArray`1", ref _ImmutableArray);
    private Option<INamedTypeSymbol?> _ImmutableArray;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.Immutable.ImmutableList{T}"/>.
    /// </summary>
    public INamedTypeSymbol? ImmutableList => GetOrResolveType("System.Collections.Immutable.ImmutableList`1", ref _ImmutableList);
    private Option<INamedTypeSymbol?> _ImmutableList;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.Immutable.ImmutableQueue{T}"/>.
    /// </summary>
    public INamedTypeSymbol? ImmutableQueue => GetOrResolveType("System.Collections.Immutable.ImmutableQueue`1", ref _ImmutableQueue);
    private Option<INamedTypeSymbol?> _ImmutableQueue;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.Immutable.ImmutableStack{T}"/>.
    /// </summary>
    public INamedTypeSymbol? ImmutableStack => GetOrResolveType("System.Collections.Immutable.ImmutableStack`1", ref _ImmutableStack);
    private Option<INamedTypeSymbol?> _ImmutableStack;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.Immutable.ImmutableHashSet{T}"/>.
    /// </summary>
    public INamedTypeSymbol? ImmutableHashSet => GetOrResolveType("System.Collections.Immutable.ImmutableHashSet`1", ref _ImmutableHashSet);
    private Option<INamedTypeSymbol?> _ImmutableHashSet;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.Immutable.ImmutableSortedSet{T}"/>.
    /// </summary>
    public INamedTypeSymbol? ImmutableSortedSet => GetOrResolveType("System.Collections.Immutable.ImmutableSortedSet`1", ref _ImmutableSortedSet);
    private Option<INamedTypeSymbol?> _ImmutableSortedSet;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.Immutable.ImmutableDictionary{TKey, TValue}"/>.
    /// </summary>
    public INamedTypeSymbol? ImmutableDictionary => GetOrResolveType("System.Collections.Immutable.ImmutableDictionary`2", ref _ImmutableDictionary);
    private Option<INamedTypeSymbol?> _ImmutableDictionary;

    /// <summary>
    /// The type symbol for <see cref="System.Collections.Immutable.ImmutableSortedDictionary{TKey, TValue}"/>.
    /// </summary>
    public INamedTypeSymbol? ImmutableSortedDictionary => GetOrResolveType("System.Collections.Immutable.ImmutableSortedDictionary`2", ref _ImmutableSortedDictionary);
    private Option<INamedTypeSymbol?> _ImmutableSortedDictionary;

    /// <summary>
    /// The type symbol for the F# list type.
    /// </summary>
    public INamedTypeSymbol? FSharpList => GetOrResolveType("Microsoft.FSharp.Collections.FSharpList`1", ref _FSharpList);
    private Option<INamedTypeSymbol?> _FSharpList;

    /// <summary>
    /// The type symbol for the F# map type.
    /// </summary>
    public INamedTypeSymbol? FSharpMap => GetOrResolveType("Microsoft.FSharp.Collections.FSharpMap`2", ref _FSharpMap);
    private Option<INamedTypeSymbol?> _FSharpMap;

    /// <summary>
    /// A "simple type" in this context defines a type that is either 
    /// a primitive, string or represents an irreducible value such as Guid or DateTime.
    /// </summary>
    public bool IsSimpleType(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            // Primitive types
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            // CoreLib non-primitives that represent a single value.
            case SpecialType.System_String:
            case SpecialType.System_Decimal:
            case SpecialType.System_DateTime:
                return true;
        }

        return (_simpleTypes ??= CreateSimpleTypes(Compilation)).Contains(type);

        static HashSet<ITypeSymbol> CreateSimpleTypes(Compilation compilation)
        {
            ReadOnlySpan<string> simpleTypeNames =
            [
                "System.Half",
                "System.Int128",
                "System.UInt128",
                "System.Guid",
                "System.DateTimeOffset",
                "System.DateOnly",
                "System.TimeSpan",
                "System.TimeOnly",
                "System.Version",
                "System.Uri",
                "System.Text.Rune",
                "System.Numerics.BigInteger",
            ];

            var simpleTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            foreach (string simpleTypeName in simpleTypeNames)
            {
                INamedTypeSymbol? simpleType = compilation.GetTypeByMetadataName(simpleTypeName);
                if (simpleType is not null)
                {
                    simpleTypes.Add(simpleType);
                }
            }

            return simpleTypes;
        }
    }

    private HashSet<ITypeSymbol>? _simpleTypes;

    /// <summary>
    /// Get or resolve a type by its fully qualified name.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified name of the type to resolve.</param>
    /// <param name="field">A field in which to cache the result for future use.</param>
    /// <returns>The type symbol result or null if not found.</returns>
    protected INamedTypeSymbol? GetOrResolveType(string fullyQualifiedName, ref Option<INamedTypeSymbol?> field)
    {
        if (field.HasValue)
        {
            return field.Value;
        }

        INamedTypeSymbol? type = Compilation.GetTypeByMetadataName(fullyQualifiedName);
        field = new(type);
        return type;
    }

    /// <summary>
    /// Defines a true optional type that supports Some(null) representations.
    /// </summary>
    /// <typeparam name="T">The optional value contained.</typeparam>
    protected readonly struct Option<T>(T value)
    {
        /// <summary>
        /// Indicates whether the option has a value, or <see langword="default" /> otherwise.
        /// </summary>
        public bool HasValue { get; } = true;
        /// <summary>
        /// The value of the option.
        /// </summary>
        public T Value { get; } = value;
    }
}
