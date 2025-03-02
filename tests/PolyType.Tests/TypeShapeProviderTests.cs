using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using PolyType.Abstractions;
using PolyType.Examples.RandomGenerator;
using PolyType.ReflectionProvider;
using Xunit;

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
        Assert.Equal(typeof(T).IsRecordType() && testCase is { UsesMarshaller: false, IsUnion: false }, shape is IObjectTypeShape { IsRecordType: true});
        Assert.Equal(typeof(T).IsTupleType() && testCase is { UsesMarshaller: false, IsUnion: false }, shape is IObjectTypeShape { IsTupleType: true });
        
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

            if (testCase.UsesMarshaller)
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
                foreach (IConstructorParameterShape parameter in constructor.Parameters)
                {
                    Assert.Equal(i++, parameter.Position);
                    argumentState = (TArgumentState)parameter.Accept(this, argumentState)!;
                }

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

        public override object? VisitConstructorParameter<TArgumentState, TParameter>(IConstructorParameterShape<TArgumentState, TParameter> parameter, object? state)
        {
            var argState = (TArgumentState)state!;
            var setter = parameter.GetSetter();
            Assert.Same(parameter.GetSetter(), parameter.GetSetter());

            TParameter? value = parameter.HasDefaultValue ? parameter.DefaultValue : default;
            setter(ref argState, value!);
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
            Type? marshallerType = typeof(T).GetCustomAttribute<TypeShapeAttribute>()?.Marshaller;
            Assert.NotNull(marshallerType);
            if (marshallerType.IsGenericTypeDefinition)
            {
                marshallerType = marshallerType.MakeGenericType(typeof(T).GetGenericArguments());
            }
            
            Assert.Equal(typeof(T), surrogateShape.Type);
            Assert.Equal(typeof(TSurrogate), surrogateShape.SurrogateType.Type);
            Assert.IsType(marshallerType, surrogateShape.Marshaller);
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
            Assert.NotSame(shape, unionShape.BaseType);
            Assert.NotEmpty(unionShape.UnionCases);
            int i = 0;
            foreach (IUnionCaseShape unionCase in unionShape.UnionCases)
            {
                Assert.True(typeof(T).IsAssignableFrom(unionCase.Type.Type));
                Assert.NotNull(unionCase.Name);
                Assert.Equal(i++, unionCase.Index);
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

            var visitor = new DictionaryTestVisitor();
            dictionaryType.Accept(visitor);
        }
        else
        {
            Assert.False(shape is IDictionaryTypeShape);
        }
    }

    private sealed class DictionaryTestVisitor : TypeShapeVisitor
    {
        public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state)
        {
            TDictionary dictionary;
            RandomGenerator<TKey> keyGenerator = RandomGenerator.Create(dictionaryShape.KeyType);
            var getter = dictionaryShape.GetGetDictionary();

            if (dictionaryShape.ConstructionStrategy is CollectionConstructionStrategy.Mutable)
            {
                var defaultCtor = dictionaryShape.GetDefaultConstructor();
                var adder = dictionaryShape.GetAddKeyValuePair();
                Assert.Same(dictionaryShape.GetDefaultConstructor(), dictionaryShape.GetDefaultConstructor());
                Assert.Same(dictionaryShape.GetAddKeyValuePair(), dictionaryShape.GetAddKeyValuePair());

                dictionary = defaultCtor();
                Assert.Empty(getter(dictionary));

                TKey newKey = keyGenerator.GenerateValue(size: 1000, seed: 42);
                adder(ref dictionary, new(newKey, default!));
                Assert.Single(getter(dictionary));
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetDefaultConstructor());
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetAddKeyValuePair());
            }

            if (dictionaryShape.ConstructionStrategy is CollectionConstructionStrategy.Enumerable)
            {
                var enumerableCtor = dictionaryShape.GetEnumerableConstructor();
                Assert.Same(dictionaryShape.GetEnumerableConstructor(), dictionaryShape.GetEnumerableConstructor());

                var values = keyGenerator.GenerateValues(seed: 42)
                    .Select(k => new KeyValuePair<TKey, TValue>(k, default!))
                    .Take(10);

                dictionary = enumerableCtor(values);
                Assert.Equal(10, getter(dictionary).Count);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetEnumerableConstructor());
            }

            if (dictionaryShape.ConstructionStrategy is CollectionConstructionStrategy.Span)
            {
                var spanCtor = dictionaryShape.GetSpanConstructor();
                Assert.Same(dictionaryShape.GetSpanConstructor(), dictionaryShape.GetSpanConstructor());

                var values = keyGenerator.GenerateValues(seed: 42)
                    .Select(k => new KeyValuePair<TKey, TValue>(k, default!))
                    .Take(10)
                    .ToArray();

                dictionary = spanCtor(values);
                Assert.Equal(10, getter(dictionary).Count);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => dictionaryShape.GetSpanConstructor());
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

            if (typeof(T).GetCompatibleGenericInterface(typeof(IEnumerable<>)) is { } enumerableImplementation)
            {
                Assert.Equal(enumerableImplementation.GetGenericArguments()[0], enumerableTypeType.ElementType.Type);
                Assert.Equal(1, enumerableTypeType.Rank);
            }
            else if (typeof(T).IsArray)
            {
                Assert.Equal(typeof(T).GetElementType(), enumerableTypeType.ElementType.Type);
                Assert.Equal(typeof(T).GetArrayRank(), enumerableTypeType.Rank);
            }
            else if (typeof(IEnumerable).IsAssignableFrom(typeof(T)))
            {
                Assert.Equal(typeof(object), enumerableTypeType.ElementType.Type);
                Assert.Equal(1, enumerableTypeType.Rank);
            }
            else if (typeof(T).IsMemoryType(out Type? elementType, out _))
            {
                Assert.Equal(elementType, enumerableTypeType.ElementType.Type);
                Assert.Equal(1, enumerableTypeType.Rank);
            }
            else
            {
                Assert.Fail($"Unexpected enumerable type: {typeof(T)}");
            }

            var visitor = new EnumerableTestVisitor();
            enumerableTypeType.Accept(visitor);
        }
        else
        {
            Assert.False(shape is IEnumerableTypeShape);
        }
    }

    private sealed class EnumerableTestVisitor : TypeShapeVisitor
    {
        public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state)
        {
            TEnumerable enumerable;
            RandomGenerator<TElement> elementGenerator = RandomGenerator.Create((ITypeShape<TElement>)enumerableShape.ElementType);
            var getter = enumerableShape.GetGetEnumerable();

            if (enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Mutable)
            {
                var defaultCtor = enumerableShape.GetDefaultConstructor();
                var adder = enumerableShape.GetAddElement();
                Assert.Same(enumerableShape.GetDefaultConstructor(), enumerableShape.GetDefaultConstructor());
                Assert.Same(enumerableShape.GetAddElement(), enumerableShape.GetAddElement());

                enumerable = defaultCtor();
                Assert.Empty(getter(enumerable));

                TElement newElement = elementGenerator.GenerateValue(size: 1000, seed: 42);
                adder(ref enumerable, newElement);
                Assert.Single(getter(enumerable));
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetDefaultConstructor());
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetAddElement());
            }

            if (enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Enumerable)
            {
                var enumerableCtor = enumerableShape.GetEnumerableConstructor();
                Assert.Same(enumerableShape.GetEnumerableConstructor(), enumerableShape.GetEnumerableConstructor());

                var values = elementGenerator.GenerateValues(seed: 42).Take(10);
                enumerable = enumerableCtor(values);
                Assert.Equal(10, getter(enumerable).Count());
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetEnumerableConstructor());
            }

            if (enumerableShape.ConstructionStrategy is CollectionConstructionStrategy.Span)
            {
                var spanCtor = enumerableShape.GetSpanConstructor();
                Assert.Same(enumerableShape.GetSpanConstructor(), enumerableShape.GetSpanConstructor());

                var values = elementGenerator.GenerateValues(seed: 42).Take(10).ToArray();
                enumerable = spanCtor(values);
                Assert.Equal(10, getter(enumerable).Count());
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => enumerableShape.GetSpanConstructor());
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

        foreach (IPropertyShape property in objectShape.Properties)
        {
            MemberInfo attributeProvider = Assert.IsAssignableFrom<MemberInfo>(property.AttributeProvider);
            PropertyShapeAttribute? attr = attributeProvider.GetCustomAttribute<PropertyShapeAttribute>();

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
            foreach (IConstructorParameterShape ctorParam in constructor.Parameters)
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
                    Assert.Equal(!hasDefaultValue, ctorParam.IsRequired);
                    Assert.Equal(ConstructorParameterKind.ConstructorParameter, ctorParam.Kind);
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
                    Assert.Equal(!hasSetsRequiredMembersAttribute && memberInfo.IsRequired(), ctorParam.IsRequired);
                    Assert.Equal(ConstructorParameterKind.MemberInitializer, ctorParam.Kind);

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

            foreach (IConstructorParameterShape ctorParam in constructor.Parameters)
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
}

public sealed class TypeShapeProviderTests_Reflection() : TypeShapeProviderTests(RefectionProviderUnderTest.NoEmit);
public sealed class TypeShapeProviderTests_ReflectionEmit() : TypeShapeProviderTests(RefectionProviderUnderTest.Emit)
{
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
    [InlineData(typeof(ClassWithInvalidMarshaller))]
    [InlineData(typeof(ClassWithMismatchingMarshaller))]
    [InlineData(typeof(ClassWithConflictingMarshallers))]
    public void ClassWithInvalidMarshallers_ThrowsInvalidOperationException(Type type)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Provider.GetShape(type));
        Assert.Contains("surrogate", ex.Message);
    }

    [TypeShape(Marshaller = typeof(int))]
    private class ClassWithInvalidMarshaller;
    
    [TypeShape(Marshaller = typeof(Marshaller))]
    private class ClassWithMismatchingMarshaller
    {
        class Marshaller : IMarshaller<int, ClassWithMismatchingMarshaller>
        {
            public ClassWithMismatchingMarshaller? ToSurrogate(int value) => throw new NotImplementedException();
            public int FromSurrogate(ClassWithMismatchingMarshaller? surrogate) => throw new NotImplementedException();
        }
    }

    [TypeShape(Marshaller = typeof(Marshaller))]
    private class ClassWithConflictingMarshallers
    {
        class Marshaller : IMarshaller<ClassWithConflictingMarshallers, int>,
              IMarshaller<ClassWithConflictingMarshallers, string>
        {
            public int ToSurrogate(ClassWithConflictingMarshallers? value) => throw new NotImplementedException();
            public ClassWithConflictingMarshallers? FromSurrogate(string? surrogate) => throw new NotImplementedException();
            public ClassWithConflictingMarshallers? FromSurrogate(int surrogate) => throw new NotImplementedException();
            string? IMarshaller<ClassWithConflictingMarshallers, string>.ToSurrogate(ClassWithConflictingMarshallers? value) => throw new NotImplementedException();
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
}

public sealed class TypeShapeProviderTests_SourceGen() : TypeShapeProviderTests(SourceGenProviderUnderTest.Default)
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
}