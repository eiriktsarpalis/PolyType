namespace PolyType.Examples.JsonSerializer.Converters;

[Flags]
internal enum JsonValueType
{
    None = 0,
    Object = 1,
    Array = 2,
    String = 4,
    Number = 8,
    Boolean = 16,
    Any = Object | Array | String | Number | Boolean,
}
