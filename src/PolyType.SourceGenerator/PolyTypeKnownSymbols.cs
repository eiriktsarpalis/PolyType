using Microsoft.CodeAnalysis;
using PolyType.Roslyn;

namespace PolyType.SourceGenerator;

public sealed class PolyTypeKnownSymbols(Compilation compilation) : KnownSymbols(compilation)
{
    /// <summary>
    /// Names of members on the TypeShapeExtensionAttribute.
    /// </summary>
    public static class TypeShapeExtensionAttributePropertyNames
    {
        public const string AssociatedShapeDepth = "AssociatedShapeDepth";
        public const string AssociatedTypes = "AssociatedTypes";
    }

    /// <summary>
    /// Names of members on the AssociatedTypeShapeAttribute.
    /// </summary>
    public static class AssociatedTypeShapeAttributePropertyNames
    {
        public const string Requirements = "Requirements";
    }

    public INamedTypeSymbol? GenerateShapeAttribute => GetOrResolveType("PolyType.GenerateShapeAttribute", ref _GenerateShapeAttribute);
    private Option<INamedTypeSymbol?> _GenerateShapeAttribute;

    public INamedTypeSymbol? GenerateShapeAttributeOfT => GetOrResolveType("PolyType.GenerateShapeAttribute`1", ref _GenerateShapeAttributeOfT);
    private Option<INamedTypeSymbol?> _GenerateShapeAttributeOfT;

    public INamedTypeSymbol? TypeShapeAttribute => GetOrResolveType("PolyType.TypeShapeAttribute", ref _TypeShapeAttribute);
    private Option<INamedTypeSymbol?> _TypeShapeAttribute;

    public INamedTypeSymbol? TypeShapeExtensionAttribute => GetOrResolveType("PolyType.TypeShapeExtensionAttribute", ref _TypeShapeExtensionAttribute);
    private Option<INamedTypeSymbol?> _TypeShapeExtensionAttribute;

    public INamedTypeSymbol? AssociatedTypeShapeAttribute => GetOrResolveType("PolyType.AssociatedTypeShapeAttribute", ref _AssociatedTypeShapeAttribute);
    private Option<INamedTypeSymbol?> _AssociatedTypeShapeAttribute;

    public INamedTypeSymbol? AssociatedTypeAttributeAttribute => GetOrResolveType("PolyType.Abstractions.AssociatedTypeAttributeAttribute", ref _AssociatedTypeAttributeAttribute);
    private Option<INamedTypeSymbol?> _AssociatedTypeAttributeAttribute;

    public INamedTypeSymbol? PropertyShapeAttribute => GetOrResolveType("PolyType.PropertyShapeAttribute", ref _PropertyShapeAttribute);
    private Option<INamedTypeSymbol?> _PropertyShapeAttribute;

    public INamedTypeSymbol? ConstructorShapeAttribute => GetOrResolveType("PolyType.ConstructorShapeAttribute", ref _ConstructorShapeAttribute);
    private Option<INamedTypeSymbol?> _ConstructorShapeAttribute;

    public INamedTypeSymbol? ParameterShapeAttribute => GetOrResolveType("PolyType.ParameterShapeAttribute", ref _ParameterShapeAttribute);
    private Option<INamedTypeSymbol?> _ParameterShapeAttribute;

    public INamedTypeSymbol? DerivedTypeShapeAttribute => GetOrResolveType("PolyType.DerivedTypeShapeAttribute", ref _DerivedTypeShapeAttribute);
    private Option<INamedTypeSymbol?> _DerivedTypeShapeAttribute;

    public INamedTypeSymbol? MarshallerType => GetOrResolveType("PolyType.IMarshaller`2", ref _Marshaller);
    private Option<INamedTypeSymbol?> _Marshaller;

    public INamedTypeSymbol? FSharpCompilationMappingAttribute => GetOrResolveType("Microsoft.FSharp.Core.CompilationMappingAttribute", ref _FSharpCompilationMappingAttribute);
    private Option<INamedTypeSymbol?> _FSharpCompilationMappingAttribute;

    public INamedTypeSymbol? FSharpOption => GetOrResolveType("Microsoft.FSharp.Core.FSharpOption`1", ref _FSharpOptionType);
    private Option<INamedTypeSymbol?> _FSharpOptionType;

    public INamedTypeSymbol? FSharpValueOption => GetOrResolveType("Microsoft.FSharp.Core.FSharpValueOption`1", ref _FSharpValueOptionType);
    private Option<INamedTypeSymbol?> _FSharpValueOptionType;

    public TargetFramework TargetFramework => _targetFramework ??= ResolveTargetFramework();
    private TargetFramework? _targetFramework;

    private TargetFramework ResolveTargetFramework()
    {
        INamedTypeSymbol? alternateEqualityComparer = Compilation.GetTypeByMetadataName("System.Collections.Generic.IAlternateEqualityComparer`2");
        if (alternateEqualityComparer is not null &&
            SymbolEqualityComparer.Default.Equals(alternateEqualityComparer.ContainingAssembly, CoreLibAssembly))
        {
            return TargetFramework.Net90;
        }

        INamedTypeSymbol? searchValues = Compilation.GetTypeByMetadataName("System.Buffers.SearchValues");
        if (searchValues is not null &&
            SymbolEqualityComparer.Default.Equals(searchValues.ContainingAssembly, CoreLibAssembly))
        {
            return TargetFramework.Net80;
        }

        return TargetFramework.Legacy;
    }
}