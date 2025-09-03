using PolyType.Abstractions;
using PolyType.Examples.JsonSerializer;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Tests for JsonFunction invocation in Native AOT.
/// </summary>
public class JsonFunctionTests
{
    [Test]
    public async Task InvokeGetPeopleAsync()
    {
        const string ExpectedJson = """
            [{"Name":"Person 1","Age":20},{"Name":"Person 2","Age":21},{"Name":"Person 3","Age":22},{"Name":"Person 4","Age":23},{"Name":"Person 5","Age":24}]
            """;

        // Arrange
        var service = new TestRpcService();
        var serviceShape = TypeShapeProvider.Resolve<TestRpcService>();
        var addMethodShape = serviceShape.Methods.First(m => m.Name == nameof(TestRpcService.GetPeopleAsync));
        var jsonFunc = JsonSerializerTS.CreateJsonFunc(addMethodShape, service);

        // Act
        var result = await jsonFunc.Invoke("""{"count": 5}""");

        // Assert
        await Assert.That(result.GetRawText()).IsEqualTo(ExpectedJson);
        await Assert.That(service.InvocationCount).IsEqualTo(1);
    }

    [Test]
    public async Task InvokeAddPersonAsync()
    {
        // Arrange
        var service = new TestRpcService();
        var serviceShape = TypeShapeProvider.Resolve<TestRpcService>();
        var addMethodShape = serviceShape.Methods.First(m => m.Name == nameof(TestRpcService.AddPersonAsync));
        var jsonFunc = JsonSerializerTS.CreateJsonFunc(addMethodShape, service);

        // Act
        var result = await jsonFunc.Invoke("""{"person": {"Name": "John", "Age": 30}}""");

        // Assert
        await Assert.That(result.GetRawText()).IsEqualTo("\"Person John added successfully.\"");
        await Assert.That(service.InvocationCount).IsEqualTo(1);
    }

    [Test]
    public async Task SubscribeJsonEventHandler()
    {
        // Arrange
        var service = new TestRpcService();
        var serviceShape = TypeShapeProvider.Resolve<TestRpcService>();
        var onInvokedEvent = serviceShape.Events.First(m => m.Name == nameof(TestRpcService.OnInvoked));
        var jsonEvent = JsonSerializerTS.CreateAsyncJsonEvent(onInvokedEvent, service);
        int eventTriggerCount = 0;
        using CancellationTokenSource cts = new();
        IDisposable sub = jsonEvent.Subscribe(async (sender, args, ct) =>
        {
            await Assert.That(sender).IsSameReferenceAs(service);
            await Assert.That(ct).IsEquatableOrEqualTo(cts.Token);
            eventTriggerCount++;
            return JsonDocument.Parse("{}").RootElement;
        });

        // Act
        await service.AddPersonAsync(new TestPerson("Me", 24), cts.Token);

        // Assert
        await Assert.That(eventTriggerCount).IsEqualTo(1);

        // Act - trigger event again after disposing
        sub.Dispose();
        await service.AddPersonAsync(new TestPerson("Me", 24), cts.Token);

        // Assert - event handler should not be called again
        await Assert.That(eventTriggerCount).IsEqualTo(1);
    }
}

/// <summary>
/// Test RPC service for JsonFunction testing.
/// </summary>
[GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial class TestRpcService
{
    public int InvocationCount { get; private set; }

    public event AsyncEventHandler<int>? OnInvoked;

    public async IAsyncEnumerable<TestPerson> GetPeopleAsync(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await TriggerEvent(cancellationToken);
        InvocationCount++;

        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return new TestPerson($"Person {i + 1}", 20 + i);
        }
    }

    public async ValueTask<string> AddPersonAsync(TestPerson person, CancellationToken cancellationToken = default)
    {
        await TriggerEvent(cancellationToken);
        await Task.Delay(10); // Simulate some processing
        InvocationCount++;
        return $"Person {person.Name} added successfully.";
    }

    private async ValueTask TriggerEvent(CancellationToken cancellationToken)
    {
        if (OnInvoked is not null)
        {
            await OnInvoked(this, InvocationCount, cancellationToken);
        }
    }
}

public record TestPerson(string Name, int Age);

public delegate ValueTask AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e, CancellationToken cancellationToken);