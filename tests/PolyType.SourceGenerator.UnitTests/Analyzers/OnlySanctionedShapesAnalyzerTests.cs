using PolyType.SourceGenerator.Analyzers;

namespace PolyType.SourceGenerator.UnitTests.Analyzers;

using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = CodeFixVerifier<OnlySanctionedShapesAnalyzer, Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

public class OnlySanctionedShapesAnalyzerTests
{
    [Fact]
    public async Task NoViolations()
    {
        string source = /* lang=c#-test */ """
            class Foo { }
            """;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ImplementationOnClass()
    {
        string source = /* lang=c#-test */ """
            using System;
            using System.Reflection;
            using PolyType;
            using PolyType.Abstractions;

            class Foo : {|PT0019:ITypeShape|}
            {
                public Type Type => throw new NotImplementedException();
                public TypeShapeKind Kind => throw new NotImplementedException();
                public ITypeShapeProvider Provider => throw new NotImplementedException();
                public ICustomAttributeProvider AttributeProvider => throw new NotImplementedException();
                public object Accept(TypeShapeVisitor visitor, object state = null) => throw new NotImplementedException();
                public ITypeShape GetAssociatedTypeShape(Type associatedType) => throw new NotImplementedException();
                public object Invoke(ITypeShapeFunc func, object state = null) => throw new NotImplementedException();
            }
            """;
        await VerifyCS.VerifyAnalyzerAsync(source);
    }
}
