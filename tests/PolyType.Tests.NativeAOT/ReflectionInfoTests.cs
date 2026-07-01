using PolyType.Abstractions;
using System.Reflection;

namespace PolyType.Tests.NativeAOT;

/// <summary>
/// Verifies that the reflection metadata exposed by source-generated shapes
/// (<see cref="IConstructorShape.MethodBase"/>, <see cref="IMethodShape.MethodBase"/> and
/// <see cref="IParameterShape.ParameterInfo"/>) resolves correctly under Native AOT.
/// These accessors are backed by <c>typeof(T).GetConstructor(...)</c> / <c>GetMethod(...)</c>
/// lookups in the generated code, so this doubles as a guard that reading them roots the
/// necessary member metadata and it is not trimmed away.
/// </summary>
public class ReflectionInfoTests
{
    [Test]
    public async Task ConstructorMethodBaseResolves()
    {
        var objectShape = TypeShapeResolver.Resolve<SimpleTestData>() as IObjectTypeShape<SimpleTestData>;
        await Assert.That(objectShape).IsNotNull();

        IConstructorShape? ctor = objectShape!.Constructor;
        await Assert.That(ctor).IsNotNull();

        MethodBase? methodBase = ctor!.MethodBase;
        await Assert.That(methodBase).IsNotNull();
        await Assert.That(methodBase is ConstructorInfo).IsTrue();
        await Assert.That(methodBase!.GetParameters().Length).IsEqualTo(5);
    }

    [Test]
    public async Task ConstructorParameterInfoResolves()
    {
        var objectShape = (IObjectTypeShape<SimpleTestData>)TypeShapeResolver.Resolve<SimpleTestData>();
        IConstructorShape ctor = objectShape.Constructor!;

        IParameterShape parameter = ctor.Parameters.First(p => p.Position == 0);
        ParameterInfo? parameterInfo = parameter.ParameterInfo;

        await Assert.That(parameterInfo).IsNotNull();
        await Assert.That(parameterInfo!.Name).IsEqualTo(nameof(SimpleTestData.IntValue));
        await Assert.That(parameterInfo.Position).IsEqualTo(0);
    }

    [Test]
    public async Task MethodBaseAndParameterInfoResolve()
    {
        var serviceShape = TypeShapeResolver.Resolve<TestRpcService>();
        IMethodShape method = serviceShape.Methods.First(m => m.Name == nameof(TestRpcService.AddPersonAsync));

        MethodBase? methodBase = method.MethodBase;
        await Assert.That(methodBase).IsNotNull();
        await Assert.That(methodBase is MethodInfo).IsTrue();
        await Assert.That(methodBase!.Name).IsEqualTo(nameof(TestRpcService.AddPersonAsync));

        IParameterShape parameter = method.Parameters.First(p => p.Name == "person");
        ParameterInfo? parameterInfo = parameter.ParameterInfo;
        await Assert.That(parameterInfo).IsNotNull();
        await Assert.That(parameterInfo!.Name).IsEqualTo("person");
    }
}
