using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace PolyType.Tests;

public sealed class GenericPrivateDefaultConstructor<T>
{
    [ConstructorShape, JsonConstructor]
    private GenericPrivateDefaultConstructor() => Marker = 42;

    public int Marker { get; }
    public T? Value { get; set; }

    public static GenericPrivateDefaultConstructor<T> Create(T value) => new() { Value = value };
}

public sealed class GenericPrivateFixedConstructor<T>
{
    [ConstructorShape, JsonConstructor]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private GenericPrivateFixedConstructor(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must not be negative.");
        }

        Count = count;
    }

    public int Count { get; }
    public T? Value { get; init; }

    public static GenericPrivateFixedConstructor<T> Create(int count, T value) => new(count) { Value = value };
}

public sealed class GenericPrivateConstructorWithInitializers<T>
{
    [ConstructorShape, JsonConstructor]
    private GenericPrivateConstructorWithInitializers(T value, int count)
    {
        Value = value;
        Count = count;
    }

    public T Value { get; }
    public int Count { get; }
    public required string Name { get; set; }
    public string Tag { get; init; } = "default";

    public static GenericPrivateConstructorWithInitializers<T> Create(T value, int count, string name)
        => new(value, count) { Name = name };
}

public sealed class GenericPrivateInitializer<T>
{
    [JsonInclude]
    public T Value { get; private init; } = default!;

    public static GenericPrivateInitializer<T> Create(T value) => new() { Value = value };
}

public sealed class GenericPrivateCompositeMembers<T>
{
    [ConstructorShape, JsonConstructor]
    private GenericPrivateCompositeMembers(List<T[]> values) => Values = values;

    public List<T[]> Values { get; }

    [PropertyShape, JsonInclude]
    private List<T[]> Extra { get; set; } = [];

    public List<T[]> GetExtra() => Extra;

    public static GenericPrivateCompositeMembers<T> Create(List<T[]> values, List<T[]> extra)
        => new(values) { Extra = extra };
}

public struct GenericPrivateConstructorStruct<T>
{
    [ConstructorShape, JsonConstructor]
    private GenericPrivateConstructorStruct(T value)
    {
        Value = value;
        ReadOnly = value;
        InitOnly = default!;
        Label = "initial";
        Field = -1;
    }

    public T Value { get; }

    [PropertyShape, JsonInclude]
    private T InitOnly { get; init; }

    [PropertyShape, JsonInclude]
    private string Label { get; set; }

    [PropertyShape, JsonInclude]
    private readonly T ReadOnly;

    [PropertyShape, JsonInclude]
    private int Field;

    public (T, T, string, int) GetMembers() => (ReadOnly, InitOnly, Label, Field);

    public static GenericPrivateConstructorStruct<T> Create(T value, T initOnly)
        => new(value) { InitOnly = initOnly };
}

public sealed class GenericReadOnlyMembers<T>
{
    [ConstructorShape, JsonConstructor]
    public GenericReadOnlyMembers(T value) => Value = value;

    [PropertyShape, JsonInclude]
    private string Label { get; } = "initial";

    [PropertyShape, JsonInclude]
    private readonly T Value;

    public (T, string) GetMembers() => (Value, Label);
}

public class GenericAccessorBase<TGrand> where TGrand : class
{
    [PropertyShape, JsonInclude]
    private TGrand? GrandValue { get; set; }

    protected void SetGrandValue(TGrand value) => GrandValue = value;
    public TGrand? GetGrandValue() => GrandValue;
}

public class GenericAccessorIntermediate<TValue> : GenericAccessorBase<string> where TValue : struct
{
    [PropertyShape(Name = "BaseValue"), JsonInclude, JsonPropertyName("BaseValue")]
    private TValue Value { get; set; }

    public TValue GetBaseValue() => Value;

    protected void SetValues(TValue value, string grandValue)
    {
        Value = value;
        SetGrandValue(grandValue);
    }

    public static GenericAccessorIntermediate<TValue> Create(TValue value, string grandValue)
    {
        var result = new GenericAccessorIntermediate<TValue>();
        result.SetValues(value, grandValue);
        return result;
    }
}

public sealed class GenericAccessorLeaf : GenericAccessorIntermediate<int>
{
    public string Label { get; init; } = "leaf";

    public static GenericAccessorLeaf CreateLeaf(int value, string grandValue)
    {
        var result = new GenericAccessorLeaf();
        result.SetValues(value, grandValue);
        return result;
    }
}

public sealed class GenericAccessorDifferentArity<TFirst, TSecond> : GenericAccessorBase<TSecond> where TSecond : class
{
    [PropertyShape, JsonInclude]
    private TFirst? First { get; set; }

    public TFirst? GetFirst() => First;

    public static GenericAccessorDifferentArity<TFirst, TSecond> Create(TFirst first, TSecond second)
    {
        var result = new GenericAccessorDifferentArity<TFirst, TSecond> { First = first };
        result.SetGrandValue(second);
        return result;
    }
}

public sealed class ConstrainedGenericAccessor<@class, TCollection>
    where @class : struct
    where TCollection : ICollection<@class>, new()
{
    [PropertyShape, JsonInclude]
    private @class Value { get; set; }

    public TCollection Values { get; init; } = new();
    public string Label { get; init; } = "default";

    public @class GetValue() => Value;

    public static ConstrainedGenericAccessor<@class, TCollection> Create(@class value, TCollection values)
        => new() { Value = value, Values = values };
}

public readonly struct GenericAccessorColor<T> where T : struct
{
    public T A { get; init; }
    public T R { get; init; }
    public T G { get; init; }
    public T B { get; init; }
}

public class GenericAccessorOuter<TOuter>
{
    public sealed class Nested
    {
        [ConstructorShape, JsonConstructor]
        private Nested(TOuter value) => Value = value;

        public TOuter Value { get; }

        [PropertyShape, JsonInclude]
        private TOuter? Extra { get; set; }

        public TOuter? GetExtra() => Extra;
        public static Nested Create(TOuter value, TOuter extra) => new(value) { Extra = extra };
    }

    public sealed class Nested<TInner>
    {
        [ConstructorShape, JsonConstructor]
        private Nested(TOuter value, TInner item)
        {
            Value = value;
            Item = item;
        }

        public TOuter Value { get; }
        public TInner Item { get; }

        [PropertyShape, JsonInclude]
        private TOuter? Extra { get; init; }

        public TOuter? GetExtra() => Extra;
        public static Nested<TInner> Create(TOuter value, TInner item, TOuter extra) => new(value, item) { Extra = extra };
    }
}

/// <summary>Provides separate accessors for getter-only and setter-only override scenarios.</summary>
public class PartialAccessorOverrideBase<T>
{

    /// <summary>Gets or sets the value whose getter is overridden by derived types.</summary>
    [PropertyShape, JsonInclude]
    public virtual T? GetterOverride { get; protected set; }

    /// <summary>Gets or sets the value whose setter is overridden by derived types.</summary>
    [PropertyShape, JsonInclude]
    public virtual T? SetterOverride { protected get; set; }
}

public sealed class PartialAccessorOverrides<T> : PartialAccessorOverrideBase<T>
{
    [ConstructorShape, JsonConstructor]
    public PartialAccessorOverrides(T? getterOverride, T? setterOverride)
    {
        base.GetterOverride = getterOverride;
        base.SetterOverride = setterOverride;
    }

    [PropertyShape, JsonInclude]
    public override T? GetterOverride => base.GetterOverride;

    [PropertyShape, JsonInclude]
    public override T? SetterOverride { set => base.SetterOverride = value; }
}

public sealed class NonGenericPartialAccessorOverrides : PartialAccessorOverrideBase<int>
{
    [ConstructorShape, JsonConstructor]
    public NonGenericPartialAccessorOverrides(int getterOverride, int setterOverride)
    {
        base.GetterOverride = getterOverride;
        base.SetterOverride = setterOverride;
    }

    [PropertyShape, JsonInclude]
    public override int GetterOverride => base.GetterOverride;

    [PropertyShape, JsonInclude]
    public override int SetterOverride { set => base.SetterOverride = value; }
}

public class GenericAccessorService<T> : ITriggerable
{
    public T? Value { get; set; }

    [MethodShape]
    private T Echo(T value) => value;

    [MethodShape]
    private static int Sum(int x, int y) => x + y;

    [EventShape]
    private event Action<int>? Triggered;

    public void Trigger(int value) => Triggered?.Invoke(value);
}

public sealed class InheritedGenericAccessorService : GenericAccessorService<int>;

public struct GenericAccessorServiceStruct<T> : ITriggerable
{
    public T? Value { get; set; }

    [MethodShape]
    private T Echo(T value) => value;

    [EventShape]
    private event Action<int>? Triggered;

    public void Trigger(int value) => Triggered?.Invoke(value);
}

public interface ITriggerable
{
    void Trigger(int x);
}
