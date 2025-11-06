using PolyType.Examples.RandomGenerator;
using PolyType.ReflectionProvider;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Diagnostics;

namespace PolyType.Tests;

public abstract class TypeShapeProviderTests(ProviderUnderTest providerUnderTest)
{
    protected ITypeShapeProvider Provider => providerUnderTest.Provider;

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void TypeShapeReportsExpectedInfo<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        Assert.Equal(typeof(T), shape.Type);
        Assert.Equal(typeof(T), shape.AttributeProvider);
        Assert.Equal(typeof(T).IsRecordType() && testCase is { UsesMarshaler: false, IsUnion: false }, shape is IObjectTypeShape { IsRecordType: true });
        Assert.Equal(typeof(T).IsTupleType() && testCase is { UsesMarshaler: false, IsUnion: false }, shape is IObjectTypeShape { IsTupleType: true });

        Assert.Same(providerUnderTest.Provider, shape.Provider);
        Assert.Same(shape, shape.Provider.GetTypeShape(shape.Type));

        TypeShapeKind expectedKind = GetExpectedTypeKind(testCase);
        Assert.Equal(expectedKind, shape.Kind);

        static TypeShapeKind GetExpectedTypeKind(TestCase<T> testCase)
        {
            if (testCase.CustomKind is { } kind and not TypeShapeKind.None)
            {
                return kind;
            }

            if (testCase.UsesMarshaler)
            {
                return TypeShapeKind.Surrogate;
            }

            if (testCase.IsUnion)
            {
                return TypeShapeKind.Union;
            }

            if (testCase.IsFunctionType)
            {
                return TypeShapeKind.Function;
            }

            if (typeof(T).IsEnum)
            {
                return TypeShapeKind.Enum;
            }
            else if (typeof(T).IsValueType && default(T) is null ||
                typeof(T) is { IsGenericType: true, Name: "FSharpOption`1" or "FSharpValueOption`1", Namespace: "Microsoft.FSharp.Core" })
            {
                return TypeShapeKind.Optional;
            }

            if (typeof(IEnumerable).IsAssignableFrom(typeof(T)) && typeof(T) != typeof(string))
            {
                return typeof(T).GetDictionaryKeyValueTypes() != null
                    ? TypeShapeKind.Dictionary
                    : TypeShapeKind.Enumerable;
            }

            if (typeof(T).GetCompatibleGenericInterface(typeof(IAsyncEnumerable<>)) is not null)
            {
                return TypeShapeKind.Enumerable;
            }

            if (typeof(T).IsMemoryType(out _, out _))
            {
                return TypeShapeKind.Enumerable;
            }

            return TypeShapeKind.Object;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetProperties<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        if (shape is not IObjectTypeShape objectShape || testCase.Value is null)
        {
            return;
        }

        var visitor = new PropertyTestVisitor();
        foreach (IPropertyShape property in objectShape.Properties)
        {
            Assert.Equal(typeof(T), property.DeclaringType.Type);
            property.Accept(visitor, state: testCase.Value);
        }
    }

    private sealed class PropertyTestVisitor : TypeShapeVisitor
    {
        public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> property, object? state)
        {
            TDeclaringType obj = (TDeclaringType)state!;
            TPropertyType propertyType = default!;

            if (property.HasGetter)
            {
                var getter = property.GetGetter();
                Assert.Same(property.GetGetter(), property.GetGetter());
                propertyType = getter(ref obj);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => property.GetGetter());
            }

            if (property.HasSetter)
            {
                var setter = property.GetSetter();
                Assert.Same(property.GetSetter(), property.GetSetter());
                setter(ref obj, propertyType);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => property.GetSetter());
            }

            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetConstructors<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        if (shape is not IObjectTypeShape objectShape)
        {
            return;
        }

        if (testCase.IsTuple)
        {
            Assert.NotNull(objectShape.Constructor);
            Assert.Equal(typeof(T).IsGenericType, objectShape.Constructor.Parameters.Count > 0);
            Assert.All(objectShape.Constructor.Parameters, param => Assert.True(param.IsRequired));
        }

        var visitor = new ConstructorTestVisitor(testCase);
        if (objectShape.Constructor is { } ctor)
        {
            Assert.Equal(typeof(T), ctor.DeclaringType.Type);
            ctor.Accept(visitor, typeof(T));
        }
    }

    private sealed class ConstructorTestVisitor(ITestCase testCase) : TypeShapeVisitor
    {
        public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructor, object? state)
        {
            var expectedType = (Type)state!;
            Assert.Equal(typeof(TDeclaringType), expectedType);

            if (constructor.Parameters.Count == 0)
            {
                Assert.Throws<InvalidOperationException>(() => constructor.GetArgumentStateConstructor());
                Assert.Throws<InvalidOperationException>(() => constructor.GetParameterizedConstructor());

                var defaultCtor = constructor.GetDefaultConstructor();
                Assert.Same(constructor.GetDefaultConstructor(), constructor.GetDefaultConstructor());
                TDeclaringType defaultValue = defaultCtor();
                Assert.Equal(testCase.IsFSharpUnitType, defaultValue is null);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => constructor.GetDefaultConstructor());

                int i = 0;
                var argumentStateCtor = constructor.GetArgumentStateConstructor();
                Assert.NotNull(argumentStateCtor);
                Assert.Same(constructor.GetArgumentStateConstructor(), constructor.GetArgumentStateConstructor());

                TArgumentState argumentState = argumentStateCtor();
                Assert.Equal(argumentState.Count, constructor.Parameters.Count);
                int lastRequiredIndex = constructor.Parameters.LastOrDefault(p => p.IsRequired)?.Position ?? -1;
                Assert.Equal(lastRequiredIndex == -1, argumentState.AreRequiredArgumentsSet);

                foreach (IParameterShape parameter in constructor.Parameters)
                {
                    Assert.Equal(i++, parameter.Position);
                    argumentState = (TArgumentState)parameter.Accept(this, argumentState)!;
                    Assert.Equal(lastRequiredIndex is -1 || parameter.Position >= lastRequiredIndex, argumentState.AreRequiredArgumentsSet);
                }

                Assert.True(argumentState.AreRequiredArgumentsSet);
                var parameterizedCtor = constructor.GetParameterizedConstructor();
                Assert.NotNull(parameterizedCtor);
                Assert.Same(constructor.GetParameterizedConstructor(), constructor.GetParameterizedConstructor());

                if (typeof(TDeclaringType).Assembly == Assembly.GetExecutingAssembly())
                {
                    TDeclaringType value = parameterizedCtor.Invoke(ref argumentState);
                    Assert.NotNull(value);
                }
            }

            return null;
        }

        public override object? VisitParameter<TArgumentState, TParameter>(IParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            RandomGenerator<TParameter> randomGenerator = RandomGenerator.Create(parameter.ParameterType);

            var argState = (TArgumentState)state!;
            var getter = parameter.GetGetter();
            Assert.Same(parameter.GetSetter(), parameter.GetSetter());
            var setter = parameter.GetSetter();
            Assert.Same(parameter.GetSetter(), parameter.GetSetter());

            TParameter? value = parameter.HasDefaultValue ? parameter.DefaultValue : default;
            Assert.Equal(getter(ref argState), value);
            Assert.False(argState.IsArgumentSet(parameter.Position));

            TParameter newValue = randomGenerator.GenerateValue(size: 1000, seed: 50);
            setter(ref argState, newValue);
            Assert.True(argState.IsArgumentSet(parameter.Position));
            Assert.Equal(getter(ref argState), newValue);
            return argState;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetEnumType<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        if (shape.Kind is TypeShapeKind.Enum)
        {
            IEnumTypeShape enumTypeShape = Assert.IsAssignableFrom<IEnumTypeShape>(shape);
            Assert.Equal(typeof(T), enumTypeShape.Type);
            Assert.Equal(typeof(T).GetEnumUnderlyingType(), enumTypeShape.UnderlyingType.Type);
            var visitor = new EnumTestVisitor();
            enumTypeShape.Accept(visitor, state: typeof(T));
        }
        else
        {
            Assert.False(shape is IEnumTypeShape);
        }
    }

    private sealed class EnumTestVisitor : TypeShapeVisitor
    {
        public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state)
        {
            var type = (Type)state!;
            Assert.Equal(typeof(TEnum), type);
            Assert.Equal(typeof(TUnderlying), type.GetEnumUnderlyingType());
            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetOptionalType<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        if (shape.Kind is TypeShapeKind.Optional)
        {
            IOptionalTypeShape optionalTypeType = Assert.IsAssignableFrom<IOptionalTypeShape>(shape);
            Assert.Equal(typeof(T).GetGenericArguments()[0], optionalTypeType.ElementType.Type);
            var visitor = new OptionalTestVisitor();
            optionalTypeType.Accept(visitor, state: typeof(T));
        }
        else
        {
            Assert.False(shape is IOptionalTypeShape);
        }
    }

    private sealed class OptionalTestVisitor : TypeShapeVisitor
    {
        public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state)
        {
            var type = (Type)state!;
            Assert.Equal(typeof(TOptional), type);
            Assert.Equal(typeof(TElement), optionalShape.ElementType.Type);
            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetSurrogateType<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        if (shape.Kind is TypeShapeKind.Surrogate)
        {
            ISurrogateTypeShape surrogateShape = Assert.IsAssignableFrom<ISurrogateTypeShape>(shape);
            var visitor = new SurrogateTestVisitor();
            surrogateShape.Accept(visitor);
        }
        else
        {
            Assert.False(shape is ISurrogateTypeShape);
        }
    }

    private sealed class SurrogateTestVisitor : TypeShapeVisitor
    {
        public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
        {
            Type? MarshalerType = typeof(T).GetCustomAttribute<TypeShapeAttribute>()?.Marshaler;
            Assert.NotNull(MarshalerType);
            if (MarshalerType.IsGenericTypeDefinition)
            {
                MarshalerType = MarshalerType.MakeGenericType(typeof(T).GetGenericArguments());
            }

            Assert.Equal(typeof(T), surrogateShape.Type);
            Assert.Equal(typeof(TSurrogate), surrogateShape.SurrogateType.Type);
            Assert.IsType(MarshalerType, surrogateShape.Marshaler);
            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetUnionType<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        if (shape.Kind is TypeShapeKind.Union)
        {
            IUnionTypeShape<T> unionShape = Assert.IsAssignableFrom<IUnionTypeShape<T>>(shape);
            DerivedTypeShapeAttribute[] attributes = unionShape.AttributeProvider?.GetCustomAttributes(typeof(DerivedTypeShapeAttribute), false)?.Cast<DerivedTypeShapeAttribute>().ToArray() ?? [];
            Assert.NotSame(shape, unionShape.BaseType);
            Assert.NotEmpty(unionShape.UnionCases);
            int i = 0;
            foreach (IUnionCaseShape unionCase in unionShape.UnionCases)
            {
                Assert.True(typeof(T).IsAssignableFrom(unionCase.UnionCaseType.Type));
                Assert.NotNull(unionCase.Name);
                Assert.Equal(i++, unionCase.Index);

                DerivedTypeShapeAttribute? attribute = attributes.FirstOrDefault(a => NormalizeType(a.Type) == NormalizeType(unionCase.UnionCaseType.Type));
                Assert.Equal(attribute is not null && attribute.Tag != -1, unionCase.IsTagSpecified);

                static Type NormalizeType(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            }

            Getter<T, int> unionCaseIndexGetter = unionShape.GetGetUnionCaseIndex();
            Assert.NotNull(unionCaseIndexGetter);
            if (testCase.Value is { } value)
            {
                int index = unionCaseIndexGetter(ref value);
                if (index >= 0)
                {
                    var matchingCase = unionShape.UnionCases[index];
                    Assert.True(matchingCase.UnionCaseType.Type.IsAssignableFrom(value!.GetType()));
                }
            }
        }
        else
        {
            Assert.False(shape is IUnionTypeShape);
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetDictionaryType<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        if (shape.Kind is TypeShapeKind.Dictionary)
        {
            IDictionaryTypeShape dictionaryType = Assert.IsAssignableFrom<IDictionaryTypeShape>(shape);
            Assert.Equal(typeof(T), dictionaryType.Type);

            Type[]? keyValueTypes = typeof(T).GetDictionaryKeyValueTypes();
            Assert.NotNull(keyValueTypes);
            Assert.Equal(keyValueTypes[0], dictionaryType.KeyType.Type);
            Assert.Equal(keyValueTypes[1], dictionaryType.ValueType.Type);

            var visitor = new DictionaryTestVisitor(providerUnderTest.Kind);
            dictionaryType.Accept(visitor, testCase);
        }
        else
        {
            Assert.False(shape is IDictionaryTypeShape);
        }
    }

    private sealed class DictionaryTestVisitor(ProviderKind providerKind) : TypeShapeVisitor
    {
        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            TDictionary dictionary;
            var testCase = (TestCase<TDictionary>)state!;
            RandomGenerator<TKey> keyGenerator = RandomGenerator.Create(dictionaryShape.KeyType);
            var getter = dictionaryShape.GetGetDictionary();

            if (dictionaryShape.ConstructionStrategy is CollectionConstructionStrategy.Mutable)
            {
                var defaultCtor = dictionaryShape.GetDefaultConstructor();
                Assert.Same(dictionaryShape.GetDefaultConstructor(), dictionaryShape.GetDefaultConstructor());

                ReadOnlySpan<DictionaryInsertionMode> allInsertionModes = [
                    DictionaryInsertionMode.None,
                    DictionaryInsertionMode.Overwrite,
                    DictionaryInsertionMode.Discard,
                    DictionaryInsertionMode.Throw
                ];

                Assert.NotEqual(DictionaryInsertionMode.None, dictionaryShape.AvailableInsertionModes);

                foreach (var insertionMode in allInsertionModes)
                {
                    if ((dictionaryShape.AvailableInsertionModes & insertionMode) == insertionMode)
                    {
                        var inserter = dictionaryShape.GetInserter(insertionMode);
                        Assert.Same(dictionaryShape.GetInserter(insertionMode), inserter);
                        dictionary = defaultCtor();
                        Assert.Empty(getter(dictionary));
                        TKey key = keyGenerator.GenerateValue(size: 1000, seed: 42);
                        Assert.True(inserter(ref dictionary, key, default!));
                        Assert.Single(getter(dictionary));

                        switch (insertionMode)
                        {
                            case DictionaryInsertionMode.Throw:
                                Assert.Throws<ArgumentException>(() => inserter(ref dictionary, key, default!));
                                break;
                            case DictionaryInsertionMode.Discard:
                                Assert.False(inserter(ref dictionary, key, default!));
                                break;
                            case DictionaryInsertionMode.Overwrite or DictionaryInsertionMode.None:
                                Assert.True(inserter(ref dictionary, key, default!));
                                break;
                        }

                        Assert.Single(getter(dictionary));
                    }
                    else
                    {
                        Assert.Throws<ArgumentOutOfRangeException>(() => dictionaryShape.GetInserter(insertionMode));
                    }
                }
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetDefaultConstructor());
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetInserter());
            }

            if (dictionaryShape.ConstructionStrategy is CollectionConstructionStrategy.Parameterized)
            {
                var spanCtor = dictionaryShape.GetParameterizedConstructor();
                Assert.Same(dictionaryShape.GetParameterizedConstructor(), dictionaryShape.GetParameterizedConstructor());

                var values = keyGenerator.GenerateValues(seed: 42)
                    .Select(k => new KeyValuePair<TKey, TValue>(k, default!))
                    .Take(10)
                    .ToArray();

                if (testCase.UsesSpanConstructor && providerKind is ProviderKind.ReflectionNoEmit)
                {
                    Assert.Throws<NotSupportedException>(() => spanCtor(values));
                }
                else
                {
                    dictionary = spanCtor(values);
                    Assert.Equal(10, getter(dictionary).Count);
                }
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetParameterizedConstructor());
            }

            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetEnumerableType<T>(TestCase<T> testCase)
    {
        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        if (shape.Kind is TypeShapeKind.Enumerable)
        {
            IEnumerableTypeShape enumerableTypeType = Assert.IsAssignableFrom<IEnumerableTypeShape>(shape);
            Assert.Equal(typeof(T), enumerableTypeType.Type);
            Assert.Equal(testCase.IsSet, enumerableTypeType.IsSetType);

            if (typeof(T).GetCompatibleGenericInterface(typeof(IEnumerable<>)) is { } enumerableImplementation)
            {
                Assert.Equal(enumerableImplementation.GetGenericArguments()[0], enumerableTypeType.ElementType.Type);
                Assert.Equal(1, enumerableTypeType.Rank);
                Assert.False(enumerableTypeType.IsAsyncEnumerable);
            }
            else if (typeof(T).IsArray)
            {
                Assert.Equal(typeof(T).GetElementType(), enumerableTypeType.ElementType.Type);
                Assert.Equal(typeof(T).GetArrayRank(), enumerableTypeType.Rank);
                Assert.False(enumerableTypeType.IsAsyncEnumerable);
            }
            else if (typeof(IEnumerable).IsAssignableFrom(typeof(T)))
            {
                Assert.Equal(typeof(object), enumerableTypeType.ElementType.Type);
                Assert.Equal(1, enumerableTypeType.Rank);
                Assert.False(enumerableTypeType.IsAsyncEnumerable);
            }
            else if (typeof(T).GetCompatibleGenericInterface(typeof(IAsyncEnumerable<>)) is { } asyncEnumerableImplementation)
            {
                Assert.Equal(asyncEnumerableImplementation.GetGenericArguments()[0], enumerableTypeType.ElementType.Type);
                Assert.Equal(1, enumerableTypeType.Rank);
                Assert.True(enumerableTypeType.IsAsyncEnumerable);
            }
            else if (typeof(T).IsMemoryType(out Type? elementType, out _))
            {
                Assert.Equal(elementType, enumerableTypeType.ElementType.Type);
                Assert.Equal(1, enumerableTypeType.Rank);
                Assert.False(enumerableTypeType.IsAsyncEnumerable);
            }
            else
            {
                Assert.Fail($"Unexpected enumerable type: {typeof(T)}");
            }

            var visitor = new EnumerableTestVisitor(providerUnderTest.Kind);
            enumerableTypeType.Accept(visitor, state: testCase);
        }
        else
        {
            Assert.False(shape is IEnumerableTypeShape);
        }
    }

    private sealed class EnumerableTestVisitor(ProviderKind providerKind) : TypeShapeVisitor
    {
        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            TEnumerable enumerable;
            RandomGenerator<TElement> elementGenerator = RandomGenerator.Create(enumerableShape.ElementType);
            var testCase = (TestCase<TEnumerable>)state!;

            var getter = enumerableShape.GetGetEnumerable();

            if (enumerableShape.IsAsyncEnumerable)
            {
                Type targetAsyncEnumerable = typeof(IAsyncEnumerable<>).MakeGenericType(enumerableShape.ElementType.Type);
                Assert.True(targetAsyncEnumerable.IsAssignableFrom(enumerableShape.Type));
                Assert.Throws<InvalidOperationException>(() => getter(testCase.Value!));
                return null;
            }
            
            if (enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Mutable)
            {
                var defaultCtor = enumerableShape.GetDefaultConstructor();
                var appender = enumerableShape.GetAppender();
                Assert.Same(enumerableShape.GetDefaultConstructor(), enumerableShape.GetDefaultConstructor());
                Assert.Same(enumerableShape.GetAppender(), enumerableShape.GetAppender());

                enumerable = defaultCtor();
                Assert.Empty(getter(enumerable));

                TElement newElement = elementGenerator.GenerateValue(size: 1000, seed: 42);
                Assert.True(appender(ref enumerable, newElement));
                Assert.Single(getter(enumerable));

                if (testCase.IsSet)
                {
                    Assert.False(appender(ref enumerable, newElement));
                    Assert.Single(getter(enumerable));
                }
                else
                {
                    Assert.True(appender(ref enumerable, newElement));
                    Assert.Equal(2, getter(enumerable).Count());
                }
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetDefaultConstructor());
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetAppender());
            }

            if (enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Parameterized)
            {
                var spanCtor = enumerableShape.GetParameterizedConstructor();
                Assert.Same(enumerableShape.GetParameterizedConstructor(), enumerableShape.GetParameterizedConstructor());

                var values = elementGenerator.GenerateValues(seed: 42).Take(10).ToArray();
                if (testCase.UsesSpanConstructor && providerKind is ProviderKind.ReflectionNoEmit)
                {
                    Assert.Throws<NotSupportedException>(() => spanCtor(values));
                }
                else
                {
                    enumerable = spanCtor(values);
                    Assert.Equal(10, getter(enumerable).Count());
                }
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetParameterizedConstructor());
            }

            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void GetFunctionType<T>(TestCase<T> testCase)
    {
        if (testCase.Value is null)
        {
            return;
        }

        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        if (shape.Kind is TypeShapeKind.Function)
        {
            Assert.True(testCase.IsFunctionType);
            IFunctionTypeShape functionShape = Assert.IsAssignableFrom<IFunctionTypeShape>(shape);
            Assert.Equal(typeof(T), functionShape.Type);
            (ParameterInfo[] parameterInfos, Type returnType, Type? effectiveReturnType) = GetMethodParameters();

            Assert.Equal(effectiveReturnType is null, functionShape.IsVoidLike);
            Assert.Equal(returnType.IsAsyncType(), functionShape.IsAsync);
            Assert.Equal(effectiveReturnType ?? typeof(Unit), functionShape.ReturnType.Type);
            Assert.Equal(parameterInfos.Length, functionShape.Parameters.Count);

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                IParameterShape paramShape = functionShape.Parameters[i];
                ParameterInfo actualParameter = parameterInfos[i];
                ParameterShapeAttribute? shapeAttr = actualParameter.GetCustomAttribute<ParameterShapeAttribute>();

                Assert.Equal(i, paramShape.Position);
                Assert.Equal(actualParameter.GetEffectiveParameterType(), paramShape.ParameterType.Type);
                string expectedName = testCase.IsFSharpFunctionType ? $"arg{i + 1}" : shapeAttr?.Name ?? actualParameter.Name!;
                Assert.Equal(expectedName, paramShape.Name);

                bool hasDefaultValue = actualParameter.TryGetDefaultValueNormalized(out object? defaultValue);
                Assert.Equal(hasDefaultValue, paramShape.HasDefaultValue);
                Assert.Equal(defaultValue, paramShape.DefaultValue);
                Assert.Equal(shapeAttr?.IsRequiredSpecified() is true ? shapeAttr.IsRequired : !hasDefaultValue, paramShape.IsRequired);
                Assert.Equal(ParameterKind.MethodParameter, paramShape.Kind);
                Assert.True(paramShape.IsPublic);

                ParameterInfo paramInfo = Assert.IsAssignableFrom<ParameterInfo>(paramShape.AttributeProvider);
                Assert.Equal(actualParameter.Position, paramInfo.Position);
                Assert.Equal(actualParameter.Name, paramInfo.Name);
                Assert.Equal(actualParameter.ParameterType, paramInfo.ParameterType);
            }

            var visitor = new FunctionTestVisitor(providerUnderTest, testCase.IsFSharpFunctionType);
            functionShape.Accept(visitor, state: testCase.Value);
        }
        else
        {
            Assert.False(shape is IFunctionTypeShape);
            Assert.False(testCase.IsFunctionType);
        }

        (ParameterInfo[] Parameters, Type ReturnType, Type? EffectiveReturnType) GetMethodParameters()
        {
            MethodInfo invokeMethod = typeof(T).GetMethod("Invoke")!;
            ParameterInfo[] parameterInfos = invokeMethod.GetParameters();
            if (testCase.IsFSharpFunctionType)
            {
                List<ParameterInfo> uncurriedParams = [parameterInfos[0]];
                while (invokeMethod.ReturnType is { Name: "FSharpFunc`2", Namespace: "Microsoft.FSharp.Core" })
                {
                    invokeMethod = invokeMethod.ReturnType.GetMethod("Invoke")!;
                    parameterInfos = invokeMethod.GetParameters();
                    uncurriedParams.Add(parameterInfos[0]);
                }
                
                if (uncurriedParams.Count == 1 && uncurriedParams[0].ParameterType is { Name: "Unit", Namespace: "Microsoft.FSharp.Core" })
                {
                    uncurriedParams.Clear();
                }

                Type? returnType = invokeMethod.GetEffectiveReturnType();
                if (returnType is null or { Name: "Unit", Namespace: "Microsoft.FSharp.Core" })
                {
                    returnType = null;
                }

                return (uncurriedParams.ToArray(), invokeMethod.ReturnType, returnType);
            }

            return (parameterInfos, invokeMethod.ReturnType, invokeMethod.GetEffectiveReturnType());
        }
    }

    private sealed class FunctionTestVisitor(ProviderUnderTest providerUnderTest, bool isFsharpFunc) : TypeShapeVisitor
    {
        public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
        {
            Assert.Equal(typeof(TFunction), functionShape.Type);
            Assert.Equal(typeof(TResult), functionShape.ReturnType.Type);
            var func = Assert.IsType<TFunction>(state, exactMatch: false);

            int i = 0;
            var argumentStateCtor = functionShape.GetArgumentStateConstructor();
            Assert.NotNull(argumentStateCtor);
            Assert.Same(functionShape.GetArgumentStateConstructor(), functionShape.GetArgumentStateConstructor());

            TArgumentState argumentState = argumentStateCtor();
            Assert.Equal(argumentState.Count, functionShape.Parameters.Count);
            int lastRequiredIndex = functionShape.Parameters.LastOrDefault(p => p.IsRequired)?.Position ?? -1;
            Assert.Equal(lastRequiredIndex == -1, argumentState.AreRequiredArgumentsSet);

            foreach (IParameterShape parameter in functionShape.Parameters)
            {
                Assert.Equal(i++, parameter.Position);
                argumentState = (TArgumentState)parameter.Accept(this, argumentState)!;
                Assert.Equal(lastRequiredIndex is -1 || parameter.Position >= lastRequiredIndex, argumentState.AreRequiredArgumentsSet);
            }

            Assert.True(argumentState.AreRequiredArgumentsSet);
            var functionInvoker = functionShape.GetFunctionInvoker();
            Assert.NotNull(functionInvoker);
            Assert.Same(functionShape.GetFunctionInvoker(), functionShape.GetFunctionInvoker());

            TResult result = functionInvoker.Invoke(ref func, ref argumentState).Result;
            Assert.Equal(functionShape.IsVoidLike, result is Unit);

            // FromDelegate/FromAsyncDelegate round-trip test
            RefFunc<TArgumentState, TResult> wrappedInvoker = (ref TArgumentState arg) =>
            {
                Assert.True(arg.AreRequiredArgumentsSet);
                return functionInvoker(ref func, ref arg).GetAwaiter().GetResult();
            };

            RefFunc<TArgumentState, ValueTask<TResult>> wrappedInvokerAsync = (ref TArgumentState arg) =>
            {
                Assert.True(arg.AreRequiredArgumentsSet);
                return functionInvoker(ref func, ref arg);
            };

            if (functionShape.IsAsync)
            {
                Assert.Throws<InvalidOperationException>(() => functionShape.FromDelegate(wrappedInvoker));
                if (providerUnderTest.Kind is ProviderKind.ReflectionNoEmit || isFsharpFunc)
                {
                    Assert.Throws<NotSupportedException>(() => functionShape.FromAsyncDelegate(wrappedInvokerAsync));
                }
                else
                {
                    TFunction wrapped = functionShape.FromAsyncDelegate(wrappedInvokerAsync);
                    TResult newResult = functionInvoker.Invoke(ref wrapped, ref argumentState).GetAwaiter().GetResult();
                    Assert.Equal(result, newResult);
                }
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => functionShape.FromAsyncDelegate(wrappedInvokerAsync));
                if (providerUnderTest.Kind is ProviderKind.ReflectionNoEmit || isFsharpFunc)
                {
                    Assert.Throws<NotSupportedException>(() => functionShape.FromDelegate(wrappedInvoker));
                }
                else
                {
                    TFunction wrapped = functionShape.FromDelegate(wrappedInvoker);
                    TResult newResult = functionInvoker.Invoke(ref wrapped, ref argumentState).Result;
                    Assert.Equal(result, newResult);
                }
            }

            return null;
        }

        public override object? VisitParameter<TArgumentState, TParameter>(IParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            RandomGenerator<TParameter> randomGenerator = RandomGenerator.Create(parameter.ParameterType);

            var argState = (TArgumentState)state!;
            var getter = parameter.GetGetter();
            Assert.Same(parameter.GetSetter(), parameter.GetSetter());
            var setter = parameter.GetSetter();
            Assert.Same(parameter.GetSetter(), parameter.GetSetter());

            TParameter? value = parameter.HasDefaultValue ? parameter.DefaultValue : default;
            Assert.Equal(getter(ref argState), value);
            Assert.False(argState.IsArgumentSet(parameter.Position));

            TParameter newValue = randomGenerator.GenerateValue(size: 1000, seed: 50);
            setter(ref argState, newValue);
            Assert.True(argState.IsArgumentSet(parameter.Position));
            Assert.Equal(getter(ref argState), newValue);
            return argState;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void ReturnsExpectedAttributeProviders<T>(TestCase<T> testCase)
    {
        if (testCase.IsTuple)
        {
            return; // tuples don't report attribute metadata.
        }

        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        Assert.Equal(typeof(T), shape.AttributeProvider);

        if (shape is not IObjectTypeShape objectShape)
        {
            return;
        }

        int pos = 0;
        foreach (IPropertyShape property in objectShape.Properties)
        {
            MemberInfo attributeProvider = Assert.IsAssignableFrom<MemberInfo>(property.AttributeProvider);
            PropertyShapeAttribute? attr = attributeProvider.GetCustomAttribute<PropertyShapeAttribute>();
            Assert.Equal(pos++, property.Position);

            if (property.IsField)
            {
                FieldInfo fieldInfo = Assert.IsAssignableFrom<FieldInfo>(attributeProvider);
                Assert.True(fieldInfo.DeclaringType!.IsAssignableFrom(typeof(T)));
                Assert.Equal(attr?.Name ?? fieldInfo.Name, property.Name);
                Assert.Equal(property.PropertyType.Type, fieldInfo.FieldType);
                Assert.True(property.HasGetter);
                Assert.Equal(!fieldInfo.IsInitOnly, property.HasSetter);
                Assert.Equal(fieldInfo.IsPublic, property.IsGetterPublic);
                Assert.Equal(property.HasSetter && fieldInfo.IsPublic, property.IsSetterPublic);
            }
            else
            {
                PropertyInfo propertyInfo = Assert.IsAssignableFrom<PropertyInfo>(attributeProvider);
                PropertyInfo basePropertyInfo = propertyInfo.GetBaseDefinition();
                Assert.True(propertyInfo.DeclaringType!.IsAssignableFrom(typeof(T)));
                Assert.Equal(attr?.Name ?? propertyInfo.Name, property.Name);
                Assert.Equal(property.PropertyType.Type, propertyInfo.PropertyType);
                Assert.True(!property.HasGetter || basePropertyInfo.CanRead);
                Assert.True(!property.HasSetter || basePropertyInfo.CanWrite);
                Assert.Equal(property.HasGetter && basePropertyInfo.GetMethod!.IsPublic, property.IsGetterPublic);
                Assert.Equal(property.HasSetter && basePropertyInfo.SetMethod!.IsPublic, property.IsSetterPublic);
            }
        }

        if (objectShape.Constructor is { AttributeProvider: not null } constructor)
        {
            MethodBase ctorInfo = Assert.IsAssignableFrom<MethodBase>(constructor.AttributeProvider);
            Assert.True(ctorInfo is MethodInfo { IsStatic: true } or ConstructorInfo);
            Assert.True(typeof(T).IsAssignableFrom(ctorInfo is MethodInfo m ? m.ReturnType : ctorInfo.DeclaringType));
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            Assert.True(parameters.Length <= constructor.Parameters.Count);
            Assert.Equal(ctorInfo.IsPublic, constructor.IsPublic);
            bool hasSetsRequiredMembersAttribute = ctorInfo.SetsRequiredMembers();

            int i = 0;
            foreach (IParameterShape ctorParam in constructor.Parameters)
            {
                if (i < parameters.Length)
                {
                    ParameterInfo actualParameter = parameters[i];
                    ParameterShapeAttribute? shapeAttr = actualParameter.GetCustomAttribute<ParameterShapeAttribute>();
                    string expectedName =
                        // 1. parameter attribute name
                        shapeAttr?.Name
                        // 2. property name picked up from matching parameter
                        ?? typeof(T).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .FirstOrDefault(m => JsonNamingPolicy.CamelCase.ConvertName(m.Name) == actualParameter.Name)
                            ?.GetCustomAttribute<PropertyShapeAttribute>()
                            ?.Name
                        // 3. the actual parameter name.
                        ?? actualParameter.Name!;

                    Assert.Equal(actualParameter.Position, ctorParam.Position);
                    Assert.Equal(actualParameter.GetEffectiveParameterType(), ctorParam.ParameterType.Type);
                    Assert.Equal(expectedName, ctorParam.Name, CommonHelpers.CamelCaseInvariantComparer.Instance);

                    bool hasDefaultValue = actualParameter.TryGetDefaultValueNormalized(out object? defaultValue);
                    Assert.Equal(hasDefaultValue, ctorParam.HasDefaultValue);
                    Assert.Equal(defaultValue, ctorParam.DefaultValue);
                    Assert.Equal(shapeAttr?.IsRequiredSpecified() is true ? shapeAttr.IsRequired : !hasDefaultValue, ctorParam.IsRequired);
                    Assert.Equal(ParameterKind.MethodParameter, ctorParam.Kind);
                    Assert.True(ctorParam.IsPublic);

                    ParameterInfo paramInfo = Assert.IsAssignableFrom<ParameterInfo>(ctorParam.AttributeProvider);
                    Assert.Equal(actualParameter.Position, paramInfo.Position);
                    Assert.Equal(actualParameter.Name, paramInfo.Name);
                    Assert.Equal(actualParameter.ParameterType, paramInfo.ParameterType);
                }
                else
                {
                    MemberInfo memberInfo = Assert.IsAssignableFrom<MemberInfo>(ctorParam.AttributeProvider);
                    Assert.True(memberInfo is PropertyInfo or FieldInfo);

                    PropertyShapeAttribute? attr = memberInfo.GetCustomAttribute<PropertyShapeAttribute>();
                    Assert.True(memberInfo.DeclaringType!.IsAssignableFrom(typeof(T)));
                    Assert.Equal(attr?.Name ?? memberInfo.Name, ctorParam.Name);
                    Assert.False(ctorParam.HasDefaultValue);
                    Assert.Equal(i, ctorParam.Position);
                    Assert.False(ctorParam.HasDefaultValue);
                    Assert.Null(ctorParam.DefaultValue);
                    Assert.Equal(attr?.IsRequiredSpecified() is true ? attr.IsRequired : (!hasSetsRequiredMembersAttribute && memberInfo.IsRequired()), ctorParam.IsRequired);
                    Assert.Equal(ParameterKind.MemberInitializer, ctorParam.Kind);

                    if (memberInfo is PropertyInfo p)
                    {
                        Assert.Equal(p.PropertyType, ctorParam.ParameterType.Type);
                        Assert.NotNull(p.GetBaseDefinition().SetMethod);
                        Assert.Equal(p.SetMethod?.IsPublic, ctorParam.IsPublic);
                    }
                    else if (memberInfo is FieldInfo f)
                    {
                        Assert.Equal(f.FieldType, ctorParam.ParameterType.Type);
                        Assert.False(f.IsInitOnly);
                        Assert.Equal(f.IsPublic, ctorParam.IsPublic);
                    }
                }

                i++;
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void ReturnsExpectedNullabilityAnnotations<T>(TestCase<T> testCase)
    {
        if (testCase.IsTuple)
        {
            return; // tuples don't report attribute metadata.
        }

        if (ReflectionHelpers.IsMonoRuntime && typeof(T) is { IsGenericType: true, IsValueType: true })
        {
            return; // Mono does not correctly resolve nullable annotations for generic structs.
        }

        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);

        if (shape is not IObjectTypeShape objectShape)
        {
            return;
        }

        NullabilityInfoContext? nullabilityCtx = providerUnderTest.ResolvesNullableAnnotations ? new() : null;

        foreach (IPropertyShape property in objectShape.Properties)
        {
            MemberInfo memberInfo = Assert.IsAssignableFrom<MemberInfo>(property.AttributeProvider);

            memberInfo.ResolveNullableAnnotation(nullabilityCtx, out bool isGetterNonNullable, out bool isSetterNonNullable);
            Assert.Equal(property.HasGetter && isGetterNonNullable, property.IsGetterNonNullable);
            Assert.Equal(property.HasSetter && isSetterNonNullable, property.IsSetterNonNullable);
        }

        if (objectShape.Constructor is { AttributeProvider: not null } constructor)
        {
            MethodBase ctorInfo = Assert.IsAssignableFrom<MethodBase>(constructor.AttributeProvider);
            ParameterInfo[] parameters = ctorInfo.GetParameters();
            Assert.True(parameters.Length <= constructor.Parameters.Count);

            foreach (IParameterShape ctorParam in constructor.Parameters)
            {
                if (ctorParam.AttributeProvider is ParameterInfo pInfo)
                {
                    bool isNonNullableReferenceType = pInfo.IsNonNullableAnnotation(nullabilityCtx);
                    Assert.Equal(isNonNullableReferenceType, ctorParam.IsNonNullable);
                }
                else
                {
                    MemberInfo memberInfo = Assert.IsAssignableFrom<MemberInfo>(ctorParam.AttributeProvider);
                    memberInfo.ResolveNullableAnnotation(nullabilityCtx, out _, out bool isSetterNonNullable);
                    Assert.Equal(isSetterNonNullable, ctorParam.IsNonNullable);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void TestMethodShapes<T>(TestCase<T> testCase)
    {
        if (testCase.Value is null)
        {
            return;
        }

        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        Assert.NotNull(shape.Methods);
        Assert.Equal(typeof(T).GetExpectedMethodShapeCount(), shape.Methods.Count);

        foreach (IMethodShape method in shape.Methods)
        {
            Assert.Equal(typeof(T), method.DeclaringType.Type);
            var methodInfo = Assert.IsType<MethodInfo>(method.AttributeProvider, exactMatch: false);
            var methodShapeAttribute = methodInfo.GetCustomAttribute<MethodShapeAttribute>();
            Type? effectiveReturnType = methodInfo.GetEffectiveReturnType();

            Assert.Equal(methodInfo.IsStatic, method.IsStatic);
            Assert.Equal(methodInfo.IsPublic, method.IsPublic);
            Assert.Equal(effectiveReturnType is null, method.IsVoidLike);
            Assert.Equal(methodInfo.ReturnType.IsAsyncType(), method.IsAsync);
            Assert.Equal(methodShapeAttribute?.Name ?? methodInfo.Name, method.Name);
            Assert.Equal(effectiveReturnType ?? typeof(Unit), method.ReturnType.Type);

            ParameterInfo[] parameters = methodInfo.GetParameters();
            Assert.Equal(parameters.Length, method.Parameters.Count);

            for (int i = 0; i < parameters.Length; i++)
            {
                IParameterShape paramShape = method.Parameters[i];
                ParameterInfo actualParameter = parameters[i];
                ParameterShapeAttribute? shapeAttr = actualParameter.GetCustomAttribute<ParameterShapeAttribute>();

                Assert.Equal(actualParameter.Position, paramShape.Position);
                Assert.Equal(actualParameter.GetEffectiveParameterType(), paramShape.ParameterType.Type);
                Assert.Equal(shapeAttr?.Name ?? actualParameter.Name, paramShape.Name);

                bool hasDefaultValue = actualParameter.TryGetDefaultValueNormalized(out object? defaultValue);
                Assert.Equal(hasDefaultValue, paramShape.HasDefaultValue);
                Assert.Equal(defaultValue, paramShape.DefaultValue);
                Assert.Equal(shapeAttr?.IsRequiredSpecified() is true ? shapeAttr.IsRequired : !hasDefaultValue, paramShape.IsRequired);
                Assert.Equal(ParameterKind.MethodParameter, paramShape.Kind);
                Assert.True(paramShape.IsPublic);

                ParameterInfo paramInfo = Assert.IsAssignableFrom<ParameterInfo>(paramShape.AttributeProvider);
                Assert.Equal(actualParameter.Position, paramInfo.Position);
                Assert.Equal(actualParameter.Name, paramInfo.Name);
                Assert.Equal(actualParameter.ParameterType, paramInfo.ParameterType);
            }

            MethodTestVisitor visitor = new(providerUnderTest);
            method.Accept(visitor, state: testCase.Value);
        }
    }

    private sealed class MethodTestVisitor(ProviderUnderTest providerUnderTest) : TypeShapeVisitor
    {
        public override object? VisitMethod<TDeclaringType, TArgumentState, TResult>(IMethodShape<TDeclaringType, TArgumentState, TResult> methodShape, object? state)
        {
            Assert.Equal(typeof(TDeclaringType), methodShape.DeclaringType.Type);
            Assert.Equal(typeof(TResult), methodShape.ReturnType.Type);
            TDeclaringType? instance = methodShape.IsStatic ? default : Assert.IsType<TDeclaringType>(state, exactMatch: false);

            int i = 0;
            var argumentStateCtor = methodShape.GetArgumentStateConstructor();
            Assert.NotNull(argumentStateCtor);
            Assert.Same(methodShape.GetArgumentStateConstructor(), methodShape.GetArgumentStateConstructor());

            TArgumentState argumentState = argumentStateCtor();
            Assert.Equal(argumentState.Count, methodShape.Parameters.Count);
            int lastRequiredIndex = methodShape.Parameters.LastOrDefault(p => p.IsRequired)?.Position ?? -1;
            Assert.Equal(lastRequiredIndex == -1, argumentState.AreRequiredArgumentsSet);

            foreach (IParameterShape parameter in methodShape.Parameters)
            {
                Assert.Equal(i++, parameter.Position);
                argumentState = (TArgumentState)parameter.Accept(this, argumentState)!;
                Assert.Equal(lastRequiredIndex is -1 || parameter.Position >= lastRequiredIndex, argumentState.AreRequiredArgumentsSet);
            }

            Assert.True(argumentState.AreRequiredArgumentsSet);
            var parameterizedCtor = methodShape.GetMethodInvoker();
            Assert.NotNull(parameterizedCtor);
            Assert.Same(methodShape.GetMethodInvoker(), methodShape.GetMethodInvoker());

#if !NET
            if (methodShape.AttributeProvider is MethodInfo { ReturnType.IsByRef: true } method && IsReflectionInvokedMethod(method, providerUnderTest))
            {
                var ex = Assert.Throws<NotSupportedException>(() => parameterizedCtor.Invoke(ref instance, ref argumentState).Result);
                Assert.Contains("ByRef", ex.Message);
                return null;
            }
#else
            _ = providerUnderTest; // Avoid unused variable warnings.
#endif
            TResult value = parameterizedCtor.Invoke(ref instance, ref argumentState).Result;
            Assert.Equal(methodShape.IsVoidLike, value is Unit);
            return null;
        }

        public override object? VisitParameter<TArgumentState, TParameter>(IParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            RandomGenerator<TParameter> randomGenerator = RandomGenerator.Create(parameter.ParameterType);

            var argState = (TArgumentState)state!;
            var getter = parameter.GetGetter();
            Assert.Same(parameter.GetSetter(), parameter.GetSetter());
            var setter = parameter.GetSetter();
            Assert.Same(parameter.GetSetter(), parameter.GetSetter());

            TParameter? value = parameter.HasDefaultValue ? parameter.DefaultValue : default;
            Assert.Equal(getter(ref argState), value);
            Assert.False(argState.IsArgumentSet(parameter.Position));

            TParameter newValue = randomGenerator.GenerateValue(size: 1000, seed: 50);
            setter(ref argState, newValue);
            Assert.True(argState.IsArgumentSet(parameter.Position));
            Assert.Equal(getter(ref argState), newValue);
            return argState;
        }
    }

    [Theory]
    [InlineData(typeof(ClassWithMethodShapes), 25)]
    [InlineData(typeof(StructWithMethodShapes), 25)]
    [InlineData(typeof(InterfaceWithMethodShapes), 7, typeof(ClassWithMethodShapes))]
    public async Task MethodShapeInvoker(Type declaringType, int expectedMethodCount, Type? implementationType = null)
    {
        ITypeShape shape = providerUnderTest.Provider.GetTypeShapeOrThrow(declaringType);
        InterfaceWithMethodShapes instance = (InterfaceWithMethodShapes)Activator.CreateInstance(implementationType ?? declaringType)!;
        var invokerBuilder = new MethodShapeInvokerBuilder();
        Assert.Equal(expectedMethodCount, shape.Methods.Count);
        foreach (IMethodShape methodShape in shape.Methods)
        {
            var invoker = (Func<int, int, ValueTask<int>>)methodShape.Accept(invokerBuilder, instance)!;
#if !NET
            if (methodShape.AttributeProvider is MethodInfo { ReturnType.IsByRef: true } method && IsReflectionInvokedMethod(method, providerUnderTest))
            {
                var ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await invoker(7, 5));
                Assert.Contains("ByRef", ex.Message);
                continue;
            }
#endif
            Assert.Equal(12, await invoker(7, 5));
        }
    }

    private static bool IsReflectionInvokedMethod(MethodInfo method, ProviderUnderTest providerUnderTest) =>
        providerUnderTest.Kind is ProviderKind.ReflectionNoEmit ||
        (providerUnderTest.Kind is ProviderKind.SourceGen && !method.IsPublic);

    private sealed class MethodShapeInvokerBuilder : TypeShapeVisitor
    {
        public override object? VisitMethod<TDeclaringType, TArgumentState, TResult>(IMethodShape<TDeclaringType, TArgumentState, TResult> methodShape, object? state)
        {
            Assert.Equal(2, methodShape.Parameters.Count);
            var target = Assert.IsType<TDeclaringType>(state, exactMatch: false);
            var p1 = Assert.IsType<IParameterShape<TArgumentState, int>>(methodShape.Parameters[0], exactMatch: false);
            var p2 = Assert.IsType<IParameterShape<TArgumentState, int>>(methodShape.Parameters[1], exactMatch: false);
            var s1 = p1.GetSetter();
            var s2 = p2.GetSetter();
            var argStateFactory = methodShape.GetArgumentStateConstructor();
            var invoker = methodShape.GetMethodInvoker();
            return new Func<int, int, ValueTask<int>>(async (x1, x2) =>
            {
                var state = argStateFactory();
                s1(ref state, x1);
                s2(ref state, x2);

                StrongBox<int>? voidResultBox = target switch
                {
                    ClassWithMethodShapes when methodShape.IsVoidLike => ClassWithMethodShapes.LastVoidResultBox.Value!,
                    StructWithMethodShapes when methodShape.IsVoidLike => StructWithMethodShapes.LastVoidResultBox.Value!,
                    _ => null,
                };

                var result = await invoker(ref target, ref state);

                switch (result)
                {
                    case int intResult:
                        Assert.Null(voidResultBox);
                        return intResult;
                    default:
                        Assert.IsType<Unit>(result);
                        Assert.NotNull(voidResultBox);
                        return voidResultBox.Value;
                }
            });
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void TestEventShapes<T>(TestCase<T> testCase)
    {
        if (testCase.Value is null)
        {
            return;
        }

        ITypeShape<T> shape = providerUnderTest.ResolveShape(testCase);
        Assert.NotNull(shape.Events);
        Assert.Equal(typeof(T).GetExpectedEventShapeCount(), shape.Events.Count);

        foreach (IEventShape eventShape in shape.Events)
        {
            Assert.Equal(typeof(T), eventShape.DeclaringType.Type);
            EventInfo eventInfo = Assert.IsType<EventInfo>(eventShape.AttributeProvider, exactMatch: false);
            Assert.True(eventInfo.DeclaringType!.IsAssignableFrom(eventShape.DeclaringType.Type));
            Assert.Equal(eventInfo.EventHandlerType, eventShape.HandlerType.Type);

            EventShapeAttribute? eventShapeAttribute = eventInfo.GetCustomAttribute<EventShapeAttribute>();
            Assert.Equal(eventShapeAttribute?.Name ?? eventInfo.Name, eventShape.Name);

            Assert.NotNull(eventInfo.AddMethod);
            Assert.Equal(eventInfo.AddMethod.IsStatic, eventShape.IsStatic);
            Assert.Equal(eventInfo.AddMethod.IsPublic, eventShape.IsPublic);

            EventTestVisitor visitor = new();
            eventShape.Accept(visitor, state: testCase.Value);
        }
    }

    private sealed class EventTestVisitor : TypeShapeVisitor
    {
        public override object? VisitEvent<TDeclaringType, THandler>(IEventShape<TDeclaringType, THandler> eventShape, object? state)
        {
            var instance = (TDeclaringType?)state;
            Assert.Equal(typeof(TDeclaringType), eventShape.DeclaringType.Type);
            Assert.Equal(typeof(THandler), eventShape.HandlerType.Type);
            
            var addHandler = eventShape.GetAddHandler();
            Assert.Same(addHandler, eventShape.GetAddHandler());
            var removeHandler = eventShape.GetRemoveHandler();
            Assert.Same(removeHandler, eventShape.GetRemoveHandler());

            if (instance is ITriggerable)
            {
                lock (typeof(TDeclaringType)) // Avoid races when subscribing to static events.
                {
                    Assert.Equal(typeof(Action<int>), typeof(THandler));
                    int counter1 = 0;
                    int counter2 = 0;
                    var handler1 = (THandler)(object)new Action<int>(x => counter1 += x);
                    var handler2 = (THandler)(object)new Action<int>(x => counter2 += x);

                    ((ITriggerable)instance).Trigger(1);
                    Assert.Equal(0, counter1);
                    Assert.Equal(0, counter2);

                    addHandler(ref instance, handler1);

                    ((ITriggerable)instance!).Trigger(1);
                    Assert.Equal(1, counter1);
                    Assert.Equal(0, counter2);

                    addHandler(ref instance, handler2);

                    ((ITriggerable)instance!).Trigger(1);
                    Assert.Equal(2, counter1);
                    Assert.Equal(1, counter2);

                    removeHandler(ref instance, handler1);

                    ((ITriggerable)instance!).Trigger(1);
                    Assert.Equal(2, counter1);
                    Assert.Equal(2, counter2);

                    removeHandler(ref instance, handler2);

                    ((ITriggerable)instance!).Trigger(1);
                    Assert.Equal(2, counter1);
                    Assert.Equal(2, counter2);
                }
            }

            return null;
        }
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void DebuggerProxy_Annotations_ArePresent(ITestCase testCase)
    {
        ITypeShape shape = providerUnderTest.ResolveShape(testCase);

        AssertHasDebuggerDisplay(shape);
        AssertHasDebuggerTypeProxy(shape);

        if (shape is IObjectTypeShape objectShape)
        {
            foreach (IPropertyShape property in objectShape.Properties)
            {
                AssertHasDebuggerDisplay(property);
                AssertHasDebuggerTypeProxy(property);
            }

            if (objectShape.Constructor is { } ctor)
            {
                AssertHasDebuggerDisplay(ctor);
                AssertHasDebuggerTypeProxy(ctor);
                foreach (IParameterShape p in objectShape.Constructor.Parameters)
                {
                    AssertHasDebuggerDisplay(p);
                    AssertHasDebuggerTypeProxy(p);
                }
            }
        }

        foreach (IMethodShape method in shape.Methods)
        {
            AssertHasDebuggerDisplay(method);
            AssertHasDebuggerTypeProxy(method);
            foreach (IParameterShape p in method.Parameters)
            {
                AssertHasDebuggerDisplay(p);
                AssertHasDebuggerTypeProxy(p);
            }
        }

        foreach (IEventShape evt in shape.Events)
        {
            AssertHasDebuggerDisplay(evt);
            AssertHasDebuggerTypeProxy(evt);
        }

        static void AssertHasDebuggerDisplay<TShape>(TShape obj) where TShape : notnull
        {
            Type t = obj.GetType();
            var attr = t.GetCustomAttribute<DebuggerDisplayAttribute>(inherit: true);
            Assert.NotNull(attr);
            Assert.NotNull(attr.Value);
            Assert.Equal("{DebuggerDisplay,nq}", attr.Value);
            Assert.NotNull(t.GetProperty("DebuggerDisplay", BindingFlags.NonPublic | BindingFlags.Instance));
        }

        static void AssertHasDebuggerTypeProxy<TShape>(TShape obj) where TShape : notnull
        {
            Type t = obj.GetType();
            var attr = t.GetCustomAttribute<DebuggerTypeProxyAttribute>();
            Assert.NotNull(attr);
            Type? proxyType = Type.GetType(attr.ProxyTypeName);
            Assert.NotNull(proxyType);
            object? proxy = Activator.CreateInstance(proxyType, obj);
            Assert.IsType<TShape>(proxy, exactMatch: false);
        }
    }

    [Fact]
    public void GenericDerivedTypes_RuntimeComputedNamesMatchExpectations()
    {
        // Skip for SourceGen since test types don't have [GenerateShape]
        if (providerUnderTest.Kind is ProviderKind.SourceGen)
        {
            return;
        }

        // Test with simple generic types
        ITypeShape<PolymorphicTypeWithGenericDerivedTypes> shape = Provider.GetTypeShape<PolymorphicTypeWithGenericDerivedTypes>()!;
        IUnionTypeShape<PolymorphicTypeWithGenericDerivedTypes> unionShape = Assert.IsAssignableFrom<IUnionTypeShape<PolymorphicTypeWithGenericDerivedTypes>>(shape);

        Assert.Equal(3, unionShape.UnionCases.Count);

        // Horse should keep simple name
        var horseCase = unionShape.UnionCases.First(c => c.UnionCaseType.Type == typeof(PolymorphicTypeWithGenericDerivedTypes.Horse));
        Assert.Equal("Horse", horseCase.Name);

        // Cow<SolidHoof> should be Cow_SolidHoof
        var solidHoofCowCase = unionShape.UnionCases.First(c => c.UnionCaseType.Type == typeof(PolymorphicTypeWithGenericDerivedTypes.Cow<PolymorphicTypeWithGenericDerivedTypes.SolidHoof>));
        Assert.Equal("Cow_SolidHoof", solidHoofCowCase.Name);

        // Cow<ClovenHoof> should be Cow_ClovenHoof
        var clovenHoofCowCase = unionShape.UnionCases.First(c => c.UnionCaseType.Type == typeof(PolymorphicTypeWithGenericDerivedTypes.Cow<PolymorphicTypeWithGenericDerivedTypes.ClovenHoof>));
        Assert.Equal("Cow_ClovenHoof", clovenHoofCowCase.Name);
    }

    [Fact]
    public void NestedGenericDerivedTypes_RuntimeComputedNamesMatchExpectations()
    {
        // Skip for SourceGen since test types don't have [GenerateShape]
        if (providerUnderTest.Kind is ProviderKind.SourceGen)
        {
            return;
        }

        // Test with nested generic types like List<T>
        ITypeShape<PolymorphicTypeWithNestedGenericDerivedTypes> shape = Provider.GetTypeShape<PolymorphicTypeWithNestedGenericDerivedTypes>()!;
        IUnionTypeShape<PolymorphicTypeWithNestedGenericDerivedTypes> unionShape = Assert.IsAssignableFrom<IUnionTypeShape<PolymorphicTypeWithNestedGenericDerivedTypes>>(shape);

        Assert.Equal(2, unionShape.UnionCases.Count);

        // Container<int> should be Container_Int32
        var intContainerCase = unionShape.UnionCases.First(c => c.UnionCaseType.Type == typeof(PolymorphicTypeWithNestedGenericDerivedTypes.Container<int>));
        Assert.Equal("Container_Int32", intContainerCase.Name);

        // Container<List<string>> should be Container_List_String
        var listContainerCase = unionShape.UnionCases.First(c => c.UnionCaseType.Type == typeof(PolymorphicTypeWithNestedGenericDerivedTypes.Container<List<string>>));
        Assert.Equal("Container_List_String", listContainerCase.Name);
    }

    [Fact]
    public void AnimalWithGenericCowTypes_ValidatesAutoComputedNames()
    {
        // Test the Animal type from the original issue - validates each union case name explicitly
        ITypeShape<Animal> shape = Provider.GetTypeShape<Animal>()!;
        IUnionTypeShape<Animal> unionShape = Assert.IsAssignableFrom<IUnionTypeShape<Animal>>(shape);

        Assert.Equal(3, unionShape.UnionCases.Count);

        // Find each union case and validate its name
        var horseCase = unionShape.UnionCases.Single(c => c.UnionCaseType.Type == typeof(Animal.Horse));
        Assert.Equal("Horse", horseCase.Name);
        Assert.Equal(0, horseCase.Index);

        var solidHoofCowCase = unionShape.UnionCases.Single(c => c.UnionCaseType.Type == typeof(Animal.Cow<Animal.SolidHoof>));
        Assert.Equal("Cow_SolidHoof", solidHoofCowCase.Name);
        Assert.Equal(1, solidHoofCowCase.Index);
        Assert.True(solidHoofCowCase.IsTagSpecified);

        var clovenHoofCowCase = unionShape.UnionCases.Single(c => c.UnionCaseType.Type == typeof(Animal.Cow<Animal.ClovenHoof>));
        Assert.Equal("Cow_ClovenHoof", clovenHoofCowCase.Name);
        Assert.Equal(2, clovenHoofCowCase.Index);
        Assert.True(clovenHoofCowCase.IsTagSpecified);
    }

    [DerivedTypeShape(typeof(Horse))]
    [DerivedTypeShape(typeof(Cow<SolidHoof>), Tag = 1)]
    [DerivedTypeShape(typeof(Cow<ClovenHoof>), Tag = 2)]
    private class PolymorphicTypeWithGenericDerivedTypes
    {
        public class Horse : PolymorphicTypeWithGenericDerivedTypes;
        public class Cow<THoof> : PolymorphicTypeWithGenericDerivedTypes;
        public class SolidHoof;
        public class ClovenHoof;
    }

    [DerivedTypeShape(typeof(Container<int>))]
    [DerivedTypeShape(typeof(Container<List<string>>))]
    private class PolymorphicTypeWithNestedGenericDerivedTypes
    {
        public class Container<T> : PolymorphicTypeWithNestedGenericDerivedTypes;
    }
}

public static class ReflectionExtensions
{
    public static Type[]? GetDictionaryKeyValueTypes(this Type type)
    {
        if (type.GetCompatibleGenericInterface(typeof(IReadOnlyDictionary<,>)) is { } rod)
        {
            return rod.GetGenericArguments();
        }

        if (type.GetCompatibleGenericInterface(typeof(IDictionary<,>)) is { } d)
        {
            return d.GetGenericArguments();
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return [typeof(object), typeof(object)];
        }

        return null;
    }

    public static Type? GetCompatibleGenericInterface(this Type type, Type genericInterface)
    {
        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == genericInterface)
        {
            return type;
        }

        foreach (Type interfaceTy in type.GetInterfaces())
        {
            if (interfaceTy.IsGenericType && interfaceTy.GetGenericTypeDefinition() == genericInterface)
            {
                return interfaceTy;
            }
        }

        return null;
    }

    public static int GetExpectedMethodShapeCount(this Type type)
    {
        MethodShapeFlags flags = type.GetCustomAttribute<TypeShapeAttribute>()?.IncludeMethods ?? MethodShapeFlags.None;
        return type.ResolveVisibleMembers().OfType<MethodInfo>()
            .Count(IsIncludedMethod);

        bool IsIncludedMethod(MethodInfo methodInfo)
        {
            if (methodInfo is { DeclaringType.IsInterface: true, IsStatic: true, IsAbstract: true })
            {
                return false; // Skip static abstract methods in interfaces.
            }

            MethodShapeAttribute? shapeAttr = methodInfo.GetCustomAttribute<MethodShapeAttribute>();
            if (shapeAttr is not null)
            {
                return !shapeAttr.Ignore; // Skip methods explicitly marked as ignored.
            }

            if (!methodInfo.IsPublic || methodInfo.IsSpecialName)
            {
                return false; // Skip methods that are not public or special names (like property getters/setters).
            }

            if (methodInfo.DeclaringType == typeof(object) || methodInfo.DeclaringType == typeof(ValueType))
            {
                return false; // Skip GetHashCode, ToString, Equals, and other object methods.
            }

            MethodShapeFlags requiredFlag = methodInfo.IsStatic ? MethodShapeFlags.PublicStatic : MethodShapeFlags.PublicInstance;
            if ((flags & requiredFlag) == 0)
            {
                return false; // Skip methods that are not included in the shape by default.
            }

            return true;
        }
    }

    public static int GetExpectedEventShapeCount(this Type type)
    {
        MethodShapeFlags flags = type.GetCustomAttribute<TypeShapeAttribute>()?.IncludeMethods ?? MethodShapeFlags.None;
        return type.ResolveVisibleMembers().OfType<EventInfo>()
            .Count(IsIncludedEvent);

        bool IsIncludedEvent(EventInfo eventInfo)
        {
            EventShapeAttribute? shapeAttr = eventInfo.GetCustomAttribute<EventShapeAttribute>();
            if (shapeAttr is not null)
            {
                return !shapeAttr.Ignore; // Skip events explicitly marked as ignored.
            }

            MethodInfo? accessor = eventInfo.AddMethod ?? eventInfo.RemoveMethod;
            Assert.NotNull(accessor);
            if (!accessor.IsPublic)
            {
                return false; // Skip events that are not public.
            }

            MethodShapeFlags requiredFlag = accessor.IsStatic ? MethodShapeFlags.PublicStatic : MethodShapeFlags.PublicInstance;
            if ((flags & requiredFlag) == 0)
            {
                return false; // Skip methods that are not included in the shape by default.
            }

            return true;
        }
    }

    public static bool IsRequiredSpecified(this ParameterShapeAttribute parameter) => (bool)s_isRequiredParamProp.GetValue(parameter)!;
    private static readonly PropertyInfo s_isRequiredParamProp =
        typeof(ParameterShapeAttribute).GetProperty("IsRequiredSpecified", BindingFlags.NonPublic | BindingFlags.Instance)!;

    public static bool IsRequiredSpecified(this PropertyShapeAttribute property) => (bool)s_isRequiredPropProp.GetValue(property)!;
    private static readonly PropertyInfo s_isRequiredPropProp =
        typeof(PropertyShapeAttribute).GetProperty("IsRequiredSpecified", BindingFlags.NonPublic | BindingFlags.Instance)!;
}

public sealed class TypeShapeProviderTests_Reflection() : TypeShapeProviderTests(ReflectionProviderUnderTest.NoEmit);
public sealed class TypeShapeProviderTests_ReflectionEmit() : TypeShapeProviderTests(ReflectionProviderUnderTest.Emit)
{
    [Fact]
    public void ReflectionTypeShapeProvider_Default_UsesReflectionEmit()
    {
        Assert.True(ReflectionTypeShapeProvider.Default.Options.UseReflectionEmit);
    }

    [Fact]
    public void ReflectionTypeShapeProvider_Default_IsSingleton()
    {
        Assert.Same(ReflectionTypeShapeProvider.Default, ReflectionTypeShapeProvider.Default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReflectionTypeShapeProvider_Default_Factory_ReflectsParameters(bool useReflectionEmit)
    {
        ReflectionTypeShapeProviderOptions options = new() { UseReflectionEmit = useReflectionEmit };
        ReflectionTypeShapeProvider provider = ReflectionTypeShapeProvider.Create(options);
        Assert.Equal(useReflectionEmit, provider.Options.UseReflectionEmit);
    }

    [Theory]
    [InlineData(typeof(ClassWithEnumKind))]
    [InlineData(typeof(ClassWithNullableKind))]
    [InlineData(typeof(ClassWithDictionaryKind))]
    [InlineData(typeof(ClassWithEnumerableKind))]
    [InlineData(typeof(EnumerableWithDictionaryKind))]
    [InlineData(typeof(EnumerableWithEnumKind))]
    [InlineData(typeof(DictionaryWithNullableKind))]
    [InlineData(typeof(ClassWithSurrogateKind))]
    public void TypesWithInvalidTypeShapeKindAnnotations_ThrowsNotSupportedException(Type type)
    {
        NotSupportedException ex = Assert.Throws<NotSupportedException>(() => Provider.GetTypeShape(type));
        Assert.Contains("TypeShapeKind", ex.Message);
    }

    [TypeShape(Kind = TypeShapeKind.Enum)]
    private class ClassWithEnumKind;

    [TypeShape(Kind = TypeShapeKind.Optional)]
    private class ClassWithNullableKind;

    [TypeShape(Kind = TypeShapeKind.Dictionary)]
    private class ClassWithDictionaryKind;

    [TypeShape(Kind = TypeShapeKind.Enumerable)]
    private class ClassWithEnumerableKind;

    [TypeShape(Kind = TypeShapeKind.Dictionary)]
    private class EnumerableWithDictionaryKind : List<int>;

    [TypeShape(Kind = TypeShapeKind.Enum)]
    private class EnumerableWithEnumKind : List<int>;

    [TypeShape(Kind = TypeShapeKind.Optional)]
    private class DictionaryWithNullableKind : Dictionary<int, int>;

    [TypeShape(Kind = TypeShapeKind.Surrogate)]
    private class ClassWithSurrogateKind;

    [Theory]
    [InlineData(typeof(ClassWithInvalidMarshaler))]
    [InlineData(typeof(ClassWithMismatchingMarshaler))]
    [InlineData(typeof(ClassWithConflictingMarshalers))]
    public void ClassWithInvalidMarshalers_ThrowsInvalidOperationException(Type type)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Provider.GetTypeShape(type));
        Assert.Contains("surrogate", ex.Message);
    }

    [TypeShape(Marshaler = typeof(int))]
    private class ClassWithInvalidMarshaler;

    [TypeShape(Marshaler = typeof(Marshaler))]
    private class ClassWithMismatchingMarshaler
    {
        class Marshaler : IMarshaler<int, ClassWithMismatchingMarshaler>
        {
            public ClassWithMismatchingMarshaler? Marshal(int value) => throw new NotImplementedException();
            public int Unmarshal(ClassWithMismatchingMarshaler? surrogate) => throw new NotImplementedException();
        }
    }

    [TypeShape(Marshaler = typeof(Marshaler))]
    private class ClassWithConflictingMarshalers
    {
        class Marshaler : IMarshaler<ClassWithConflictingMarshalers, int>,
              IMarshaler<ClassWithConflictingMarshalers, string>
        {
            public int Marshal(ClassWithConflictingMarshalers? value) => throw new NotImplementedException();
            public ClassWithConflictingMarshalers? Unmarshal(string? surrogate) => throw new NotImplementedException();
            public ClassWithConflictingMarshalers? Unmarshal(int surrogate) => throw new NotImplementedException();
            string? IMarshaler<ClassWithConflictingMarshalers, string>.Marshal(ClassWithConflictingMarshalers? value) => throw new NotImplementedException();
        }
    }

    [Theory]
    [InlineData(typeof(PolymorphicClassWithInvalidDerivedType_NotASubtype), nameof(Object))]
    [InlineData(typeof(PolymorphicClassWithInvalidDerivedType_ConflictingTypes), nameof(PolymorphicClassWithInvalidDerivedType_ConflictingTypes.Derived))]
    [InlineData(typeof(PolymorphicClassWithInvalidDerivedType_ConflictingNames), "case1")]
    [InlineData(typeof(PolymorphicClassWithInvalidDerivedType_ConflictingTags), "42")]
    public void PolymorphicClassWithInvalidDerivedType_ThrowsInvalidOperationException(Type type, string invalidValue)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Provider.GetTypeShape(type));
        Assert.Contains(invalidValue, ex.Message);
    }

    [DerivedTypeShape(typeof(object))]
    private class PolymorphicClassWithInvalidDerivedType_NotASubtype;

    [DerivedTypeShape(typeof(Derived), Name = "case1", Tag = 1)]
    [DerivedTypeShape(typeof(Derived), Name = "case2", Tag = 2)]
    private class PolymorphicClassWithInvalidDerivedType_ConflictingTypes
    {
        public class Derived : PolymorphicClassWithInvalidDerivedType_ConflictingTypes;
    }

    [DerivedTypeShape(typeof(Derived1), Name = "case1", Tag = 1)]
    [DerivedTypeShape(typeof(Derived2), Name = "case1", Tag = 2)]
    private class PolymorphicClassWithInvalidDerivedType_ConflictingNames
    {
        public class Derived1 : PolymorphicClassWithInvalidDerivedType_ConflictingNames;
        public class Derived2 : PolymorphicClassWithInvalidDerivedType_ConflictingNames;
    }

    [DerivedTypeShape(typeof(Derived1), Name = "case1", Tag = 42)]
    [DerivedTypeShape(typeof(Derived2), Name = "case2", Tag = 42)]
    private class PolymorphicClassWithInvalidDerivedType_ConflictingTags
    {
        public class Derived1 : PolymorphicClassWithInvalidDerivedType_ConflictingTags;
        public class Derived2 : PolymorphicClassWithInvalidDerivedType_ConflictingTags;
    }

    [Theory]
    [InlineData(typeof(PolymorphicClassWithGenericDerivedType), "Derived")]
    [InlineData(typeof(GenericPolymorphicClassWithMismatchingGenericDerivedType<int>), "Derived")]
    public void PolymorphicClassWithGenericDerivedType_ThrowsInvalidOperationException(Type type, string derivedTypeName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Provider.GetTypeShape(type));
        Assert.Contains(derivedTypeName, ex.Message);
    }

    [DerivedTypeShape(typeof(Derived<>))]
    private class PolymorphicClassWithGenericDerivedType
    {
        public class Derived<T> : PolymorphicClassWithGenericDerivedType;
    }

    [DerivedTypeShape(typeof(Derived<>))]
    private class GenericPolymorphicClassWithMismatchingGenericDerivedType<T>
    {
        public class Derived<S> : GenericPolymorphicClassWithMismatchingGenericDerivedType<List<S>>;
    }

    [Fact]
    public void PropertyNamingConflicts_ThrowsNotSupportedException()
    {
        IObjectTypeShape shape = Assert.IsType<IObjectTypeShape>(Provider.GetTypeShape<ClassWithPropertyNamingConflict>(), exactMatch: false);
        var ex = Assert.Throws<NotSupportedException>(() => shape.Properties);
        Assert.Contains("Conflicting members named 'SameName' were found", ex.Message);
    }

    [Fact]
    public void EventNamingConflicts_ThrowsNotSupportedException()
    {
        ITypeShape shape = Provider.GetTypeShape<ClassWithEventNamingConflict>()!;
        var ex = Assert.Throws<NotSupportedException>(() => shape.Events);
        Assert.Contains("Conflicting members named 'SameName' were found", ex.Message);
    }

    [Fact]
    public void MethodNamingConflicts_ThrowsNotSupportedException()
    {
        ITypeShape shape = Provider.GetTypeShape<ClassWithMethodNamingConflict>()!;
        var ex = Assert.Throws<NotSupportedException>(() => shape.Methods);
        Assert.Contains("Multiple methods named 'SameName' were found", ex.Message);
    }

    [TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
    private class ClassWithPropertyNamingConflict
    {
        [PropertyShape(Name = "SameName")]
        public int Property1 { get; set; }

        [PropertyShape(Name = "SameName")]
        public string? Property2 { get; set; }
    }

    [TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
    private class ClassWithEventNamingConflict : ITriggerable
    {
        [EventShape(Name = "SameName")]
        public event Action<int>? Event1;

        [EventShape(Name = "SameName")]
        public event Action<int>? Event2;

        public void Trigger(int value)
        {
            Event1?.Invoke(value);
            Event2?.Invoke(value);
        }
    }

    [TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
    private class ClassWithMethodNamingConflict
    {
        [MethodShape(Name = "SameName")]
        public int Method1(int x, int y) => x + y;

        [MethodShape(Name = "SameName")]
        public int Method2(int x, int y) => x * y;
    }
}

public sealed partial class TypeShapeProviderTests_SourceGen() : TypeShapeProviderTests(SourceGenProviderUnderTest.Default)
{
    [Fact]
    public void WitnessType_ShapeProvider_IsSingleton()
    {
        ITypeShapeProvider provider = Witness.GeneratedTypeShapeProvider;

        Assert.NotNull(provider);
        Assert.Same(provider, Witness.GeneratedTypeShapeProvider);
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void WitnessType_ShapeProvider_MatchesGeneratedShapes(ITestCase testCase)
    {
        Assert.Same(Witness.GeneratedTypeShapeProvider, testCase.DefaultShape.Provider);
        Assert.Same(testCase.DefaultShape, Witness.GeneratedTypeShapeProvider.GetTypeShape(testCase.Type));
    }

#if NET8_0_OR_GREATER
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void IShapeableOfT_ReturnsExpectedSingleton<T, TProvider>(TestCase<T, TProvider> testCase)
        where TProvider : IShapeable<T>
    {
        Assert.Same(TProvider.GetTypeShape(), TProvider.GetTypeShape());
        Assert.Same(testCase.DefaultShape, TProvider.GetTypeShape());
        Assert.Same(Provider.GetTypeShape(typeof(T)), TProvider.GetTypeShape());
    }
#endif

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void ResolveDynamic_ReturnsExpectedSingleton<T, TProvider>(TestCase<T, TProvider> testCase)
#if NET
        where TProvider : IShapeable<T>
#endif
    {
        ITypeShape<T>? result1 = TypeShapeResolver.ResolveDynamic<T, TProvider>();
        Assert.NotNull(result1);

        ITypeShape<T>? result2 = TypeShapeResolver.ResolveDynamic<T, TProvider>();
        Assert.NotNull(result2);

        Assert.Same(result1, result2);
        Assert.Same(testCase.DefaultShape, result1);
#if NET
        Assert.Same(TProvider.GetTypeShape(), result1);
#endif
    }

    [Fact]
    public void ResolveDynamic_ReturnsNullForNonShapeable()
    {
        Assert.Null(TypeShapeResolver.ResolveDynamic<object>());
        Assert.Null(TypeShapeResolver.ResolveDynamic<int, object>());

        Assert.Throws<NotSupportedException>(() => TypeShapeResolver.ResolveDynamicOrThrow<object>());
        Assert.Throws<NotSupportedException>(() => TypeShapeResolver.ResolveDynamicOrThrow<int, object>());
    }

    [Fact]
    public void GenerateShape_CanConfigureMarshaler()
    {
        ITypeShape<ClassWithMarshaler> typeShape = LocalWitness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<ClassWithMarshaler>();
        var surrogateShape = Assert.IsType<ISurrogateTypeShape<ClassWithMarshaler, int>>(typeShape, exactMatch: false);
        Assert.Equal(TypeShapeKind.Surrogate, typeShape.Kind);
        Assert.IsType<ClassWithMarshaler.Marshaler>(surrogateShape.Marshaler);

        // Because we're configuring through GenerateShape, this does not flow through to the reflection provider.
        ITypeShape reflectionShape = ReflectionTypeShapeProvider.Default.GetTypeShapeOrThrow<ClassWithMarshaler>();
        Assert.NotEqual(TypeShapeKind.Surrogate, reflectionShape.Kind);
    }

    [Fact]
    public void GenerateShape_CanConfigureMethods()
    {
        ITypeShape<ClassWithMethod> typeShape = LocalWitness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<ClassWithMethod>();
        var method = Assert.Single(typeShape.Methods);
        Assert.Equal("Add", method.Name);
    }

    [Fact]
    public void GenerateShape_CanConfigureKind()
    {
        ITypeShape<ClassWithTrivialShape> typeShape = LocalWitness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<ClassWithTrivialShape>();
        var objectShape = Assert.IsType<IObjectTypeShape<ClassWithTrivialShape>>(typeShape, exactMatch: false);
        Assert.Empty(objectShape.Properties);
        Assert.Null(objectShape.Constructor);
    }

    [Fact]
    public void GenerateShapeFor_CanConfigureMarshaler()
    {
        ITypeShape<ClassWithMarshalerExternal> typeShape = LocalWitness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<ClassWithMarshalerExternal>();
        var surrogateShape = Assert.IsType<ISurrogateTypeShape<ClassWithMarshalerExternal, int>>(typeShape, exactMatch: false);
        Assert.Equal(TypeShapeKind.Surrogate, typeShape.Kind);
        Assert.IsType<ClassWithMarshalerExternal.Marshaler>(surrogateShape.Marshaler);

        // Because we're configuring through GenerateShapeFor, this does not flow through to the reflection provider.
        ITypeShape reflectionShape = ReflectionTypeShapeProvider.Default.GetTypeShapeOrThrow<ClassWithMarshalerExternal>();
        Assert.NotEqual(TypeShapeKind.Surrogate, reflectionShape.Kind);
    }

    [Fact]
    public void GenerateShapeFor_CanConfigureMethods()
    {
        ITypeShape<ClassWithMethodExternal> typeShape = LocalWitness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<ClassWithMethodExternal>();
        var method = Assert.Single(typeShape.Methods);
        Assert.Equal("Add", method.Name);
    }

    [Fact]
    public void GenerateShapeFor_CanConfigureKind()
    {
        ITypeShape<ClassWithTrivialShapeExternal> typeShape = LocalWitness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow<ClassWithTrivialShapeExternal>();
        var objectShape = Assert.IsType<IObjectTypeShape<ClassWithTrivialShapeExternal>>(typeShape, exactMatch: false);
        Assert.Empty(objectShape.Properties);
        Assert.Null(objectShape.Constructor);
    }


    [GenerateShape(Marshaler = typeof(Marshaler))]
    public partial class ClassWithMarshaler
    {
        public int Value { get; set; }

        public sealed class Marshaler : IMarshaler<ClassWithMarshaler, int>
        {
            public int Marshal(ClassWithMarshaler? value) => value?.Value ?? 0;
            public ClassWithMarshaler? Unmarshal(int value) => new() { Value = value };
        }
    }

    [GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
    public partial class ClassWithMethod
    {
        public int Add(int x, int y) => x + y;
    }

    [GenerateShape(Kind = TypeShapeKind.None)]
    public partial record ClassWithTrivialShape(int x, string y);

    public partial class ClassWithMarshalerExternal
    {
        public int Value { get; set; }

        public sealed class Marshaler : IMarshaler<ClassWithMarshalerExternal, int>
        {
            public int Marshal(ClassWithMarshalerExternal? value) => value?.Value ?? 0;
            public ClassWithMarshalerExternal? Unmarshal(int value) => new() { Value = value };
        }
    }

    public partial class ClassWithMethodExternal
    {
        public int Add(int x, int y) => x + y;
    }

    public partial record ClassWithTrivialShapeExternal(int x, string y);

    [GenerateShapeFor<ClassWithMarshalerExternal>(Marshaler = typeof(ClassWithMarshalerExternal.Marshaler))]
    [GenerateShapeFor(typeof(ClassWithMethodExternal), IncludeMethods = MethodShapeFlags.PublicInstance)]
    [GenerateShapeFor(typeof(ClassWithTrivialShapeExternal), Kind = TypeShapeKind.None)]
    public partial class LocalWitness;
}