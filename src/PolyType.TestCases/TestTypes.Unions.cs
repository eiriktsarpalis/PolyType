using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.FSharp.Core;
using PolyType.Tests.FSharp;

namespace PolyType.Tests;

[GenerateShape]
public partial union CSharpScalarUnion(int, bool, string?);

[GenerateShape]
public partial union CSharpValueUnion(int, DateTimeOffset)
{
    public CSharpValueUnion() : this(123)
    {
    }
}

[GenerateShape]
public partial union CSharpNumericUnion(int, long, string?);

[GenerateShape]
public partial union CSharpAmbiguousNumericUnion(int, bool, long, double);

[GenerateShape]
public partial union CSharpNullableValueUnion(int, int?);

[GenerateShape]
public partial union CSharpRecursiveUnion(bool, CSharpRecursiveUnion);

[GenerateShape]
public partial union CSharpTreeUnion(bool, CSharpTreeUnion[]);

[GenerateShape]
public partial record struct CSharpRecursivePayload(CSharpRecursivePayloadUnion? Next);

[GenerateShape]
public partial union CSharpRecursivePayloadUnion(CSharpRecursivePayload?, bool);

[GenerateShape]
public partial union CSharpMutualUnionA(int, CSharpMutualUnionB[], CSharpMutualUnionB);

[GenerateShape]
public partial union CSharpMutualUnionB(string?, CSharpMutualUnionA);

[GenerateShape]
public partial union CSharpObjectUnion(int, object?);

[GenerateShape]
public partial union CSharpObjectOnlyUnion(object?);

[GenerateShape]
public partial union CSharpInterfaceUnion(IUnion, int);

[GenerateShape]
public partial union CSharpArrayUnion(byte[], int[], Dictionary<string, int>);

[Union, GenerateShape]
public sealed partial class CSharpClassUnion
{
    public CSharpClassUnion(int value) => Value = value;
    public CSharpClassUnion(string? value) => Value = value;
    public object? Value { get; }
}

[Union, GenerateShape]
public sealed partial class CSharpMutableUnion
{
    public CSharpMutableUnion(int value) => Value = value;
    public CSharpMutableUnion(bool value) => Value = value;
    public object? Value { get; set; }
}

[GenerateShape]
public sealed partial class CSharpInterfaceOnlyUnion : IUnion
{
    public CSharpInterfaceOnlyUnion()
    {
    }

    public CSharpInterfaceOnlyUnion(int value) => Value = value;
    public object? Value { get; set; }
}

[Union, GenerateShape]
public readonly partial struct CSharpStructUnion
{
    private readonly bool _isText;
    private readonly int _number;
    private readonly string? _text;

    public CSharpStructUnion(int value)
    {
        _isText = false;
        _number = value;
        _text = null;
    }

    public CSharpStructUnion(string? value)
    {
        _isText = true;
        _number = 0;
        _text = value;
    }

    public object? Value => _isText ? _text : _number;

    public bool HasValue => !_isText || _text is not null;

    public bool TryGetValue(out int value)
    {
        value = _number;
        return !_isText;
    }

    public bool TryGetValue([NotNullWhen(true)] out string? value)
    {
        value = _text;
        return _isText && _text is not null;
    }
}

[Union, GenerateShape]
public readonly partial struct CSharpValueStorageUnion
{
    private readonly bool _isBoolean;
    private readonly int _number;
    private readonly bool _boolean;

    public CSharpValueStorageUnion(int value)
    {
        _isBoolean = false;
        _number = value;
        _boolean = false;
    }

    public CSharpValueStorageUnion(bool value)
    {
        _isBoolean = true;
        _number = 0;
        _boolean = value;
    }

    public Action? OnValueAccess { get; init; }

    public object Value
    {
        get
        {
            OnValueAccess?.Invoke();
            return _isBoolean ? _boolean : _number;
        }
    }

    public bool HasValue => true;

    public bool TryGetValue(out int value)
    {
        value = _number;
        return !_isBoolean;
    }

    public bool TryGetValue(out bool value)
    {
        value = _boolean;
        return _isBoolean;
    }
}

public class CSharpTryGetValueHolder<T>
{
    protected CSharpTryGetValueHolder(object? payload) => Payload = payload;

    public object? Payload { get; set; }
    public bool HasValue => Payload is not null;
    public Action? OnValueAccess { get; set; }
    public Action? OnTryGetValue { get; set; }

    public object? Value
    {
        get
        {
            OnValueAccess?.Invoke();
            return Payload;
        }
    }

    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        OnTryGetValue?.Invoke();
        if (Payload is T result)
        {
            value = result;
            return true;
        }

        value = default!;
        return false;
    }
}

[Union]
public sealed class CSharpTryGetValueUnion<T> : CSharpTryGetValueHolder<T>
{
    public CSharpTryGetValueUnion(T? value) : base(value) { }
    public CSharpTryGetValueUnion(bool value) : base(value) { }

    public bool TryGetValue(ref bool value) => throw new InvalidOperationException("The ref overload must not be called.");
    public bool TryGetValue<TIgnored>(out bool value) => throw new InvalidOperationException("The generic overload must not be called.");
}

[Union, GenerateShape]
public readonly partial struct CSharpInUnion
{
    public CSharpInUnion(in int value) => Value = value;
    public CSharpInUnion(string? value) => Value = value;
    public object? Value { get; }
}

public abstract class CSharpValueHolder
{
    protected CSharpValueHolder(object? value) => Value = value;
    public object? Value { get; }
}

[Union, GenerateShape]
public sealed partial class CSharpInheritedUnion : CSharpValueHolder
{
    public CSharpInheritedUnion(int value) : base(value)
    {
    }

    public CSharpInheritedUnion(string? value) : base(value)
    {
    }
}

public record CSharpAnimal(string Name);
public sealed record CSharpDog(string Name, int BarkVolume) : CSharpAnimal(Name);
public sealed record CSharpCat(string Name) : CSharpAnimal(Name);

[GenerateShape]
public partial union CSharpHierarchyUnion(CSharpAnimal?, CSharpDog, int);

[Union, GenerateShape]
public sealed partial class CSharpConstructorUnion
{
    public CSharpConstructorUnion(CSharpAnimal? value)
        => (Value, ConstructorUsed) = (value, nameof(CSharpAnimal));

    public CSharpConstructorUnion(CSharpDog value)
        => (Value, ConstructorUsed) = (value, nameof(CSharpDog));

    public object? Value { get; }
    public string ConstructorUsed { get; }
    public Action<Type>? OnTryGetValue { get; set; }

    public bool TryGetValue(out CSharpAnimal? value)
    {
        OnTryGetValue?.Invoke(typeof(CSharpAnimal));
        value = (CSharpAnimal?)Value;
        return true;
    }

    public bool TryGetValue([NotNullWhen(true)] out CSharpDog? value)
    {
        OnTryGetValue?.Invoke(typeof(CSharpDog));
        value = Value as CSharpDog;
        return value is not null;
    }

    public static implicit operator CSharpConstructorUnion(CSharpAnimal? value)
        => throw new InvalidOperationException("Union case construction must not invoke conversion operators.");
}

[Union, GenerateShape]
[DerivedTypeShape(typeof(CSharpDerivedWrapper))]
public partial class CSharpHierarchyWrapper
{
    public CSharpHierarchyWrapper()
    {
    }

    public CSharpHierarchyWrapper(int value) => Value = value;
    public CSharpHierarchyWrapper(string? value) => Value = value;
    public object? Value { get; set; }
}

public sealed class CSharpDerivedWrapper : CSharpHierarchyWrapper
{
    public CSharpDerivedWrapper()
    {
    }

    public CSharpDerivedWrapper(int value) : base(value)
    {
    }
}

[GenerateShape]
[DerivedTypeShape(typeof(CSharpRegisteredBaseHierarchy))]
[DerivedTypeShape(typeof(CSharpRegisteredBaseDerived))]
public partial class CSharpRegisteredBaseHierarchy
{
    public int Number { get; set; }
}

public sealed class CSharpRegisteredBaseDerived : CSharpRegisteredBaseHierarchy;

[Union, GenerateShape]
[TypeShape(Kind = TypeShapeKind.Object)]
public sealed partial class CSharpObjectModelUnion
{
    public CSharpObjectModelUnion()
    {
    }

    public CSharpObjectModelUnion(int value) => Value = value;
    public CSharpObjectModelUnion(string? value) => Value = value;
    public object? Value { get; set; }
}

public partial union CSharpGenericUnion<TLeft, TRight>(TLeft, TRight);

#if NET
[Union, GenerateShape]
public sealed partial class CSharpProviderUnion : CSharpProviderUnion.IUnionMembers
{
    private readonly object? _value;

    private CSharpProviderUnion(object? value) => _value = value;

    public interface IUnionMembers
    {
        object? Value { get; }
        static abstract CSharpProviderUnion Create(int value);
        static abstract CSharpProviderUnion Create(string? value);
    }

    object? IUnionMembers.Value => _value;
    static CSharpProviderUnion IUnionMembers.Create(int value) => new(value);
    static CSharpProviderUnion IUnionMembers.Create(string? value) => new(value);

    public static CSharpProviderUnion CreateNumber(int value) => new(value);
    public static CSharpProviderUnion CreateText(string? value) => new(value);
}

[Union, GenerateShape]
public sealed partial class CSharpDefaultProviderUnion : CSharpDefaultProviderUnion.IUnionMembers
{
    private readonly object? _value;

    private CSharpDefaultProviderUnion(object? value) => _value = value;

    public interface IUnionMembers
    {
        object? Value => ((CSharpDefaultProviderUnion)this)._value;
        static virtual CSharpDefaultProviderUnion Create(int value) => new(value);
        static virtual CSharpDefaultProviderUnion Create(string? value) => new(value);
    }

    public static CSharpDefaultProviderUnion CreateNumber(int value) => new(value);
    public static CSharpDefaultProviderUnion CreateText(string? value) => new(value);
}

[Union]
public sealed class CSharpGenericProviderUnion<T> : CSharpGenericProviderUnion<T>.IUnionMembers
{
    private readonly object? _value;

    private CSharpGenericProviderUnion(object? value) => _value = value;

    public interface IUnionMembers
    {
        object? Value { get; }
        static abstract CSharpGenericProviderUnion<T> Create(T value);
        static abstract CSharpGenericProviderUnion<T> Create(string? value);
    }

    object? IUnionMembers.Value => _value;
    static CSharpGenericProviderUnion<T> IUnionMembers.Create(T value) => new(value);
    static CSharpGenericProviderUnion<T> IUnionMembers.Create(string? value) => new(value);

    public static CSharpGenericProviderUnion<T> CreateValue(T value) => new(value);
    public static CSharpGenericProviderUnion<T> CreateText(string? value) => new(value);
}
#endif

[GenerateShape]
public partial union CSharpOptionalUnion(
    bool,
    FSharpValueOption<CSharpOptionalUnion>,
    FSharpValueOption<CSharpTreeUnion>);

[GenerateShape]
[DerivedTypeShape(typeof(Branch))]
public partial record CanonicalUnionTree
{
    public sealed record Branch(GenericFSharpUnion<CanonicalUnionTree> Value) : CanonicalUnionTree;
}

[GenerateShape]
[DerivedTypeShape(typeof(Text), Name = "text")]
public abstract partial record ObjectSurrogateHierarchy
{
    [TypeShape(Marshaler = typeof(Text.Marshaler))]
    public sealed record Text(string? Value) : ObjectSurrogateHierarchy
    {
        public sealed class Marshaler : IMarshaler<Text, object>
        {
            public object? Marshal(Text? value) => value?.Value;
            public Text? Unmarshal(object? value) => value is null ? null : new(value.ToString());
        }
    }
}
