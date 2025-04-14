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
}
