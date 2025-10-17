# PolyType.Examples

Includes a set of reference library implementations built on top of the PolyType abstractions. These include:

* A serializer built on top of System.Text.Json,
* A serializer built on top of System.Xml,
* A serializer built on top of System.Formats.Cbor,
* A `ConfigurationBinder` like implementation,
* A dependency injection implementation,
* A simple pretty-printer for .NET values,
* A generic random value generator based on `System.Random`,
* A JSON schema generator for .NET types,
* An object graph cloning function,
* A structural `IEqualityComparer<T>` generator for POCOs and collections,
* An object validator in the style of System.ComponentModel.DataAnnotations.
* A simple .NET object mapper.

## RPC Generation with JsonFunc

The JSON serializer includes `JsonFunc` and `JsonEvent` abstractions that enable rapid development of RPC systems. These wrap .NET methods with JSON-based parameter marshaling, making it easy to build HTTP APIs, message-based RPC, or any dynamic invocation scenario.

### JsonFunc Example

```csharp
using PolyType;
using PolyType.Examples.JsonSerializer;

[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial class ApiService
{
    public async ValueTask<string> GreetAsync(string name)
    {
        await Task.Delay(10);
        return $"Hello, {name}!";
    }
}

var service = new ApiService();
var serviceShape = TypeShapeResolver.Resolve<ApiService>();
var greetMethod = serviceShape.Methods.First(m => m.Name == "GreetAsync");

// Create a JSON-based delegate
var jsonFunc = JsonSerializerTS.CreateJsonFunc(greetMethod, service);

// Invoke with JSON parameters
var result = await jsonFunc.Invoke("""{"name": "World"}""");
Console.WriteLine(result.GetRawText()); // "Hello, World!"
```

### JsonEvent Example

Event shapes can be wrapped with `JsonEvent` for dynamic event handling:

```csharp
[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial class NotificationService
{
    public event AsyncEventHandler<string>? OnNotification;
    
    public async ValueTask SendNotificationAsync(string message)
    {
        if (OnNotification != null)
        {
            await OnNotification(this, message, CancellationToken.None);
        }
    }
}

var service = new NotificationService();
var serviceShape = TypeShapeResolver.Resolve<NotificationService>();
var onNotificationEvent = serviceShape.Events.First(e => e.Name == "OnNotification");

var jsonEvent = JsonSerializerTS.CreateAsyncJsonEvent(onNotificationEvent, service);
jsonEvent.Subscribe(async (sender, parameters, ct) =>
{
    Console.WriteLine($"Received: {parameters["message"]}");
    return JsonDocument.Parse("{}").RootElement;
});
```

These abstractions demonstrate how PolyType's method and event shapes enable building complete RPC frameworks with minimal code.
