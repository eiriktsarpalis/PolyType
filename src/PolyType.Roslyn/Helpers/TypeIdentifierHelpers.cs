using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Text;

namespace PolyType.Roslyn.Helpers;

internal static class TypeIdentifierHelpers
{
    /// <summary>
    /// Returns a string representation of the type suitable for use as an identifier in source code or file names.
    /// </summary>
    public static string CreateTypeIdentifier(this ITypeSymbol type, ReadOnlySpan<string> reservedIdentifiers, bool includeNamespaces = false)
    {
        StringBuilder sb = new();
        GenerateCore(type, sb);
        string identifier = sb.ToString();

        // Do not return identifiers that are C# keywords or reserved identifiers.
        return IsCSharpKeyword(identifier) || reservedIdentifiers.IndexOf(identifier) >= 0
            ? "__Type_" + identifier
            : identifier;

        void GenerateCore(ITypeSymbol type, StringBuilder sb)
        {
            switch (type)
            {
                case ITypeParameterSymbol typeParameter:
                    sb.Append(typeParameter.Name);
                    break;

                case IArrayTypeSymbol arrayType:
                    // Use prefix notation to avoid ambiguity: Array_Int32 instead of Int32_Array
                    // This prevents collisions like Dictionary<string, int[]> vs Dictionary<string, int>[]
                    sb.Append("Array");
                    if (arrayType.Rank > 1)
                    {
                        // Array2D, Array3D, etc.
                        sb.Append(arrayType.Rank);
                        sb.Append('D');
                    }
                    sb.Append('_');
                    GenerateCore(arrayType.ElementType, sb);
                    break;

                case INamedTypeSymbol namedType:
                    if (includeNamespaces)
                    {
                        PrependNamespaces(namedType.ContainingNamespace);
                        PrependContainingTypes(namedType);
                    }

                    sb.Append(namedType.Name);

                    IEnumerable<ITypeSymbol> typeArguments = namedType.IsTupleType
                        ? namedType.TupleElements.Select(e => e.Type)
                        : namedType.TypeArguments;

                    foreach (ITypeSymbol argument in typeArguments)
                    {
                        sb.Append('_');
                        GenerateCore(argument, sb);
                    }

                    break;

                default:
                    Debug.Fail($"Type {type} not supported");
                    throw new InvalidOperationException();
            }

            void PrependNamespaces(INamespaceSymbol ns)
            {
                if (ns.ContainingNamespace is { } containingNs)
                {
                    PrependNamespaces(containingNs);
                    sb.Append(ns.Name);
                    sb.Append('_');
                }
            }

            void PrependContainingTypes(INamedTypeSymbol namedType)
            {
                if (namedType.ContainingType is { } parent)
                {
                    PrependContainingTypes(parent);
                    GenerateCore(parent, sb);
                    sb.Append('_');
                }
            }
        }
    }

    public static bool IsCSharpKeyword(string name) =>
        SyntaxFacts.GetKeywordKind(name) is not SyntaxKind.None ||
        SyntaxFacts.GetContextualKeywordKind(name) is not SyntaxKind.None;

    public static string EscapeKeywordIdentifier(string name) =>
        IsCSharpKeyword(name) ? "@" + name : name;
}
