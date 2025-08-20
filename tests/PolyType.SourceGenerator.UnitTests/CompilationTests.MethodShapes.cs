using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    [Fact]
    public static void ClassWithMethodShapes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial class ClassWithMethods
        {
            public ClassWithMethods(string name) => Name = name;
            
            public string Name { get; }

            public void DoSomething() { }

            public int Calculate(int x, int y) => x + y;

            [MethodShape]
            private void AuthorizePayments() { }
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void StructWithMethodShapes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
        public partial struct StructWithMethods
        {
            public int Value { get; set; }
            
            public void Reset() => Value = 0;
            
            public readonly int GetDoubleValue() => Value * 2;
            
            public static StructWithMethods Create(int value) => new() { Value = value };
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void RecordWithMethodShapes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial record RecordWithMethods(string Name, int Age)
        {
            public string GetDisplayText() => $"{Name} ({Age})";
            
            public static RecordWithMethods CreateDefault() => new("Unknown", 0);
            
            public RecordWithMethods IncrementAge() => this with { Age = Age + 1 };
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void AsyncMethodShapes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;
        using System.Threading.Tasks;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial class AsyncService
        {
            public async Task ProcessAsync()
            {
                await Task.Delay(1);
            }
            
            public async Task<string> GetDataAsync(int id)
            {
                await Task.Delay(1);
                return $"Data-{id}";
            }
            
            public async ValueTask<int> CalculateAsync(int x, int y)
            {
                await Task.Delay(1);
                return x + y;
            }
            
            public static async Task<bool> ValidateAsync(string input)
            {
                await Task.Delay(1);
                return !string.IsNullOrEmpty(input);
            }
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodShapeFlags_PublicInstanceOnly_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
        public partial class ServiceWithInstanceMethods
        {
            public void InstanceMethod() { }
            
            public static void StaticMethod() { } // Should be excluded
            
            private void PrivateMethod() { } // Should be excluded
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodShapeFlags_PublicStaticOnly_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.PublicStatic)]
        public partial class ServiceWithStaticMethods
        {
            public void InstanceMethod() { } // Should be excluded
            
            public static void StaticMethod() { }
            
            public static int CalculateStatic(int x) => x * 2;
            
            private static void PrivateStaticMethod() { } // Should be excluded
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodShapeFlags_None_WithExplicitAttributes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.None)]
        public partial class ServiceWithExplicitMethods
        {
            public void PublicMethod() { } // Should be excluded
            
            [MethodShape]
            public void ExplicitPublicMethod() { } // Should be included
            
            [MethodShape]
            private void ExplicitPrivateMethod() { } // Should be included
            
            [MethodShape(Ignore = true)]
            public void IgnoredMethod() { } // Should be excluded
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodsWithComplexParameters_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;
        using System.Collections.Generic;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial class ServiceWithComplexParameters
        {
            public void ProcessList(List<string> items) { }
            
            public void ProcessOptional(string required, int optional = 42, bool? nullable = null) { }
            
            public string ProcessParams(params int[] numbers) => string.Join(",", numbers);
            
            public ref int GetReference(ref int value) => ref value;
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodsWithCustomNames_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial class ServiceWithCustomNames
        {
            [MethodShape(Name = "CustomProcessName")]
            public void Process() { }
            
            [MethodShape(Name = "CustomCalculateName")]
            public static int Calculate(
                [ParameterShape(Name = "FirstNumber")] int x, 
                [ParameterShape(Name = "SecondNumber")] int y) => x + y;
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void GenericClassWithMethodShapes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public class GenericService<T>
        {
            public void ProcessItem(T item) { }
            
            public T CreateDefault() => default(T)!;
        }

        [GenerateShapeFor(typeof(GenericService<string>))]
        public partial class Witness { }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void InterfaceWithMethodShapes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public interface IProcessor
        {
            void Process(string input);
            int Calculate(int x, int y);
        }

        [GenerateShapeFor(typeof(IProcessor))]
        public partial class Witness { }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodShapes_WithInheritance_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        public abstract class BaseService
        {
            public virtual void BaseMethod() { }
            public abstract void AbstractMethod();
        }

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial class DerivedService : BaseService
        {
            public override void AbstractMethod() { }
            
            public override void BaseMethod() { }
            
            public void DerivedMethod() { }
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodShapes_WithEvents_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;
        using System;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial class ServiceWithEvents
        {
            public event Action<string>? DataProcessed;
            
            public void ProcessData(string data)
            {
                DataProcessed?.Invoke(data);
            }
            
            public void Subscribe(Action<string> handler)
            {
                DataProcessed += handler;
            }
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodShapeWithOverloads_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial class ServiceWithOverloads
        {
            public void Process() { }
            
            public void Process(string input) { }
            
            public void Process(string input, int count) { }
            
            public void Process(int number) { }
            
            public static string Format(object value) => value?.ToString() ?? string.Empty;
            
            public static string Format(string format, params object[] args) => string.Format(format, args);
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodShapeWithPropertyAndMethodMixed_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial class ServiceWithMixedMembers
        {
            public string Name { get; set; } = string.Empty;
            
            public int Count { get; private set; }
            
            public void IncrementCount() => Count++;
            
            public void SetName(string name) => Name = name;
            
            public string GetDisplayName() => $"{Name} ({Count})";
            
            public static ServiceWithMixedMembers Create(string name) => new() { Name = name };
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("public ref int GetRef(ref int value) => ref value;")]
    [InlineData("public ref readonly int GetRefReadonly(ref readonly int value) => ref value;")]
    [InlineData("public int ProcessIn(in int value) => value * 2;")]
    [InlineData("public void ModifyRef(ref string text) => text = text.ToUpper();")]
    [InlineData("public ref readonly string GetRefReadonlyString(ref readonly string text) => ref text;")]
    [InlineData("public bool CompareIn(in int first, in int second) => first == second;")]
    public static void MethodsWithRefParameters_NoErrors(string methodDeclaration)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
        using PolyType;

        [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
        public partial class ServiceWithRefParameters
        {
            {{methodDeclaration}}
        }
        """, parseOptions: CompilationHelpers.CreateParseOptions(LanguageVersion.CSharp12));

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void MethodDiamondAmbiguity_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
        using PolyType;

        public interface IBase1
        {
            void DoSomething();
        }

        public interface IBase2
        {
            void DoSomething();
        }

        [GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
        public partial interface IDerived : IBase1, IBase2
        {
        }
        """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
}
