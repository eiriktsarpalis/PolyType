using PolyType;

#pragma warning disable CA1050 // Declare types in namespaces

[GenerateShape]
public partial record RecordWithoutNamespace(int Value);

public record GenericRecordWithoutNamespace<T>(T Value);

public static class GenericContainerWithoutNamespace<T1>
{ 
    public partial record Record<T2>(T1 value1, T2 value2);
}
