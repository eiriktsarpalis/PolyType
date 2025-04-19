namespace CSharpSamples;

public partial class AssociatedTypes
{
    #region TypeShapeOneType
    [AssociatedTypeShape(typeof(MyAssociatedType))]
    public class MyType
    {
        public int Value { get; set; }
    }

    public class MyAssociatedType
    {
    }
    #endregion

    #region GenericAssociatedType
    [AssociatedTypeShape(typeof(GenericAssociatedType<,>))]
    public class MyGenericType<T1, T2>
    {
        public required T1 Value1 { get; set; }
        public required T2 Value2 { get; set; }
    }

    public class GenericAssociatedType<T1, T2>
    {
    }
    #endregion

    #region SerializerConverter
    [AssociatedTypeShape(typeof(SomeDataTypeConverter<>), Requirements = TypeShapeRequirements.Constructor)]
    public class SomeDataType<T>
    {
        public T? Value { get; set; }
    }

    public class SomeDataTypeConverter<T> : Converter<SomeDataType<T>>
    {
        // This type can be activated via the constructor defined on its shape.
        // Properties on this type will not be available on the associated shape.
    }
    #endregion

    public abstract class Converter<T>;
}
