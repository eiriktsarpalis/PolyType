using PolyType.Examples.DependencyInjection;

namespace PolyType.Tests;

public partial class DependencyInjectionTests
{
    [Fact]
    public void ServiceCollection_Add_Scoped()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add<IFoo, FooImpl>();
        serviceCollection.Add<IBar, BarImpl>(ServiceLifetime.Scoped);

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();

        TestService testService = provider.GetRequiredService<TestService>();
        Assert.IsType<FooImpl>(testService.Foo);
        Assert.IsType<BarImpl>(testService.Bar);
        Assert.Null(testService.Baz);
        Assert.Equal(-1, testService.Number);

        TestService testService2 = provider.GetRequiredService<TestService>();
        Assert.Same(testService, testService2);

        using ServiceProvider provider2 = context.CreateScope();
        TestService testService3 = provider2.GetRequiredService<TestService>();
        Assert.NotSame(testService, testService3);
    }

    [Fact]
    public void ServiceCollection_Add_Transient()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add<IFoo, FooImpl>();
        serviceCollection.Add<IBar, BarImpl>(ServiceLifetime.Transient);

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();

        TestService testService = provider.GetRequiredService<TestService>();
        Assert.IsType<FooImpl>(testService.Foo);
        Assert.IsType<BarImpl>(testService.Bar);
        Assert.Null(testService.Baz);
        Assert.Equal(-1, testService.Number);

        TestService testService2 = provider.GetRequiredService<TestService>();
        Assert.NotSame(testService, testService2);
        Assert.Same(testService.Foo, testService2.Foo);
        Assert.NotSame(testService.Bar, testService2.Bar);
        Assert.Null(testService2.Baz);
        Assert.Equal(-1, testService2.Number);
    }

    [Fact]
    public void ServiceCollection_Add_Singleton()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add<IFoo, FooImpl>(ServiceLifetime.Scoped);
        serviceCollection.Add<IBar, BarImpl>(ServiceLifetime.Singleton);

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();
        TestService testService = provider.GetRequiredService<TestService>();
        Assert.IsType<FooImpl>(testService.Foo);
        Assert.IsType<BarImpl>(testService.Bar);
        Assert.Null(testService.Baz);
        Assert.Equal(-1, testService.Number);

        using ServiceProvider provider2 = context.CreateScope();
        TestService testService2 = provider2.GetRequiredService<TestService>();
        Assert.NotSame(testService, testService2);
        Assert.NotSame(testService.Foo, testService2.Foo);
        Assert.Same(testService.Bar, testService2.Bar);
        Assert.Null(testService2.Baz);
        Assert.Equal(-1, testService2.Number);
    }

    [Fact]
    public void ServiceCollection_Add_Values()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add<IFoo, FooImpl>(ServiceLifetime.Scoped);
        serviceCollection.Add<IBar, BarImpl>(ServiceLifetime.Singleton);
        serviceCollection.Add<IBaz>(serviceProvider => new BazImpl(), ServiceLifetime.Transient);
        serviceCollection.Add<int>(42);

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();
        TestService testService = provider.GetRequiredService<TestService>();
        Assert.IsType<FooImpl>(testService.Foo);
        Assert.IsType<BarImpl>(testService.Bar);
        Assert.IsType<BazImpl>(testService.Baz);
        Assert.Equal(42, testService.Number);

        TestService testService2 = provider.GetRequiredService<TestService>();
        Assert.NotSame(testService, testService2);
        Assert.Same(testService.Foo, testService2.Foo);
        Assert.Same(testService.Bar, testService2.Bar);
        Assert.NotSame(testService.Baz, testService2.Baz);
        Assert.Equal(42, testService2.Number);
    }

    [Fact]
    public void ServiceProvider_MissingValue_ThrowsInvalidOperationException()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add<IFoo, FooImpl>(ServiceLifetime.Scoped);
        serviceCollection.Add<IBaz>(serviceProvider => new BazImpl(), ServiceLifetime.Transient);

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<TestService>());
        Assert.Contains("IBar", ex.Message);
    }

    [Fact]
    public void ServiceProvider_InstantiatesEmptyCollections()
    {
        ServiceCollection serviceCollection = new();
        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();

        var service = provider.GetRequiredService<TestServiceWithCollections>();
        Assert.Empty(service.Foos);
        Assert.Empty(service.Bars);
    }

    [Fact]
    public void ServiceCollection_SingletonFactory_ScopedDependency()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add<IFoo, FooImpl>(ServiceLifetime.Scoped);
        serviceCollection.Add<IBar>(serviceProvider => new BarImpl { Value = serviceProvider.GetRequiredService<IFoo>().Value }, ServiceLifetime.Singleton);

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);

        using ServiceProvider provider = context.CreateScope();
        TestService testService = provider.GetRequiredService<TestService>();

        using ServiceProvider provider2 = context.CreateScope();
        TestService testService2 = provider2.GetRequiredService<TestService>();

        Assert.NotSame(testService, testService2);
        Assert.NotSame(testService.Foo, testService2.Foo);
        Assert.Same(testService.Bar, testService2.Bar);
    }

    [Fact]
    public void ServiceCollection_CyclicDependency_ThrowsInvalidOperationException()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add(42);
        serviceCollection.Add<BinTree>();

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();

        InvalidOperationException ex;
        ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<BinTree>());
        Assert.Contains("BinTree", ex.Message);
        Assert.Contains("cyclic", ex.Message);
    }

    [Fact]
    public void ServiceCollection_TypeWithSurrogate()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add<IFoo>(_ => new FooImpl { Value = 42 });

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();
        var value = provider.GetRequiredService<ClassWithSurrogate>();
        Assert.Equal(42, value.Value);
    }

    [Fact]
    public void ServiceCollection_NullableDependency()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add(42);

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();
        var value = provider.GetRequiredService<NullableValue>();
        Assert.Equal(42, value.Value);
    }

    [Fact]
    public void ServiceCollection_UnregisteredService_ThrowsInvalidOperationException()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.Add<IFoo, FooImpl>();

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using ServiceProvider provider = context.CreateScope();

        InvalidOperationException ex;
        ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IBar>());
        Assert.Contains("IBar", ex.Message);

        ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<TestService>());
        Assert.Contains("IBar", ex.Message);
    }

    [Fact]
    public void ServiceCollection_DisposableDependency_GetsDisposed()
    {
        DisposableFoo foo;
        ServiceCollection serviceCollection = new();
        serviceCollection.Add<IFoo, DisposableFoo>();
        serviceCollection.Add<IBar, BarImpl>();

        using ServiceProviderContext context = serviceCollection.Build(PolyType.SourceGenerator.ShapeProvider_PolyType_Tests.Default);
        using (ServiceProvider provider = context.CreateScope())
        {
            TestService testService = provider.GetRequiredService<TestService>();
            foo = Assert.IsType<DisposableFoo>(testService.Foo);
            Assert.False(foo.IsDisposed);
        }

        Assert.True(foo.IsDisposed);
    }

    [GenerateShape]
    public partial record TestService(IFoo Foo, IBar Bar, IBaz? Baz = null, int Number = -1);

    [GenerateShape]
    public partial record TestServiceWithCollections(List<IFoo> Foos, Dictionary<string, IBar> Bars);

    [GenerateShape]
    public partial class FooImpl : IFoo
    {
        public int Value { get; set; } = 42;
    }

    [GenerateShape]
    public partial class BarImpl : IBar
    {
        public int Value { get; set; } = 43;
    }

    [GenerateShape]
    public partial class BazImpl : IBaz
    {
        public int Value { get; set; } = 43;
    }

    [GenerateShape]
    public partial record BinTree(int Value, BinTree? Left = null, BinTree? Right = null);

    [GenerateShape]
    public partial record NullableValue(int? Value);

    public interface IFoo
    {
        public int Value { get; }
    }

    public interface IBar
    {
        public int Value { get; }
    }

    public interface IBaz
    {
        public int Value { get; }
    }

    [GenerateShape, TypeShape(Marshaler = typeof(Marshaler))]
    public partial record ClassWithSurrogate(int Value)
    {
        public sealed class Marshaler : IMarshaler<ClassWithSurrogate, IFoo>
        {
            public ClassWithSurrogate? Unmarshal(IFoo? surrogate) => surrogate is null ? null : new ClassWithSurrogate(surrogate.Value);
            public IFoo? Marshal(ClassWithSurrogate? value) => value is null ? null : new FooImpl { Value = value.Value };
        }
    }

    [GenerateShape]
    public partial class DisposableFoo : IFoo, IDisposable
    {
        public int Value { get; set; } = 42;
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }
}