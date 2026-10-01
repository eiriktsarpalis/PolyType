using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyType.Roslyn.Helpers;

internal static class UnionHelpers
{
    public static bool IsUnionDeclaration(this BaseTypeDeclarationSyntax declaration)
    {
        // Union syntax is supplied by the compiler host, not the shipping Roslyn reference.
        return declaration.ChildTokens().Any(token => token != declaration.Identifier && token.ValueText == "union");
    }

    public static bool IsCSharpUnion(this ITypeSymbol type, CancellationToken cancellationToken = default)
    {
        return type.TypeKind is TypeKind.Class or TypeKind.Struct &&
            (type.GetAttributes().Any(attribute =>
                attribute.AttributeClass is { Name: "UnionAttribute" } attributeType &&
                attributeType.ContainingNamespace.MatchesNamespace(["System", "Runtime", "CompilerServices"])) ||
             type.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax(cancellationToken) is BaseTypeDeclarationSyntax declaration && declaration.IsUnionDeclaration()));
    }
}
