using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    [Theory]
    [InlineData("System", "Half")]
    [InlineData("System", "Int128")]
    [InlineData("System", "UInt128")]
    [InlineData("System", "DateOnly")]
    [InlineData("System", "TimeOnly")]
    [InlineData("System.Text", "Rune")]
    public static void ScalarPolyfills_PreserveObjectMembers(string typeNamespace, string typeName)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;
            using PolyType.Abstractions;
            using PolyType.ReflectionProvider;

            namespace {{typeNamespace}}
            {
                public struct {{typeName}}
                {
                    public int Value { get; set; }
                }
            }

            [GenerateShapeFor(typeof({{typeNamespace}}.{{typeName}}))]
            public partial class Witness { }

            public static class Entry
            {
                public static string Run()
                {
                    var generated = (IObjectTypeShape<{{typeNamespace}}.{{typeName}}>)
                        Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof({{typeNamespace}}.{{typeName}}))!;
                    var reflected = (IObjectTypeShape<{{typeNamespace}}.{{typeName}}>)
                        ReflectionTypeShapeProvider.Default.GetTypeShape(typeof({{typeNamespace}}.{{typeName}}))!;
                    return Describe(generated) + ";" + Describe(reflected);
                }

                private static string Describe(IObjectTypeShape<{{typeNamespace}}.{{typeName}}> shape)
                {
                    if (shape.Properties.Count != 1 || shape.Constructor is null)
                    {
                        return shape.Properties.Count + ":" + (shape.Constructor is not null);
                    }

                    var property = (IPropertyShape<{{typeNamespace}}.{{typeName}}, int>)shape.Properties[0];
                    {{typeNamespace}}.{{typeName}} value = default;
                    property.GetSetter()(ref value, 42);
                    return property.Name + ":" + property.GetGetter()(ref value) + ":" + shape.Constructor.Parameters.Count;
                }
            }
            """, assemblyName: "ScalarPolyfill_" + Guid.NewGuid().ToString("N"));
        compilation = compilation.WithOptions(((CSharpCompilationOptions)compilation.Options)
            .WithSpecificDiagnosticOptions(compilation.Options.SpecificDiagnosticOptions.SetItem("CS0436", ReportDiagnostic.Suppress)));
        Assert.Equal("Value:42:0;Value:42:0", RunScalarShapeTest(compilation));
    }

#if NET
    [Theory]
    [InlineData("System.Half")]
    [InlineData("System.Int128")]
    [InlineData("System.UInt128")]
    [InlineData("System.DateOnly")]
    [InlineData("System.TimeOnly")]
    [InlineData("System.Text.Rune")]
    public static void FrameworkScalars_RemainOpaque(string typeName)
    {
        Compilation compilation = CompilationHelpers.CreateCompilation($$"""
            using PolyType;
            using PolyType.Abstractions;
            using PolyType.ReflectionProvider;

            [GenerateShapeFor(typeof({{typeName}}))]
            public partial class Witness { }

            public static class Entry
            {
                public static string Run()
                {
                    var generated = (IObjectTypeShape)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof({{typeName}}))!;
                    var reflected = (IObjectTypeShape)ReflectionTypeShapeProvider.Default.GetTypeShape(typeof({{typeName}}))!;
                    return generated.Properties.Count + ":" + (generated.Constructor is not null) + ";" +
                        reflected.Properties.Count + ":" + (reflected.Constructor is not null);
                }
            }
            """, assemblyName: "FrameworkScalar_" + Guid.NewGuid().ToString("N"));
        Assert.Equal("0:False;0:False", RunScalarShapeTest(compilation));
    }
#endif

    private static string RunScalarShapeTest(Compilation compilation)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        using var image = new MemoryStream();
        var emitted = result.NewCompilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        Assembly assembly = Assembly.Load(image.ToArray());
        var run = (Func<string>)assembly.GetType("Entry")!.GetMethod("Run")!.CreateDelegate(typeof(Func<string>));
        return run();
    }
}
