namespace PolyType.Tests;

public static class TypeShapeAttributeTests
{
    [Theory]
    [InlineData(TypeShapeKind.None)]
    [InlineData(TypeShapeKind.Object)]
    [InlineData(TypeShapeKind.Enum)]
    [InlineData(TypeShapeKind.Optional)]
    [InlineData(TypeShapeKind.Enumerable)]
    [InlineData(TypeShapeKind.Dictionary)]
    [InlineData(TypeShapeKind.Surrogate)]
    [InlineData(TypeShapeKind.Union)]
    [InlineData(TypeShapeKind.Function)]
    public static void Kind_AcceptsDefinedValues(TypeShapeKind kind)
    {
        Assert.Equal(kind, new TypeShapeAttribute { Kind = kind }.Kind);
        Assert.Equal(kind, new TypeShapeExtensionAttribute(typeof(object)) { Kind = kind }.Kind);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(int.MaxValue)]
    public static void Kind_RejectsUndefinedValues(int value)
    {
        TypeShapeKind kind = (TypeShapeKind)value;
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TypeShapeAttribute { Kind = kind });
        Assert.Equal("value", exception.ParamName);
        Assert.Equal(kind, exception.ActualValue);

        exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TypeShapeExtensionAttribute(typeof(object)) { Kind = kind });
        Assert.Equal("value", exception.ParamName);
        Assert.Equal(kind, exception.ActualValue);
    }
}
