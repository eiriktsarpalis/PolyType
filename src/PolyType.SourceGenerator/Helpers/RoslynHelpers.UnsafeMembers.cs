using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn.Helpers;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace PolyType.SourceGenerator.Helpers;

internal static partial class RoslynHelpers
{
    private static readonly Func<IModuleSymbol, int>? s_memorySafetyRulesVersionAccessor = CreateMemorySafetyRulesVersionAccessor();

    public static bool UsesUpdatedMemorySafetyRules(this Compilation compilation) =>
        GetUpdatedMemorySafetyRulesFromApi(compilation.SourceModule) ??
        (compilation.SyntaxTrees.FirstOrDefault()?.Options.Features.ContainsKey("updated-memory-safety-rules") is true);

    private static bool? GetUpdatedMemorySafetyRulesFromApi(IModuleSymbol module)
    {
        const int UpdatedMemorySafetyRulesVersion = 2;
        return s_memorySafetyRulesVersionAccessor is { } getVersion
            ? getVersion(module) >= UpdatedMemorySafetyRulesVersion
            : null;
    }

    private static Func<IModuleSymbol, int>? CreateMemorySafetyRulesVersionAccessor()
    {

        // Light up the enum-valued API without raising the minimum supported Roslyn version.
        MethodInfo? getter = typeof(IModuleSymbol).GetProperty("MemorySafetyRulesVersion")?.GetMethod;
        return getter is null
            ? null
            : (Func<IModuleSymbol, int>)getter.CreateDelegate(typeof(Func<IModuleSymbol, int>));
    }

    public static bool HasUnsafeMembers(this ITypeSymbol type, Compilation compilation)
    {
        IAssemblySymbol coreLibrary = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly;
        foreach (ITypeSymbol current in type.GetSortedTypeHierarchy())
        {
            if (current is not INamedTypeSymbol ||
                SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, coreLibrary))
            {
                continue;
            }

            ITypeSymbol definition = current.OriginalDefinition;
            bool? usesUpdatedRules = GetUpdatedMemorySafetyRulesFromApi(definition.ContainingModule);
            if (definition.DeclaringSyntaxReferences.IsEmpty &&
                definition.ContainingModule.GetMetadata() is { } metadata &&
                definition.MetadataToken != 0)
            {
                MetadataReader reader = metadata.GetMetadataReader();
                if (!(usesUpdatedRules ??
                    HasMetadataAttribute(reader, reader.GetModuleDefinition().GetCustomAttributes(), "System.Runtime.CompilerServices", "MemorySafetyRulesAttribute")))
                {
                    continue;
                }

                // Read metadata directly: Roslyn hides safety attributes and can omit private members.
                TypeDefinition typeDefinition = reader.GetTypeDefinition((TypeDefinitionHandle)MetadataTokens.EntityHandle(definition.MetadataToken));
                if (typeDefinition.GetMethods().Any(handle => IsUnsafeMember(reader, handle)) ||
                    typeDefinition.GetFields().Any(handle => IsUnsafeMember(reader, handle)) ||
                    typeDefinition.GetProperties().Any(handle => IsUnsafeMember(reader, handle)) ||
                    typeDefinition.GetEvents().Any(handle => IsUnsafeMember(reader, handle)))
                {
                    return true;
                }
            }
            else
            {
                foreach (ISymbol member in definition.GetMembers())
                {
                    if (member.IsImplicitlyDeclared || member is IFieldSymbol { IsFixedSizeBuffer: true })
                    {
                        continue;
                    }

                    if (member.DeclaringSyntaxReferences.Any(reference => IsUnsafeDeclaration(reference, usesUpdatedRules)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;

        static bool IsUnsafeDeclaration(SyntaxReference reference, bool? usesUpdatedRules)
        {
            SyntaxNode declaration = reference.GetSyntax();
            return (usesUpdatedRules ?? declaration.SyntaxTree.Options.Features.ContainsKey("updated-memory-safety-rules")) &&
                HasUnsafeModifier(declaration);
        }

        static bool IsUnsafeMember(MetadataReader reader, EntityHandle member)
        {
            CustomAttributeHandleCollection attributes = reader.GetCustomAttributes(member);
            return HasMetadataAttribute(reader, attributes, "System.Diagnostics.CodeAnalysis", "RequiresUnsafeAttribute") &&
                !(member.Kind is HandleKind.MethodDefinition &&
                    reader.StringComparer.StartsWith(reader.GetMethodDefinition((MethodDefinitionHandle)member).Name, "<") &&
                    HasMetadataAttribute(reader, attributes, "System.Runtime.CompilerServices", "CompilerGeneratedAttribute")) &&
                !(member.Kind is HandleKind.FieldDefinition &&
                    HasMetadataAttribute(reader, attributes, "System.Runtime.CompilerServices", "FixedBufferAttribute"));
        }
    }

    private static bool HasUnsafeModifier(SyntaxNode declaration) => declaration switch
    {
        AccessorDeclarationSyntax accessor => accessor.Modifiers.Any(SyntaxKind.UnsafeKeyword),
        BaseMethodDeclarationSyntax method => method.Modifiers.Any(SyntaxKind.UnsafeKeyword),
        BasePropertyDeclarationSyntax property => property.Modifiers.Any(SyntaxKind.UnsafeKeyword),
        BaseFieldDeclarationSyntax field => field.Modifiers.Any(SyntaxKind.UnsafeKeyword),
        VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax field } => field.Modifiers.Any(SyntaxKind.UnsafeKeyword),
        _ => false,
    };

    private static bool HasMetadataAttribute(MetadataReader reader, CustomAttributeHandleCollection attributes, string @namespace, string name)
    {
        foreach (CustomAttributeHandle handle in attributes)
        {
            EntityHandle constructor = reader.GetCustomAttribute(handle).Constructor;
            EntityHandle declaringType = constructor.Kind switch
            {
                HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
                HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
                _ => default,
            };

            StringHandle typeName;
            StringHandle typeNamespace;
            switch (declaringType.Kind)
            {
                case HandleKind.TypeReference:
                    TypeReference reference = reader.GetTypeReference((TypeReferenceHandle)declaringType);
                    typeName = reference.Name;
                    typeNamespace = reference.Namespace;
                    break;

                case HandleKind.TypeDefinition:
                    TypeDefinition definition = reader.GetTypeDefinition((TypeDefinitionHandle)declaringType);
                    typeName = definition.Name;
                    typeNamespace = definition.Namespace;
                    break;

                default:
                    continue;
            }

            if (reader.StringComparer.Equals(typeName, name) &&
                reader.StringComparer.Equals(typeNamespace, @namespace))
            {
                return true;
            }
        }

        return false;
    }
}
