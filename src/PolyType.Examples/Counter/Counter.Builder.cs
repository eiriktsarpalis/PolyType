﻿using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using PolyType.Utilities;

namespace PolyType.Examples.Counter;

public static partial class Counter
{
    private sealed class Builder(ITypeShapeFunc self) : TypeShapeVisitor, ITypeShapeFunc
    {
        /// <summary>Recursively looks up or creates a counter for the specified shape.</summary>
        public Func<T?, long> GetOrAddCounter<T>(ITypeShape<T> typeShape) => (Func<T?, long>)self.Invoke(typeShape)!;
        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> shape, object? _) => shape.Accept(this);

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            Func<T, long>[] propertyCounters = type.Properties
                .Where(prop => prop.HasGetter)
                .Select(prop => (Func<T, long>)prop.Accept(this)!)
                .ToArray();

            if (propertyCounters.Length == 0)
            {
                return new Func<T, long>(value => value is null ? 0 : 1);
            }

            return new Func<T, long>(value =>
            {
                if (value is null)
                {
                    return 0;
                }

                long count = 1;
                foreach (Func<T, long> propCounter in propertyCounters)
                {
                    count += propCounter(value);
                }

                return count;
            });
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            Func<TPropertyType, long> propertyTypeCounter = GetOrAddCounter(property.PropertyType);
            return new Func<TDeclaringType, long>(obj => propertyTypeCounter(getter(ref obj)));
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state) => 
            new Func<TEnum, long>(_ => 1);

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            var elementTypeCounter = (Func<TElement, long>)optionalShape.ElementType.Accept(this)!;
            var deconstructor = optionalShape.GetDeconstructor();
            return new Func<TOptional, long>(t => deconstructor(t, out TElement? value) ? elementTypeCounter(value) : 0);
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
        {
            var surrogateCounter = (Func<TSurrogate?, long>)surrogateShape.SurrogateType.Accept(this)!;
            var marshaller = surrogateShape.Marshaller;
            return new Func<T?, long>(t => surrogateCounter(marshaller.ToSurrogate(t)));
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            Func<TEnumerable, IEnumerable<TElement>> enumerableGetter = enumerableShape.GetGetPotentiallyBlockingEnumerable();
            Func<TElement, long> elementTypeCounter = GetOrAddCounter(enumerableShape.ElementType);
            return new Func<TEnumerable, long>(enumerable =>
            {
                if (enumerable is null)
                {
                    return 0;
                }

                long count = 1;
                foreach (TElement element in enumerableGetter(enumerable))
                {
                    count += elementTypeCounter(element);
                }

                return count;
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> dictionaryGetter = dictionaryShape.GetGetDictionary();
            Func<TKey, long> keyTypeCounter = GetOrAddCounter(dictionaryShape.KeyType);
            Func<TValue, long> valueTypeCounter = GetOrAddCounter(dictionaryShape.ValueType);
            return new Func<TDictionary, long>(dict =>
            {
                if (dict is null)
                {
                    return 0;
                }

                long count = 1;
                foreach (KeyValuePair<TKey, TValue> kvp in dictionaryGetter(dict))
                {
                    count += keyTypeCounter(kvp.Key);
                    count += valueTypeCounter(kvp.Value);
                }

                return count;
            });
        }

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
        {
            var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
            var baseCaseCounter = (Func<TUnion, long>)unionShape.BaseType.Accept(this)!;
            var unionCaseCounters = unionShape.UnionCases
                .Select(unionCase => (Func<TUnion, long>)unionCase.Accept(this)!)
                .ToArray();

            return new Func<TUnion, long>(union =>
            {
                if (union is null)
                {
                    return 0;
                }

                int index = getUnionCaseIndex(ref union);
                var derivedCounter = index < 0 ? baseCaseCounter : unionCaseCounters[index];
                return derivedCounter(union);
            });
        }

        public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCase, object? state = null)
        {
            var underlyingCounter = (Func<TUnionCase, long>)unionCase.Type.Accept(this)!;
            return new Func<TUnion, long>(union => underlyingCounter((TUnionCase)union!));
        }
    }

    private sealed class DelayedCounterFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
            new DelayedValue<Func<T?, long>>(self => t => self.Result(t));
    }
}
