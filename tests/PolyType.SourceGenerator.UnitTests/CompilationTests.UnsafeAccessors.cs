using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.SourceGenerator.Model;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static void AccessorParameterNames_PreserveSourceNames(bool isStatic, bool updatedMemorySafetyRules)
    {
        string source = $$"""
            using PolyType;

            [GenerateShapeFor(typeof(ParameterNames<int>))]
            public partial class Witness { }

            public class ParameterNames<T>
            {
                [ConstructorShape]
                private ParameterNames(
                    [ParameterShape(Name = "wire-value")] int @event,
                    int ctorInfo,
                    int paramArray,
                    ref int result,
                    int __InvokeConstructor,
                    int __BindingFlags_Instance_All,
                    int __s___CtorAccessor_ParameterNames_Int32_CtorInfo)
                {
                    Value = @event;
                    result += @event;
                }

                public int Value { get; }

                [MethodShape]
                private {{(isStatic ? "static " : "")}}int Update(
                    [ParameterShape(Name = "wire-target")] int target,
                    int target_,
                    ref int result,
                    out int methodInfo,
                    int CreateDelegate,
                    int __BindingFlags_All,
                    int __MethodAccessor_ParameterNames_Int32_0_Update_Delegate)
                {
                    result += target;
                    methodInfo = target_;
                    return result + methodInfo;
                }
            }
            """;

        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(LanguageVersion.Preview);
        if (updatedMemorySafetyRules)
        {
            parseOptions = parseOptions.WithFeatures([new("updated-memory-safety-rules", "true")]);
        }

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CompilationHelpers.CreateCompilation(source, parseOptions: parseOptions, nullableContextOptions: NullableContextOptions.Disable));
        ObjectShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<ObjectShapeModel>(),
            type => type.SourceIdentifier is "ParameterNames_Int32");
        MethodShapeModel method = Assert.Single(model.Methods);
        MethodDeclarationSyntax[] declarations = result.NewCompilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>())
            .ToArray();
        MethodDeclarationSyntax constructorAccessor = Assert.Single(declarations, declaration => declaration.Identifier.ValueText == "__CtorAccessor_ParameterNames_Int32");
        MethodDeclarationSyntax methodAccessor = Assert.Single(declarations, declaration => declaration.Identifier.ValueText == "__MethodAccessor_ParameterNames_Int32_0_Update");
        Assert.Equal(
            ["event", "ctorInfo", "paramArray", "result", "__InvokeConstructor", "__BindingFlags_Instance_All", "__s___CtorAccessor_ParameterNames_Int32_CtorInfo"],
            constructorAccessor.ParameterList.Parameters.Select(parameter => parameter.Identifier.ValueText));
        Assert.Equal("@event", constructorAccessor.ParameterList.Parameters[0].Identifier.Text);
        Assert.Equal(
            ["target", "target_", "result", "methodInfo", "CreateDelegate", "__BindingFlags_All", "__MethodAccessor_ParameterNames_Int32_0_Update_Delegate"],
            methodAccessor.ParameterList.Parameters.Skip(isStatic ? 0 : 1).Select(parameter => parameter.Identifier.ValueText));
        if (!isStatic)
        {
            Assert.Equal("target__", methodAccessor.ParameterList.Parameters[0].Identifier.ValueText);
        }

        using var stream = new MemoryStream();
        var emit = result.NewCompilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        Type provider = assembly.GetType(Assert.Single(result.GeneratedModels).ProviderDeclaration.Id.FullyQualifiedName.Replace("global::", ""))!;
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        Type ConstructorScope() => model.Constructor!.GenericDeclaringType is null ? provider : GenericScope();
        Type GenericScope() => provider.GetNestedType("__GenericAccessors_ParameterNames_Int32_0`1", System.Reflection.BindingFlags.NonPublic)!.MakeGenericType(typeof(int));
        object?[] constructorArguments = [5, 0, 0, 10, 0, 0, 0];
        object instance = ConstructorScope().GetMethod(constructorAccessor.Identifier.ValueText, flags)!.Invoke(null, constructorArguments)!;
        Assert.Equal(15, constructorArguments[3]);
        Assert.Equal(5, instance.GetType().GetProperty("Value")!.GetValue(instance));

        Type methodScope = method.GenericDeclaringType is null ? provider : GenericScope();
        object?[] methodArguments = isStatic ? [2, 3, 10, null, 0, 0, 0] : [instance, 2, 3, 10, null, 0, 0, 0];
        Assert.Equal(15, methodScope.GetMethod(methodAccessor.Identifier.ValueText, flags)!.Invoke(null, methodArguments));
        Assert.Equal(12, methodArguments[isStatic ? 2 : 3]);
        Assert.Equal(3, methodArguments[isStatic ? 3 : 4]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void GenericUnsafeAccessors_PreserveOpenSignaturesAndConstraints(bool updatedMemorySafetyRules)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using PolyType;

            [GenerateShapeFor(typeof(Accessors<int, List<int>>))]
            [GenerateShapeFor(typeof(Leaf))]
            [GenerateShapeFor(typeof(Outer<int>.Nested<string>))]
            public partial class Witness { }

            public class Accessors<@class, TCollection>
                where @class : struct
                where TCollection : ICollection<@class>, new()
            {
                [ConstructorShape]
                private Accessors([ParameterShape(Name = "wire-value")] @class @event, in TCollection @params, int count)
                {
                    Value = @event;
                    Values = @params;
                    Count = count;
                }

                [PropertyShape]
                private @class Value { get; set; }

                [PropertyShape]
                private string Label { get; set; } = "label";

                public TCollection Values { get; init; }
                public int Count { get; }

                [MethodShape]
                private ref @class Echo([ParameterShape(Name = "wire-value")] ref @class @event, out TCollection @params)
                {
                    @params = new();
                    return ref @event;
                }

                [EventShape]
                private event Action<@class> Changed { add { } remove { } }
            }

            public class GrandBase<TGrand> where TGrand : class
            {
                [PropertyShape]
                private TGrand GrandValue { get; set; }
            }

            public class Intermediate<TValue> : GrandBase<string> where TValue : struct
            {
                [PropertyShape]
                private TValue Value { get; set; }
            }

            public class Leaf : Intermediate<int> { }

            public class Outer<T>
            {
                public class Nested<U>
                {
                    [ConstructorShape]
                    private Nested(T value, U item) { Value = value; Item = item; }

                    public T Value { get; }
                    public U Item { get; }
                }
            }
            """;

        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(
            updatedMemorySafetyRules ? LanguageVersion.Preview : CompilationHelpers.DefaultLanguageVersion);
        if (updatedMemorySafetyRules)
        {
            parseOptions = parseOptions.WithFeatures([new("updated-memory-safety-rules", "true")]);
        }

        var compilation = (CSharpCompilation)CompilationHelpers.CreateCompilation(source, parseOptions: parseOptions,
            nullableContextOptions: NullableContextOptions.Disable);
        compilation = compilation.WithOptions(compilation.Options.WithAllowUnsafe(false));
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(compilation);
        TypeShapeProviderModel provider = Assert.Single(result.GeneratedModels);
        Assert.Equal(updatedMemorySafetyRules, provider.UsesUpdatedMemorySafetyRules);

        ObjectShapeModel genericType = result.AllGeneratedTypes.OfType<ObjectShapeModel>()
            .Single(t => t.SourceIdentifier.StartsWith("Accessors_", StringComparison.Ordinal));
        ObjectShapeModel leaf = result.AllGeneratedTypes.OfType<ObjectShapeModel>()
            .Single(t => t.SourceIdentifier is "Leaf");
        ObjectShapeModel nested = result.AllGeneratedTypes.OfType<ObjectShapeModel>()
            .Single(t => t.Type.FullyQualifiedName.Contains(".Nested<"));

        Assert.False(nested.Constructor!.CanUseUnsafeAccessors);
        Assert.Null(nested.Constructor.GenericDeclaringType);

        ClassDeclarationSyntax[] accessorClasses = result.NewCompilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(declaration => declaration.Identifier.ValueText.StartsWith("__GenericAccessors_", StringComparison.Ordinal))
            .ToArray();
        Assert.All(accessorClasses.GroupBy(declaration => declaration.Identifier.ValueText), group => Assert.Single(group));
        Assert.All(accessorClasses, declaration => Assert.DoesNotContain(declaration.Modifiers, modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));

#if NET9_0_OR_GREATER
        Assert.True(genericType.Constructor!.CanUseUnsafeAccessors);
        GenericTypeModel definition = Assert.IsType<GenericTypeModel>(genericType.Constructor.GenericDeclaringType);
        Assert.Equal(["@class", "TCollection"], definition.TypeParameters);
        Assert.Contains("where @class : struct", definition.ConstraintClauses);
        Assert.Contains("ICollection<@class>", definition.ConstraintClauses);
        Assert.Equal("@class", genericType.Constructor.Parameters[0].OpenParameterTypeName);
        Assert.Equal("TCollection", genericType.Constructor.Parameters[1].OpenParameterTypeName);
        Assert.Null(genericType.Constructor.Parameters[2].OpenParameterTypeName);
        Assert.Null(Assert.Single(genericType.Properties, p => p.Name is "Label").OpenPropertyTypeName);
        Assert.Equal("@class", Assert.Single(genericType.Methods).OpenReturnTypeName);
        Assert.Contains("Action<@class>", Assert.Single(genericType.Events).OpenHandlerTypeName);
        Assert.Equal([1, 2], leaf.Properties.Select(p => p.DeclaringTypeIndex).OrderBy(i => i));
        Assert.All(leaf.Properties, p => Assert.NotNull(p.GenericDeclaringType));
        ClassDeclarationSyntax accessorClass = Assert.Single(accessorClasses,
            declaration => declaration.Identifier.ValueText == $"__GenericAccessors_{genericType.SourceIdentifier}_0");
        string[] accessorNames = accessorClass.Members.OfType<MethodDeclarationSyntax>().Select(method => method.Identifier.ValueText).ToArray();
        Assert.Contains($"__CtorAccessor_{genericType.SourceIdentifier}", accessorNames);
        Assert.Contains(accessorNames, name => name.StartsWith("__GetAccessor_", StringComparison.Ordinal));
        Assert.Contains(accessorNames, name => name.StartsWith("__SetAccessor_", StringComparison.Ordinal));
        Assert.Contains(accessorNames, name => name.StartsWith("__MethodAccessor_", StringComparison.Ordinal));
        Assert.Contains(accessorNames, name => name.StartsWith("__EventAccessor_", StringComparison.Ordinal));
#else
        Assert.False(genericType.Constructor!.CanUseUnsafeAccessors);
        Assert.Null(genericType.Constructor.GenericDeclaringType);
        Assert.All(leaf.Properties, p => Assert.Null(p.GenericDeclaringType));
        Assert.Empty(accessorClasses);
#endif

        foreach (MethodDeclarationSyntax accessor in result.NewCompilation.SyntaxTrees
            .SelectMany(t => t.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(m => m.Modifiers.Any(SyntaxKind.ExternKeyword)))
        {
            Assert.Equal(updatedMemorySafetyRules, accessor.Modifiers.Any(m => m.Text is "safe"));
        }

        PolyTypeSourceGeneratorResult secondResult = CompilationHelpers.RunPolyTypeSourceGenerator(
            CompilationHelpers.CreateCompilation(source, parseOptions: parseOptions, nullableContextOptions: NullableContextOptions.Disable));
        Assert.Equal(result.GeneratedModels, secondResult.GeneratedModels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void GenericStructAccessors_RespectRuntimeCompatibility(bool updatedMemorySafetyRules)
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;
            using PolyType;
            using PolyType.Abstractions;

            public struct GenericStruct<T>
            {
                [ConstructorShape]
                private GenericStruct(T value) { }

                [PropertyShape]
                private T Value
                {
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    get => throw new InvalidOperationException("Getter failure.");
                    set { }
                }
            }

            [GenerateShapeFor(typeof(GenericStruct<string>))]
            public partial class Witness { }

            public static class Entry
            {
                public static string Run()
                {
                    var shape = (IObjectTypeShape<GenericStruct<string>>)Witness.GeneratedTypeShapeProvider.GetTypeShape(typeof(GenericStruct<string>));
                    var property = (IPropertyShape<GenericStruct<string>, string>)shape.Properties[0];
                    var value = new GenericStruct<string>();
                    try
                    {
                        property.GetGetter()(ref value);
                        return "No exception.";
                    }
                    catch (InvalidOperationException exception)
                    {
                        return exception.Message;
                    }
                }
            }
            """;

        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(
            updatedMemorySafetyRules ? LanguageVersion.Preview : CompilationHelpers.DefaultLanguageVersion);
        if (updatedMemorySafetyRules)
        {
            parseOptions = parseOptions.WithFeatures([new("updated-memory-safety-rules", "true")]);
        }

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CompilationHelpers.CreateCompilation(source, parseOptions: parseOptions, nullableContextOptions: NullableContextOptions.Disable));
        ObjectShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<ObjectShapeModel>(),
            t => t.SourceIdentifier.StartsWith("GenericStruct_", StringComparison.Ordinal));
        int runtimeMajorVersion = typeof(object).Assembly.GetName().Version!.Major;
        Assert.Equal(runtimeMajorVersion >= 9, model.Constructor!.CanUseUnsafeAccessors);
        Assert.Equal(runtimeMajorVersion >= 11, Assert.Single(model.Properties).CanUseUnsafeAccessors);

        using var stream = new MemoryStream();
        Assert.True(result.NewCompilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken).Success);
        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        Assert.Equal("Getter failure.", assembly.GetType("Entry")!.GetMethod("Run")!.Invoke(null, null));
    }

    [Theory]
    [InlineData("int")]
    [InlineData("string")]
    public static void PartialPropertyOverrides_UseInheritedAccessors(string typeArgument)
    {
        string source = $$"""
            using PolyType;

            public class Base<T>
            {
                [PropertyShape]
                public virtual T GetterOverride { get; protected set; }

                [PropertyShape]
                public virtual T SetterOverride { protected get; set; }
            }

            public class Derived<T> : Base<T>
            {
                [PropertyShape]
                public override T GetterOverride => base.GetterOverride;

                [PropertyShape]
                public override T SetterOverride { set => base.SetterOverride = value; }
            }

            [GenerateShapeFor(typeof(Derived<{{typeArgument}}>))]
            public partial class Witness { }
            """;

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CompilationHelpers.CreateCompilation(source, nullableContextOptions: NullableContextOptions.Disable));
        ObjectShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<ObjectShapeModel>(), t => t.SourceIdentifier.StartsWith("Derived_", StringComparison.Ordinal));
        Assert.Equal(2, model.Properties.Length);
        Assert.All(model.Properties, property =>
        {
            Assert.True(property.EmitGetter);
            Assert.True(property.EmitSetter);
            Assert.False(property.CanUseUnsafeAccessors);
            Assert.Null(property.GenericDeclaringType);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void InlineArrayCodegen_RespectsMemorySafetyRules(bool updatedMemorySafetyRules)
    {
        const string source = """
            using PolyType;

            [GenerateShape]
            public partial struct Buffer
            {
                public const int Length = 3;
                public unsafe fixed int Values[Length];
            }
            """;

        CSharpParseOptions parseOptions = CompilationHelpers.CreateParseOptions(LanguageVersion.Preview);
        if (updatedMemorySafetyRules)
        {
            parseOptions = parseOptions.WithFeatures([new("updated-memory-safety-rules", "true")]);
        }

        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CompilationHelpers.CreateCompilation(source, parseOptions: parseOptions));
        Assert.Contains(result.AllGeneratedTypes, t => t is EnumerableShapeModel { Kind: Roslyn.EnumerableKind.InlineArrayOfT });
        string generated = string.Join("\n", result.NewCompilation.SyntaxTrees.Skip(1).Select(t => t.ToString()));
        Assert.Equal(updatedMemorySafetyRules, generated.Contains("unsafe\r\n") || generated.Contains("unsafe\n"));

        AssignmentExpressionSyntax constructor = Assert.Single(result.NewCompilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<AssignmentExpressionSyntax>()),
            assignment => assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "ParameterizedConstructor" });
        var methodReference = Assert.IsType<IdentifierNameSyntax>(constructor.Right);
        MethodDeclarationSyntax helper = Assert.Single(result.NewCompilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>()),
            method => method.Identifier.ValueText == methodReference.Identifier.ValueText);
        Assert.Contains(helper.Modifiers, modifier => modifier.IsKind(SyntaxKind.PrivateKeyword));
        Assert.Contains(helper.Modifiers, modifier => modifier.IsKind(SyntaxKind.StaticKeyword));
        AssertBlockIndentation(Assert.IsType<BlockSyntax>(helper.Body), GetColumn(helper));

        static void AssertBlockIndentation(BlockSyntax block, int indentation)
        {
            Assert.Equal(indentation, GetColumn(block.OpenBraceToken));
            Assert.Equal(indentation, GetColumn(block.CloseBraceToken));
            foreach (StatementSyntax statement in block.Statements)
            {
                Assert.Equal(indentation + 4, GetColumn(statement));
                BlockSyntax? nestedBlock = statement switch
                {
                    BlockSyntax nested => nested,
                    UnsafeStatementSyntax unsafeStatement => unsafeStatement.Block,
                    IfStatementSyntax { Statement: BlockSyntax nested } => nested,
                    ForStatementSyntax { Statement: BlockSyntax nested } => nested,
                    _ => null,
                };

                if (nestedBlock is not null)
                {
                    AssertBlockIndentation(nestedBlock, indentation + 4);
                }
            }
        }

        static int GetColumn(SyntaxNodeOrToken node)
        {
            Location location = Assert.IsAssignableFrom<Location>(node.GetLocation());
            return location.GetLineSpan().StartLinePosition.Character;
        }
    }
}
