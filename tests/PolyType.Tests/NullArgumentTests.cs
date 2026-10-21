using PolyType.Examples.Utilities;
using PolyType.ReflectionProvider;
using PolyType.Utilities;

namespace PolyType.Tests;

public static class NullArgumentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void DerivedTypeAttribute_PreservesParameterNames(bool initializer)
    {
        Action action = initializer
            ? () => _ = new DerivedTypeShapeAttribute(typeof(int)) { Name = null! }
            : () => _ = new DerivedTypeShapeAttribute(null!);

        Assert.Equal(initializer ? "value" : "type", Assert.Throws<ArgumentNullException>(action).ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ReflectionProvider_PreservesParameterNames(bool useEmit)
    {
        var provider = ReflectionTypeShapeProvider.Create(new() { UseReflectionEmit = useEmit });

        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => ReflectionTypeShapeProvider.Create(null!)).ParamName);
        Assert.Equal("type", Assert.Throws<ArgumentNullException>(() => provider.GetTypeShape(null!)).ParamName);
    }

    [Theory]
    [InlineData(nameof(ReflectionUtilities.IsDefined), "attributeProvider")]
    [InlineData(nameof(ReflectionUtilities.GetCustomAttribute), "attributeProvider")]
    [InlineData(nameof(ReflectionUtilities.GetCustomAttributes), "attributeProvider")]
    [InlineData(nameof(ReflectionUtilities.GetDerivedTypeShapeName), "type")]
    public static void ReflectionUtilities_PreservesParameterNames(string method, string parameterName)
    {
        Action action = method switch
        {
            nameof(ReflectionUtilities.IsDefined) => () => ReflectionUtilities.IsDefined<Attribute>(null!),
            nameof(ReflectionUtilities.GetCustomAttribute) => () => ReflectionUtilities.GetCustomAttribute<Attribute>(null!),
            nameof(ReflectionUtilities.GetCustomAttributes) => () => ReflectionUtilities.GetCustomAttributes<Attribute>(null!),
            nameof(ReflectionUtilities.GetDerivedTypeShapeName) => () => ReflectionUtilities.GetDerivedTypeShapeName(null!),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

        Assert.Equal(parameterName, Assert.Throws<ArgumentNullException>(action).ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void TypeCache_ValidatesBeforeProviderLookup(bool useShape)
    {
        var cache = new TypeCache();
        Action action = useShape
            ? () => cache.GetOrAdd((ITypeShape)null!)
            : () => cache.GetOrAdd((Type)null!);

        Assert.Equal(useShape ? "typeShape" : "type", Assert.Throws<ArgumentNullException>(action).ParamName);
        Assert.Empty(cache);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void MultiProviderCache_PreservesParameterNames(bool scoped)
    {
        var cache = new MultiProviderTypeCache();
        Action action = scoped
            ? () => cache.GetScopedCache(null!)
            : () => cache.GetOrAdd(null!);

        Assert.Equal("typeShape", Assert.Throws<ArgumentNullException>(action).ParamName);
    }

    [Theory]
    [InlineData(nameof(TypeGenerationContext.GetOrAdd), "typeShape")]
    [InlineData(nameof(TypeGenerationContext.TryGetValue), "typeShape")]
    [InlineData(nameof(TypeGenerationContext.Add), "key")]
    public static void TypeGenerationContext_ValidatesBeforeResolvingOrAddingEntries(string method, string parameterName)
    {
        var context = new TypeGenerationContext();
        Action action = method switch
        {
            nameof(TypeGenerationContext.GetOrAdd) => () => context.GetOrAdd(null!),
            nameof(TypeGenerationContext.TryGetValue) => () => context.TryGetValue(null!, out _),
            nameof(TypeGenerationContext.Add) => () => context.Add(null!, null),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

        Assert.Equal(parameterName, Assert.Throws<ArgumentNullException>(action).ParamName);
        Assert.Empty(context);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    public static void SourceGeneratedShapes_ValidateAssociatedTypeBeforeLookup(Type type)
    {
        ITypeShape shape = Witness.GeneratedTypeShapeProvider.GetTypeShapeOrThrow(type);

        Assert.Equal("associatedType", Assert.Throws<ArgumentNullException>(() => shape.GetAssociatedTypeShape(null!)).ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void AggregateProvider_PreservesNullCollectionAndNullElementExceptions(bool nullElement)
    {
        if (nullElement)
        {
            Assert.Equal("providers", Assert.Throws<ArgumentException>(() => new AggregatingTypeShapeProvider([null!])).ParamName);
        }
        else
        {
            Assert.Equal("providers", Assert.Throws<ArgumentNullException>(() => new AggregatingTypeShapeProvider(null!)).ParamName);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void DuplicatePropertyValidation_RejectsNullBeforeInvokingCallbacks(bool customCallback)
    {
        Func<IPropertyShape, Exception>? callback = customCallback
            ? _ => throw new InvalidOperationException("The callback must not be invoked.")
            : null;

        Assert.Equal("properties", Assert.Throws<ArgumentNullException>(() => new DuplicatePropertyValidator(null!, callback)).ParamName);
    }
}
