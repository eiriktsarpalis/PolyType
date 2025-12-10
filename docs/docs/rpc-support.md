# RPC Support

PolyType enables rapid development of RPC frameworks and method invocation libraries using method shapes. Here's an example using a `JsonFunc` abstraction from the `PolyType.Examples` sample library, which wraps .NET methods in JSON-based delegates:

```csharp
using PolyType;
using PolyType.Examples.JsonSerializer;

// Define a service class with methods to expose
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial class CalculatorService
{
    public int Add(int x, int y) => x + y;
    
    public async ValueTask<double> DivideAsync(double numerator, double denominator)
    {
        await Task.Delay(10); // Simulate async work
        return numerator / denominator;
    }
}

// Create JSON-based delegates for each method
var service = new CalculatorService();
var serviceShape = TypeShapeResolver.Resolve<CalculatorService>();

var addFunc = JsonSerializerTS.CreateJsonFunc(
    serviceShape.Methods.First(m => m.Name == "Add"), 
    service);

var divideFunc = JsonSerializerTS.CreateJsonFunc(
    serviceShape.Methods.First(m => m.Name == "DivideAsync"), 
    service);

// Invoke methods with JSON parameters
var result1 = await addFunc.Invoke("""{"x": 5, "y": 3}""");
Console.WriteLine(result1.GetRawText()); // 8

var result2 = await divideFunc.Invoke("""{"numerator": 10.0, "denominator": 2.0}""");
Console.WriteLine(result2.GetRawText()); // 5
```

The `JsonFunc` abstraction provides a uniform way to invoke arbitrary .NET methods using JSON-serialized parameters and return values, making it ideal for building RPC systems, HTTP API handlers, and other dynamic invocation scenarios. See the [core abstractions](core-abstractions.md#method-shapes) documentation for more details.
