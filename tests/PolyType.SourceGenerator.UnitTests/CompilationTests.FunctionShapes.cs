using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    [Fact]
    public static void DelegateShapes_Simple_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;

            [GenerateShapeFor(typeof(Action))]
            [GenerateShapeFor(typeof(Func<int, string>))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void CustomDelegateShapes_WithAsyncAndRefKinds_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using PolyType;

            public delegate ValueTask<bool> AsyncHandler(ref int a, in string s, CancellationToken token);
            public delegate void ComplexDelegate(ref int a, in DateTime t, params int[] rest);

            [GenerateShapeFor(typeof(AsyncHandler))]
            [GenerateShapeFor(typeof(ComplexDelegate))]
            public partial class Witness { }
            """, parseOptions: CompilationHelpers.CreateParseOptions(LanguageVersion.CSharp12));

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void EventShapes_PublicInstanceAndStatic_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            #pragma warning disable CS0067 // Event is never used
            using System;
            using PolyType;

            [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
            public partial class ServiceWithEvents
            {
                public event Action? OnSomething;
                public static event EventHandler? OnGlobal;

                [EventShape]
                private event Action<int>? Hidden;
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void EventShapes_WithCustomNames_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            #pragma warning disable CS0067 // Event is never used
            using System;
            using PolyType;

            [GenerateShape, TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
            public partial class ServiceWithNamedEvents
            {
                [EventShape(Name = "CustomOnData")] public event Action<string>? OnData;
                [EventShape(Name = "CustomOnTick")] public static event Action? OnTick;
            }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public static void InterfaceWithEventShapes_NoErrors()
    {
        Compilation compilation = CompilationHelpers.CreateCompilation("""
            using System;
            using PolyType;

            [TypeShape(IncludeMethods = MethodShapeFlags.AllPublic)]
            public interface IEmitter
            {
                event Action<int>? Progress;
            }

            [GenerateShapeFor(typeof(IEmitter))]
            public partial class Witness { }
            """);

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        Assert.Empty(result.Diagnostics);
    }
}
