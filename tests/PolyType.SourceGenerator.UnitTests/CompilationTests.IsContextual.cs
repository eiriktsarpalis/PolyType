using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Abstractions;
using PolyType.SourceGenerator.Model;
using PolyType.SourceGenModel;
using System.Reflection;
using Xunit;

namespace PolyType.SourceGenerator.UnitTests;

public static partial class CompilationTests
{
    internal const string IsContextualHierarchySource = """
        using System.Collections.Generic;
        using PolyType;

        namespace TestNamespace
        {
            [DerivedTypeShape(typeof(ObjectBase))]
            [DerivedTypeShape(typeof(ObjectDerived))]
            public class ObjectBase
            {
                public ObjectBase? Next { get; set; }
            }
            public sealed class ObjectDerived : ObjectBase { }

            [DerivedTypeShape(typeof(ListBase))]
            [DerivedTypeShape(typeof(ListDerived))]
            public class ListBase : List<ListBase> { }
            public sealed class ListDerived : ListBase { }

            [DerivedTypeShape(typeof(DictionaryBase))]
            [DerivedTypeShape(typeof(DictionaryDerived))]
            public class DictionaryBase : Dictionary<string, DictionaryBase> { }
            public sealed class DictionaryDerived : DictionaryBase { }

            [GenerateShapeFor(typeof(ObjectBase))]
            [GenerateShapeFor(typeof(ListBase))]
            [GenerateShapeFor(typeof(DictionaryBase))]
            public partial class Witness { }
        }
        """;

    internal const string IsContextualFSharpUnionSource = """
        using Microsoft.FSharp.Core;
        using PolyType;

        namespace TestNamespace
        {
            [GenerateShapeFor(typeof(FSharpResult<string, int>))]
            [GenerateShapeFor(typeof(FSharpChoice<string, int>))]
            public partial class Witness { }
        }
        """;

    internal const string IsContextualCSharpUnionSource = """
        using PolyType;

        namespace TestNamespace
        {
            public union RecursiveUnion(int, RecursiveUnion, string?);

            [GenerateShapeFor(typeof(RecursiveUnion))]
            public partial class Witness { }
        }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void IsContextual_HierarchyViewsAreContextual(bool accessContextualFirst)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CompilationHelpers.CreateCompilation(IsContextualHierarchySource, assemblyName: "ContextualHierarchy_" + Guid.NewGuid().ToString("N")));
        UnionShapeModel[] models = result.AllGeneratedTypes.OfType<UnionShapeModel>().ToArray();
        Assert.Equal(3, models.Length);
        Assert.Single(models, model => model.UnderlyingModel is ObjectShapeModel);
        Assert.Single(models, model => model.UnderlyingModel is EnumerableShapeModel);
        Assert.Single(models, model => model.UnderlyingModel is DictionaryShapeModel);
        ITypeShapeProvider provider = LoadContextualTestProvider(result);

        foreach (UnionShapeModel model in models)
        {
            Assert.True(model.UnderlyingModel.IsContextual);
            AssertContextualInitializer(result, model.UnderlyingModel);
            ITypeShape? detachedBase = null;
            if (accessContextualFirst)
            {
                detachedBase = GetGeneratedShapeProperty(provider, model.UnderlyingModel);
                Assert.True(detachedBase.IsContextual);
            }

            IUnionTypeShape union = Assert.IsAssignableFrom<IUnionTypeShape>(GetGeneratedShapeProperty(provider, model));
            Assert.False(union.IsContextual);
            Assert.Same(union, provider.GetTypeShape(union.Type));
            Assert.True(union.BaseType.IsContextual);
            detachedBase ??= GetGeneratedShapeProperty(provider, model.UnderlyingModel);
            Assert.Same(detachedBase, union.BaseType);
            Assert.Equal(union.Type, detachedBase.Type);

            UnionCaseModel selfCase = Assert.Single(model.UnionCases, unionCase => unionCase.IsBaseType);
            Assert.Same(detachedBase, union.UnionCases[selfCase.Index].UnionCaseType);
            Assert.True(union.UnionCases[selfCase.Index].UnionCaseType.IsContextual);

            UnionCaseModel derivedCase = Assert.Single(model.UnionCases, unionCase => !unionCase.IsBaseType);
            ITypeShape derivedShape = union.UnionCases[derivedCase.Index].UnionCaseType;
            Assert.False(derivedShape.IsContextual);
            Assert.Same(derivedShape, provider.GetTypeShape(derivedShape.Type));

            ITypeShape recursiveShape = detachedBase switch
            {
                IObjectTypeShape objectShape => Assert.Single(objectShape.Properties).PropertyType,
                IEnumerableTypeShape enumerableShape => enumerableShape.ElementType,
                IDictionaryTypeShape dictionaryShape => dictionaryShape.ValueType,
                _ => throw new InvalidOperationException(),
            };
            Assert.False(recursiveShape.IsContextual);
            Assert.Same(union, recursiveShape);
            Assert.True(detachedBase.IsContextual);
            Assert.False(union.IsContextual);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void IsContextual_FSharpUnionBodiesAreContextual(bool accessContextualFirst)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CompilationHelpers.CreateCompilation(IsContextualFSharpUnionSource, assemblyName: "ContextualFSharpUnion_" + Guid.NewGuid().ToString("N")));
        FSharpUnionShapeModel[] models = result.AllGeneratedTypes.OfType<FSharpUnionShapeModel>().ToArray();
        Assert.Equal(2, models.Length);
        Assert.Contains(models, model => model.UnionCases.All(unionCase => unionCase.TypeModel.Type == model.Type));
        Assert.Contains(models, model => model.UnionCases.All(unionCase => unionCase.TypeModel.Type != model.Type));
        ITypeShapeProvider provider = LoadContextualTestProvider(result);

        foreach (FSharpUnionShapeModel model in models)
        {
            Assert.True(model.UnderlyingModel.IsContextual);
            AssertContextualInitializer(result, model.UnderlyingModel);
            Assert.All(model.UnionCases, unionCase =>
            {
                Assert.True(unionCase.TypeModel.IsContextual);
                AssertContextualInitializer(result, unionCase.TypeModel);
            });

            ITypeShape? detachedBase = null;
            ITypeShape[]? detachedCases = null;
            if (accessContextualFirst)
            {
                detachedBase = GetGeneratedShapeProperty(provider, model.UnderlyingModel);
                detachedCases = model.UnionCases.Select(unionCase => GetGeneratedShapeProperty(provider, unionCase.TypeModel)).ToArray();
                Assert.True(detachedBase.IsContextual);
                Assert.All(detachedCases, shape => Assert.True(shape.IsContextual));
            }

            IUnionTypeShape union = Assert.IsAssignableFrom<IUnionTypeShape>(GetGeneratedShapeProperty(provider, model));
            Assert.False(union.IsContextual);
            Assert.Same(union, provider.GetTypeShape(union.Type));
            IObjectTypeShape baseShape = Assert.IsAssignableFrom<IObjectTypeShape>(union.BaseType);
            Assert.True(baseShape.IsContextual);
            Assert.Empty(baseShape.Properties);
            Assert.Null(baseShape.Constructor);
            detachedBase ??= GetGeneratedShapeProperty(provider, model.UnderlyingModel);
            Assert.Same(detachedBase, baseShape);
            Assert.Equal(union.Type, baseShape.Type);

            Assert.All(union.UnionCases, unionCase => Assert.True(unionCase.UnionCaseType.IsContextual));
            detachedCases ??= model.UnionCases.Select(unionCase => GetGeneratedShapeProperty(provider, unionCase.TypeModel)).ToArray();
            foreach (FSharpUnionCaseShapeModel caseModel in model.UnionCases)
            {
                ITypeShape caseShape = union.UnionCases[caseModel.Tag].UnionCaseType;
                Assert.Same(detachedCases[caseModel.Tag], caseShape);
                Assert.True(caseShape.IsContextual);
                Assert.Equal(caseModel.TypeModel.Type == model.Type, caseShape.Type == union.Type);
                if (caseShape.Type == union.Type)
                {
                    Assert.NotSame(union, caseShape);
                    Assert.Same(union, provider.GetTypeShape(caseShape.Type));
                }
            }

            Assert.True(detachedBase.IsContextual);
            Assert.False(union.IsContextual);
        }
    }

    [Theory]
    [InlineData(IsContextualCSharpUnionSource)]
    [InlineData("""
        using PolyType;
        [System.Runtime.CompilerServices.Union]
        public class RecursiveUnion
        {
            public RecursiveUnion(int value) => Value = value;
            public RecursiveUnion(RecursiveUnion value) => Value = value;
            public RecursiveUnion(string? value) => Value = value;
            public object? Value { get; }
        }
        [GenerateShapeFor(typeof(RecursiveUnion))]
        public partial class Witness { }
        """)]
    [InlineData("""
        using PolyType;
        [System.Runtime.CompilerServices.Union]
        public struct RecursiveUnion
        {
            public RecursiveUnion(int value) => Value = value;
            public RecursiveUnion(RecursiveUnion value) => Value = value;
            public RecursiveUnion(string? value) => Value = value;
            public object? Value { get; }
        }
        [GenerateShapeFor(typeof(RecursiveUnion))]
        public partial class Witness { }
        """)]
    public static void IsContextual_CSharpUnionPayloadsAreNotContextual(string source)
    {
        PolyTypeSourceGeneratorResult result = CompilationHelpers.RunPolyTypeSourceGenerator(
            CreateCSharpUnionCompilation(source, assemblyName: "ContextualCSharpUnion_" + Guid.NewGuid().ToString("N")));
        CSharpUnionShapeModel model = Assert.Single(result.AllGeneratedTypes.OfType<CSharpUnionShapeModel>());
        Assert.True(model.UnderlyingModel.IsContextual);
        AssertContextualInitializer(result, model.UnderlyingModel);
        ITypeShapeProvider provider = LoadContextualTestProvider(result);
        IObjectTypeShape detachedBase = Assert.IsAssignableFrom<IObjectTypeShape>(GetGeneratedShapeProperty(provider, model.UnderlyingModel));
        Assert.True(detachedBase.IsContextual);
        Assert.Empty(detachedBase.Properties);
        Assert.Null(detachedBase.Constructor);

        IUnionTypeShape union = Assert.IsAssignableFrom<IUnionTypeShape>(GetGeneratedShapeProperty(provider, model));
        Assert.False(union.IsContextual);
        Assert.Same(union, provider.GetTypeShape(union.Type));
        Assert.Same(detachedBase, union.BaseType);
        Assert.Equal(union.Type, detachedBase.Type);
        Assert.All(union.UnionCases, unionCase =>
        {
            Assert.False(unionCase.UnionCaseType.IsContextual);
            Assert.Same(unionCase.UnionCaseType, provider.GetTypeShape(unionCase.UnionCaseType.Type));
        });
        IUnionCaseShape selfCase = Assert.Single(union.UnionCases, unionCase => unionCase.UnionCaseType.Type == union.Type);
        Assert.Same(union, selfCase.UnionCaseType);
        Assert.True(detachedBase.IsContextual);
    }

    private static ITypeShapeProvider LoadContextualTestProvider(PolyTypeSourceGeneratorResult result)
    {
        Assert.All(result.AllGeneratedTypes, model =>
        {
            Assert.False(model.IsContextual);
            AssertContextualInitializer(result, model);
        });

        using var stream = new MemoryStream();
        var emit = result.NewCompilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        Assembly assembly = Assembly.Load(stream.ToArray());
        Type providerType = assembly.GetType(Assert.Single(result.GeneratedModels).ProviderDeclaration.Id.FullyQualifiedName.Replace("global::", ""))!;
        var provider = Assert.IsAssignableFrom<SourceGenTypeShapeProvider>(providerType.GetProperty("Default")!.GetValue(null));
        Assert.Equal(PolyTypeGenerator.SourceGeneratorVersion, provider.SourceGeneratorVersion);
        return provider;
    }

    private static ITypeShape GetGeneratedShapeProperty(ITypeShapeProvider provider, TypeShapeModel model) =>
        Assert.IsAssignableFrom<ITypeShape>(provider.GetType().GetProperty(model.SourceIdentifier)!.GetValue(provider));

    private static void AssertContextualInitializer(PolyTypeSourceGeneratorResult result, TypeShapeModel model)
    {
        string prefix = Assert.Single(result.GeneratedModels).ProviderDeclaration.SourceFilenamePrefix;
        SyntaxTree tree = Assert.Single(result.NewCompilation.SyntaxTrees,
            tree => tree.FilePath.EndsWith(prefix + "." + model.SourceIdentifier + ".g.cs", StringComparison.Ordinal));
        MethodDeclarationSyntax factory = Assert.Single(tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes()
            .OfType<MethodDeclarationSyntax>(), method => method.Identifier.ValueText == "__Create_" + model.SourceIdentifier);
        ReturnStatementSyntax statement = Assert.Single(factory.Body!.Statements.OfType<ReturnStatementSyntax>());
        ObjectCreationExpressionSyntax creation = Assert.IsType<ObjectCreationExpressionSyntax>(statement.Expression);
        InitializerExpressionSyntax initializer = Assert.IsType<InitializerExpressionSyntax>(creation.Initializer);
        Assert.True(initializer.IsKind(SyntaxKind.ObjectInitializerExpression));
        AssignmentExpressionSyntax[] assignments = initializer.Expressions.Cast<AssignmentExpressionSyntax>().ToArray();
        Assert.DoesNotContain(assignments, assignment =>
            assignment.Right.IsKind(SyntaxKind.NullLiteralExpression) ||
            assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression) ||
            assignment.Right.IsKind(SyntaxKind.DefaultLiteralExpression));
        AssignmentExpressionSyntax[] contextualAssignments = assignments
            .Where(assignment => assignment.Left is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == nameof(ITypeShape.IsContextual))
            .ToArray();

        if (model.IsContextual)
        {
            Assert.True(Assert.Single(contextualAssignments).Right.IsKind(SyntaxKind.TrueLiteralExpression));
        }
        else
        {
            Assert.Empty(contextualAssignments);
        }
    }
}
