#if NET
using System.Diagnostics;
using System.Threading.Tasks;
using PolyType.Abstractions;
using PolyType.Examples.Utilities;
using PolyType.Examples.Validation;
using PolyType.Utilities;

namespace PolyType.Examples.AsyncValidation;

public static partial class AsyncValidator
{
    private sealed class Builder(ITypeShapeFunc self) : TypeShapeVisitor, ITypeShapeFunc
    {
        public AsyncValidator<T>? GetOrAddValidator<T>(ITypeShape<T> shape) => (AsyncValidator<T>?)self.Invoke(shape);

        /// <summary>Recursively looks up or creates a validator for the specified shape.</summary>
        object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state) => typeShape.Accept(this);

        public override object? VisitObject<T>(IObjectTypeShape<T> type, object? state)
        {
            (string, AsyncValidator<T>)[] propertyValidators = type.Properties
                .Where(prop => prop.HasGetter)
                .Select(prop => (prop.Name, Validator: (AsyncValidator<T>?)prop.Accept(this)))
                .Where(prop => prop.Validator is not null)
                .ToArray()!;

            if (propertyValidators.Length == 0)
            {
                return null;
            }

            return new AsyncValidator<T>(async (T? value, List<string> path, List<string> errors) =>
            {
                if (value is null)
                {
                    return;
                }

                foreach ((string name, AsyncValidator<T> propertyValidator) in propertyValidators)
                {
                    path.Add(name);
                    await propertyValidator(value, path, errors).ConfigureAwait(false);
                    path.RemoveAt(path.Count - 1);
                }
            });
        }

        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            // Collect synchronous predicates from ValidationAttribute.
            (Predicate<TPropertyType> Predicate, string ErrorMessage)[]? syncPredicates = property.AttributeProvider
                .GetCustomAttributes<ValidationAttribute>(inherit: true)
                .Select(attr => (Predicate: attr.CreateValidationPredicate<TPropertyType>(), attr.ErrorMessage))
                .Where(pair => pair.Predicate is not null)
                .ToArray()!;

            if (syncPredicates.Length == 0)
            {
                syncPredicates = null;
            }

            // Collect asynchronous predicates from AsyncValidationAttribute.
            (Func<TPropertyType, ValueTask<bool>> Predicate, string ErrorMessage)[]? asyncPredicates = property.AttributeProvider
                .GetCustomAttributes<AsyncValidationAttribute>(inherit: true)
                .Select(attr => (Predicate: attr.CreateAsyncValidationPredicate<TPropertyType>(), attr.ErrorMessage))
                .Where(pair => pair.Predicate is not null)
                .ToArray()!;

            if (asyncPredicates.Length == 0)
            {
                asyncPredicates = null;
            }

            AsyncValidator<TPropertyType>? propertyTypeValidator = GetOrAddValidator(property.PropertyType);

            if (syncPredicates is null && asyncPredicates is null && propertyTypeValidator is null)
            {
                return null;
            }

            Getter<TDeclaringType, TPropertyType> getter = property.GetGetter();
            return new AsyncValidator<TDeclaringType>(async (TDeclaringType? obj, List<string> path, List<string> errors) =>
            {
                DebugExt.Assert(obj is not null);

                TPropertyType propertyValue = getter(ref obj);

                // Run synchronous predicates first (no async overhead).
                if (syncPredicates is not null)
                {
                    foreach ((Predicate<TPropertyType> predicate, string errorMessage) in syncPredicates)
                    {
                        if (!predicate(propertyValue))
                        {
                            errors.Add($"$.{string.Join(".", path)}: {errorMessage}");
                        }
                    }
                }

                // Run asynchronous predicates.
                if (asyncPredicates is not null)
                {
                    foreach ((Func<TPropertyType, ValueTask<bool>> predicate, string errorMessage) in asyncPredicates)
                    {
                        if (!await predicate(propertyValue).ConfigureAwait(false))
                        {
                            errors.Add($"$.{string.Join(".", path)}: {errorMessage}");
                        }
                    }
                }

                // Continue traversal of the property value.
                if (propertyTypeValidator is not null)
                {
                    await propertyTypeValidator(propertyValue, path, errors).ConfigureAwait(false);
                }
            });
        }

        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            AsyncValidator<TValue>? valueValidator = GetOrAddValidator(dictionaryShape.ValueType);
            if (valueValidator is null)
            {
                return null;
            }

            Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getDictionary = dictionaryShape.GetGetDictionary();
            return new AsyncValidator<TDictionary>(async (TDictionary? dict, List<string> path, List<string> errors) =>
            {
                if (dict is null)
                {
                    return;
                }

                foreach (var kvp in getDictionary(dict))
                {
                    path.Add(kvp.Key.ToString()!);
                    await valueValidator(kvp.Value, path, errors).ConfigureAwait(false);
                    path.RemoveAt(path.Count - 1);
                }
            });
        }

        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            AsyncValidator<TElement>? elementValidator = GetOrAddValidator(enumerableShape.ElementType);
            if (elementValidator is null)
            {
                return null;
            }

            Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetPotentiallyBlockingEnumerable();
            return new AsyncValidator<TEnumerable>(async (TEnumerable? enumerable, List<string> path, List<string> errors) =>
            {
                if (enumerable is null)
                {
                    return;
                }

                int i = 0;
                foreach (TElement? e in getEnumerable(enumerable))
                {
                    path.Add($"[{i}]");
                    await elementValidator(e, path, errors).ConfigureAwait(false);
                    path.RemoveAt(path.Count - 1);
                    i++;
                }
            });
        }

        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            AsyncValidator<TElement>? elementValidator = GetOrAddValidator(optionalShape.ElementType);
            if (elementValidator is null)
            {
                return null;
            }

            var deconstructor = optionalShape.GetDeconstructor();
            return new AsyncValidator<TOptional>(async (TOptional? optional, List<string> path, List<string> errors) =>
            {
                if (deconstructor(optional, out TElement? element))
                {
                    await elementValidator(element, path, errors).ConfigureAwait(false);
                }
            });
        }

        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            return null;
        }

        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
        {
            var surrogateValidator = GetOrAddValidator(surrogateShape.SurrogateType);
            var marshaler = surrogateShape.Marshaler;

            return surrogateValidator is null ? null :
                new AsyncValidator<T>((T? value, List<string> path, List<string> errors) =>
                    surrogateValidator(marshaler.Marshal(value), path, errors));
        }

        public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
        {
            var getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
            var baseCaseValidator = (AsyncValidator<TUnion>?)unionShape.BaseType.Accept(this);
            var unionCaseValidators = unionShape.UnionCases
                .Select(caseShape => (AsyncValidator<TUnion>?)caseShape.Accept(this))
                .ToArray();

            if (baseCaseValidator is null && unionCaseValidators.All(v => v is null))
            {
                return null;
            }

            return new AsyncValidator<TUnion>(async (TUnion? value, List<string> path, List<string> errors) =>
            {
                if (value is null)
                {
                    return;
                }

                int caseIndex = getUnionCaseIndex(ref value);
                if (caseIndex < 0)
                {
                    if (baseCaseValidator is not null)
                    {
                        await baseCaseValidator(value, path, errors).ConfigureAwait(false);
                    }

                    return;
                }

                AsyncValidator<TUnion>? caseValidator = unionCaseValidators[caseIndex];
                if (caseValidator is not null)
                {
                    await caseValidator(value, path, errors).ConfigureAwait(false);
                }
            });
        }

        public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null)
        {
            var underlying = (AsyncValidator<TUnionCase>?)unionCaseShape.UnionCaseType.Accept(this);
            if (underlying is null)
            {
                return null;
            }

            var marshaler = unionCaseShape.Marshaler;
            return new AsyncValidator<TUnion>((TUnion? value, List<string> path, List<string> errors) =>
                underlying(marshaler.Unmarshal(value), path, errors));
        }

        public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
        {
            return null;
        }

        /// <summary>
        /// Creates a trivial validator that always succeeds.
        /// </summary>
        public static AsyncValidator<T> CreateNullValidator<T>() => new((T? value, List<string> path, List<string> errors) => default);
    }

    private sealed class DelayedAsyncValidatorFactory : IDelayedValueFactory
    {
        public DelayedValue Create<T>(ITypeShape<T> typeShape) =>
            new DelayedValue<AsyncValidator<T>>(self => (T? value, List<string> path, List<string> errors) => self.Result(value, path, errors));
    }
}
#endif
