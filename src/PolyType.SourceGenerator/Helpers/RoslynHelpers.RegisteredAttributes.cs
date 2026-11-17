using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace PolyType.SourceGenerator.Helpers;

internal static partial class RoslynHelpers
{
    public static ImmutableArray<TypeWithAttributeDeclarationContext> IncludeRegisteredAttributeDeclarations(
        ImmutableArray<TypeWithAttributeDeclarationContext> declarations,
        PolyTypeKnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (knownSymbols.GenerateShapeAttributeAttribute is not { } registrationType)
        {
            return declarations;
        }

        Compilation compilation = knownSymbols.Compilation;
        Dictionary<INamedTypeSymbol, AttributeData>? registrations = null;
        AddRegistrations(compilation.Assembly);
        foreach (IAssemblySymbol assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            AddRegistrations(assembly);
        }

        if (registrations is null)
        {
            return declarations;
        }

        HashSet<(string Name, int Arity)> names = [];
        foreach (INamedTypeSymbol type in registrations.Keys)
        {
            AddName(type.Name, type.Arity);
        }

        // Type aliases can hide a registered name, including aliases in another file.
        // Include alias names conservatively; the symbol check below rejects unrelated ones.
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            foreach (UsingDirectiveSyntax directive in GetDeclarations(tree).OfType<UsingDirectiveSyntax>())
            {
                if (directive.Alias is { } alias)
                {
                    AddName(alias.Name.Identifier.ValueText, 0);
                }
            }
        }

        var result = declarations.ToBuilder();
        Dictionary<ITypeSymbol, int> indices = new(SymbolEqualityComparer.Default);
        for (int i = 0; i < declarations.Length; i++)
        {
            indices.Add(declarations[i].TypeSymbol, i);
        }

        HashSet<ITypeSymbol> inspected = new(SymbolEqualityComparer.Default);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            SemanticModel? semanticModel = null;
            foreach (TypeDeclarationSyntax declaration in GetDeclarations(tree).OfType<TypeDeclarationSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!declaration.AttributeLists.SelectMany(list => list.Attributes).Any(IsCandidate))
                {
                    continue;
                }

                semanticModel ??= compilation.GetSemanticModel(tree);
                if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol type ||
                    !inspected.Add(type))
                {
                    continue;
                }

                AttributeData? registration = null;
                foreach (AttributeData attribute in type.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.GenerateShapeAttribute))
                    {
                        registration = null;
                        break;
                    }

                    if (registration is null && attribute.AttributeClass is { } attributeType)
                    {
                        registrations.TryGetValue(attributeType.OriginalDefinition, out registration);
                    }
                }

                if (registration is null)
                {
                    continue;
                }

                TypeWithAttributeDeclarationContext context = new()
                {
                    TypeSymbol = type,
                    Declarations = indices.TryGetValue(type, out int index)
                        ? result[index].Declarations
                        : ImmutableArray.Create<(BaseTypeDeclarationSyntax, SemanticModel)>((declaration, semanticModel)),
                    GenerationAttribute = registration,
                };
                if (indices.ContainsKey(type))
                {
                    result[index] = context;
                }
                else
                {
                    indices.Add(type, result.Count);
                    result.Add(context);
                }
            }
        }

        return result.ToImmutable();

        void AddRegistrations(IAssemblySymbol assembly)
        {
            foreach (AttributeData attribute in assembly.GetAttributes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, registrationType) &&
                    attribute.ConstructorArguments is [{ Value: INamedTypeSymbol type }])
                {
                    // Current-assembly registrations are visited first and override dependencies.
                    registrations ??= new(SymbolEqualityComparer.Default);
                    if (!registrations.ContainsKey(type.OriginalDefinition))
                    {
                        registrations.Add(type.OriginalDefinition, attribute);
                    }
                }
            }
        }

        IEnumerable<SyntaxNode> GetDeclarations(SyntaxTree tree) =>
            tree.GetRoot(cancellationToken).DescendantNodes(static node =>
                node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax);

        void AddName(string name, int arity)
        {
            names.Add((name, arity));
            if (name.EndsWith("Attribute", StringComparison.Ordinal))
            {
                names.Add((name[..^"Attribute".Length], arity));
            }
        }

        bool IsCandidate(AttributeSyntax attribute)
        {
            NameSyntax name = attribute.Name;
            while (true)
            {
                switch (name)
                {
                    case QualifiedNameSyntax qualified:
                        name = qualified.Right;
                        continue;
                    case AliasQualifiedNameSyntax alias:
                        name = alias.Name;
                        continue;
                    case GenericNameSyntax generic:
                        return names.Contains((generic.Identifier.ValueText, generic.Arity));
                    case IdentifierNameSyntax identifier:
                        return names.Contains((identifier.Identifier.ValueText, 0));
                    default:
                        return false;
                }
            }
        }
    }
}
