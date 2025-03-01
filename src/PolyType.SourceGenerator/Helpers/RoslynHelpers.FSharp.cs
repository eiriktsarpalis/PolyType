using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.SourceGenerator.Helpers;

public abstract record FSharpUnionInfo;
public sealed record FSharpOptionInfo(ITypeSymbol Type, ITypeSymbol ElementType, bool IsValueOption) : FSharpUnionInfo;
public sealed record GenericFSharpUnionInfo(ITypeSymbol Type, IMethodSymbol TagReader, bool IsOptional, FSharpUnionCaseInfo[] UnionCases) : FSharpUnionInfo;
public sealed record FSharpUnionCaseInfo(int Tag, string Name, ITypeSymbol DeclaringType, IPropertySymbol[] Properties, IMethodSymbol Constructor);

internal static partial class RoslynHelpers
{
    public static FSharpUnionInfo? ResolveFSharpUnionMetadata(this PolyTypeKnownSymbols knownSymbols, ITypeSymbol type)
    {
        if (!knownSymbols.IsFSharpUnion(type))
        {
            return null;
        }

        var namedType = (INamedTypeSymbol)type;
        if (namedType.IsGenericType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, knownSymbols.FSharpList))
            {
                return null; // F# lists are viewed as collections and not as unions.
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, knownSymbols.FSharpOption) ||
                SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, knownSymbols.FSharpValueOption))
            {
                return new FSharpOptionInfo(namedType, namedType.TypeArguments[0], namedType.IsValueType);
            }
        }

        IMethodSymbol tagReader = type.GetMembers<IMethodSymbol>("get_Tag").FirstOrDefault() ?? type.GetMembers<IMethodSymbol>("GetTag").First();
        List<FSharpUnionCaseInfo> unionCases = new();
        foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>().Where(m => m.IsStatic))
        {
            if (knownSymbols.TryParseFSharpCompilationMappingAttribute(method, out FSharpSourceConstructFlags flags, out _, out int sequenceNumber) &&
                flags is FSharpSourceConstructFlags.UnionCase)
            {
                string name = method switch
                {
                    { MethodKind: MethodKind.PropertyGet } => method.AssociatedSymbol!.Name,
                    _ when method.Name.StartsWith("New", StringComparison.Ordinal) => method.Name[3..],
                    _ => method.Name
                };

                unionCases.Add(BuildCaseInfo(tag: sequenceNumber, name, method));
            }
        }

        return new GenericFSharpUnionInfo(type, tagReader, IsOptional: false, unionCases.OrderBy(c => c.Tag).ToArray());

        FSharpUnionCaseInfo BuildCaseInfo(int tag, string name, IMethodSymbol constructor)
        {
            ITypeSymbol caseType = type.TypeKind is TypeKind.Class 
                ? type.GetTypeMembers().FirstOrDefault(x => x.Name == name) ?? type
                : type;

            List<(IPropertySymbol Property, int SequenceNumber)> properties = new();
            foreach (IPropertySymbol property in caseType.GetMembers().OfType<IPropertySymbol>())
            {
                if (knownSymbols.TryParseFSharpCompilationMappingAttribute(property, out FSharpSourceConstructFlags flags, out int variantNumber, out int sequenceNumber) &&
                    flags is FSharpSourceConstructFlags.Field && variantNumber == tag)
                {
                    properties.Add((property, sequenceNumber));
                }
            }

            IPropertySymbol[] propertySymbols = properties.OrderBy(x => x.SequenceNumber).Select(x => x.Property).ToArray();
            return new FSharpUnionCaseInfo(tag, name, caseType, propertySymbols, constructor);
        }
    }

    public static bool IsFSharpUnion(this PolyTypeKnownSymbols knownSymbols, ITypeSymbol type) =>
        type is { TypeKind: TypeKind.Class or TypeKind.Struct } &&
        knownSymbols.TryParseFSharpCompilationMappingAttribute(type, out FSharpSourceConstructFlags flags, out _, out _) &&
        flags is FSharpSourceConstructFlags.SumType;

    public static bool TryParseFSharpCompilationMappingAttribute(this PolyTypeKnownSymbols knownSymbols,
        ISymbol symbol, 
        out FSharpSourceConstructFlags flags,
        out int variantNumber,
        out int sequenceNumber)
    {
        // https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-compilationmappingattribute.html
        flags = FSharpSourceConstructFlags.None;
        variantNumber = 0;
        sequenceNumber = 0;

        AttributeData? compilationMappingAttr = symbol.GetAttribute(knownSymbols.FSharpCompilationMappingAttribute, inherit: false);
        if (compilationMappingAttr is null)
        {
            return false;
        }

        switch (compilationMappingAttr.ConstructorArguments)
        {
            case [{ Value: int intFlags }]:
                flags = (FSharpSourceConstructFlags)intFlags;
                break;

            case [{ Value: int intFlags }, { Value: int intSequenceNumber }]:
                flags = (FSharpSourceConstructFlags)intFlags;
                sequenceNumber = intSequenceNumber;
                break;

            case [{ Value: int intFlags }, { Value: int intVariantNumber }, { Value: int intSequenceNumber }]:
                flags = (FSharpSourceConstructFlags)intFlags;
                variantNumber = intVariantNumber;
                sequenceNumber = intSequenceNumber;
                break;
        }

        return true;
    }
}
