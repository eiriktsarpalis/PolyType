﻿using PolyType.Examples.RandomGenerator;
using PolyType.ReflectionProvider;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

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
        Assert.Same(shape, shape.Provider.GetShape(shape.Type));

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

        var visitor = new ConstructorTestVisitor();
        if (objectShape.Constructor is { } ctor)
        {
            Assert.Equal(typeof(T), ctor.DeclaringType.Type);
            ctor.Accept(visitor, typeof(T));
        }
    }

    private sealed class ConstructorTestVisitor : TypeShapeVisitor
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
                Assert.NotNull(defaultValue);
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
            var argState = (TArgumentState)state!;
            var setter = parameter.GetSetter();
            Assert.Same(parameter.GetSetter(), parameter.GetSetter());

            TParameter? value = parameter.HasDefaultValue ? parameter.DefaultValue : default;
            Assert.False(argState.IsArgumentSet(parameter.Position));
            setter(ref argState, value!);
            Assert.True(argState.IsArgumentSet(parameter.Position));
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
                Assert.True(typeof(T).IsAssignableFrom(unionCase.Type.Type));
                Assert.NotNull(unionCase.Name);
                Assert.Equal(i++, unionCase.Index);

                DerivedTypeShapeAttribute? attribute = attributes.FirstOrDefault(a => NormalizeType(a.Type) == NormalizeType(unionCase.Type.Type));
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
                    Assert.True(matchingCase.Type.Type.IsAssignableFrom(value!.GetType()));
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
            Assert.Equal(methodInfo.IsAsyncMethod(), method.IsAsync);
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
            var argState = (TArgumentState)state!;
            var setter = parameter.GetSetter();
            Assert.Same(parameter.GetSetter(), parameter.GetSetter());

            TParameter? value = parameter.HasDefaultValue ? parameter.DefaultValue : default;
            Assert.False(argState.IsArgumentSet(parameter.Position));
            setter(ref argState, value!);
            Assert.True(argState.IsArgumentSet(parameter.Position));
            return argState;
        }
    }

    [Theory]
    [InlineData(typeof(ClassWithMethodShapes), 25)]
    [InlineData(typeof(StructWithMethodShapes), 25)]
    [InlineData(typeof(InterfaceWithMethodShapes), 7, typeof(ClassWithMethodShapes))]
    public async Task MethodShapeInvoker(Type declaringType, int expectedMethodCount, Type? implementationType = null)
    {
        ITypeShape shape = providerUnderTest.Provider.Resolve(declaringType);
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
        return type.GetAllMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Count(IsIncludedMethod);

        bool IsIncludedMethod(MethodInfo methodInfo)
        {
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
        NotSupportedException ex = Assert.Throws<NotSupportedException>(() => Provider.GetShape(type));
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
        var ex = Assert.Throws<InvalidOperationException>(() => Provider.GetShape(type));
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
        var ex = Assert.Throws<InvalidOperationException>(() => Provider.GetShape(type));
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
        var ex = Assert.Throws<InvalidOperationException>(() => Provider.GetShape(type));
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
}

public sealed partial class TypeShapeProviderTests_SourceGen() : TypeShapeProviderTests(SourceGenProviderUnderTest.Default)
{
    [Fact]
    public void WitnessType_ShapeProvider_IsSingleton()
    {
        ITypeShapeProvider provider = Witness.ShapeProvider;

        Assert.NotNull(provider);
        Assert.Same(provider, Witness.ShapeProvider);
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void WitnessType_ShapeProvider_MatchesGeneratedShapes(ITestCase testCase)
    {
        Assert.Same(Witness.ShapeProvider, testCase.DefaultShape.Provider);
        Assert.Same(testCase.DefaultShape, Witness.ShapeProvider.GetShape(testCase.Type));
    }

#if NET8_0_OR_GREATER
    [Theory]
    [MemberData(nameof(TestTypes.GetTestCases), MemberType = typeof(TestTypes))]
    public void IShapeableOfT_ReturnsExpectedSingleton<T, TProvider>(TestCase<T, TProvider> testCase)
        where TProvider : IShapeable<T>
    {
        Assert.Same(TProvider.GetShape(), TProvider.GetShape());
        Assert.Same(testCase.DefaultShape, TProvider.GetShape());
        Assert.Same(Provider.GetShape(typeof(T)), TProvider.GetShape());
    }
#endif

    [Fact]
    public void GenerateShape_CanConfigureMarshaler()
    {
        ITypeShape<ClassWithMarshaler> typeShape = LocalWitness.ShapeProvider.Resolve<ClassWithMarshaler>();
        var surrogateShape = Assert.IsType<ISurrogateTypeShape<ClassWithMarshaler, int>>(typeShape, exactMatch: false);
        Assert.Equal(TypeShapeKind.Surrogate, typeShape.Kind);
        Assert.IsType<ClassWithMarshaler.Marshaler>(surrogateShape.Marshaler);

        // Because we're configuring through GenerateShape, this does not flow through to the reflection provider.
        ITypeShape reflectionShape = ReflectionTypeShapeProvider.Default.Resolve<ClassWithMarshaler>();
        Assert.NotEqual(TypeShapeKind.Surrogate, reflectionShape.Kind);
    }

    [Fact]
    public void GenerateShape_CanConfigureMethods()
    {
        ITypeShape<ClassWithMethod> typeShape = LocalWitness.ShapeProvider.Resolve<ClassWithMethod>();
        var method = Assert.Single(typeShape.Methods);
        Assert.Equal("Add", method.Name);
    }

    [Fact]
    public void GenerateShape_CanConfigureKind()
    {
        ITypeShape<ClassWithTrivialShape> typeShape = LocalWitness.ShapeProvider.Resolve<ClassWithTrivialShape>();
        var objectShape = Assert.IsType<IObjectTypeShape<ClassWithTrivialShape>>(typeShape, exactMatch: false);
        Assert.Empty(objectShape.Properties);
        Assert.Null(objectShape.Constructor);
    }

    [Fact]
    public void GenerateShapeFor_CanConfigureMarshaler()
    {
        ITypeShape<ClassWithMarshalerExternal> typeShape = LocalWitness.ShapeProvider.Resolve<ClassWithMarshalerExternal>();
        var surrogateShape = Assert.IsType<ISurrogateTypeShape<ClassWithMarshalerExternal, int>>(typeShape, exactMatch: false);
        Assert.Equal(TypeShapeKind.Surrogate, typeShape.Kind);
        Assert.IsType<ClassWithMarshalerExternal.Marshaler>(surrogateShape.Marshaler);

        // Because we're configuring through GenerateShapeFor, this does not flow through to the reflection provider.
        ITypeShape reflectionShape = ReflectionTypeShapeProvider.Default.Resolve<ClassWithMarshalerExternal>();
        Assert.NotEqual(TypeShapeKind.Surrogate, reflectionShape.Kind);
    }

    [Fact]
    public void GenerateShapeFor_CanConfigureMethods()
    {
        ITypeShape<ClassWithMethodExternal> typeShape = LocalWitness.ShapeProvider.Resolve<ClassWithMethodExternal>();
        var method = Assert.Single(typeShape.Methods);
        Assert.Equal("Add", method.Name);
    }

    [Fact]
    public void GenerateShapeFor_CanConfigureKind()
    {
        ITypeShape<ClassWithTrivialShapeExternal> typeShape = LocalWitness.ShapeProvider.Resolve<ClassWithTrivialShapeExternal>();
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