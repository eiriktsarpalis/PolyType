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
        public const string AssociatedTypes = "AssociatedTypes";
        public const string AssociatedTypeRequirements = "AssociatedTypeRequirements";
        public const string Marshaler = "Marshaler";
    }

    /// <summary>
    /// Names of members on the AssociatedTypeShapeAttribute.
    /// </summary>
    public static class AssociatedTypeShapeAttributePropertyNames
    {
        public const string Requirements = "Requirements";
    }

    public static class EnumMemberShapeAttributePropertyNames
    {
        public const string Name = "Name";
    }

    public INamedTypeSymbol? GenerateShapeAttribute => GetOrResolveType("PolyType.GenerateShapeAttribute", ref _GenerateShapeAttribute);
    private Option<INamedTypeSymbol?> _GenerateShapeAttribute;

    public INamedTypeSymbol? GenerateShapeForAttribute => GetOrResolveType("PolyType.GenerateShapeForAttribute", ref _GenerateShapeForAttribute);
    private Option<INamedTypeSymbol?> _GenerateShapeForAttribute;

    public INamedTypeSymbol? GenerateShapeForAttributeOfT => GetOrResolveType("PolyType.GenerateShapeForAttribute`1", ref _GenerateShapeForAttributeOfT);
    private Option<INamedTypeSymbol?> _GenerateShapeForAttributeOfT;

    public INamedTypeSymbol? TypeShapeAttribute => GetOrResolveType("PolyType.TypeShapeAttribute", ref _TypeShapeAttribute);
    private Option<INamedTypeSymbol?> _TypeShapeAttribute;

    public INamedTypeSymbol? UnitType => GetOrResolveType("PolyType.Abstractions.Unit", ref _UnitType);
    private Option<INamedTypeSymbol?> _UnitType;

    public INamedTypeSymbol? TypeShapeExtensionAttribute => GetOrResolveType("PolyType.TypeShapeExtensionAttribute", ref _TypeShapeExtensionAttribute);
    private Option<INamedTypeSymbol?> _TypeShapeExtensionAttribute;

    public INamedTypeSymbol? AssociatedTypeShapeAttribute => GetOrResolveType("PolyType.AssociatedTypeShapeAttribute", ref _AssociatedTypeShapeAttribute);
    private Option<INamedTypeSymbol?> _AssociatedTypeShapeAttribute;

    public INamedTypeSymbol? AssociatedTypeAttributeAttribute => GetOrResolveType("PolyType.Abstractions.AssociatedTypeAttributeAttribute", ref _AssociatedTypeAttributeAttribute);
    private Option<INamedTypeSymbol?> _AssociatedTypeAttributeAttribute;

    public INamedTypeSymbol? PropertyShapeAttribute => GetOrResolveType("PolyType.PropertyShapeAttribute", ref _PropertyShapeAttribute);
    private Option<INamedTypeSymbol?> _PropertyShapeAttribute;

    public INamedTypeSymbol? EnumMemberShapeAttribute => GetOrResolveType("PolyType.EnumMemberShapeAttribute", ref _EnumMemberShapeAttribute);
    private Option<INamedTypeSymbol?> _EnumMemberShapeAttribute;

    public INamedTypeSymbol? ConstructorShapeAttribute => GetOrResolveType("PolyType.ConstructorShapeAttribute", ref _ConstructorShapeAttribute);
    private Option<INamedTypeSymbol?> _ConstructorShapeAttribute;

    public INamedTypeSymbol? MethodShapeAttribute => GetOrResolveType("PolyType.MethodShapeAttribute", ref _MethodShapeAttribute);
    private Option<INamedTypeSymbol?> _MethodShapeAttribute;

    public INamedTypeSymbol? EventShapeAttribute => GetOrResolveType("PolyType.EventShapeAttribute", ref _EventShapeAttribute);
    private Option<INamedTypeSymbol?> _EventShapeAttribute;

    public INamedTypeSymbol? ParameterShapeAttribute => GetOrResolveType("PolyType.ParameterShapeAttribute", ref _ParameterShapeAttribute);
    private Option<INamedTypeSymbol?> _ParameterShapeAttribute;

    public INamedTypeSymbol? DerivedTypeShapeAttribute => GetOrResolveType("PolyType.DerivedTypeShapeAttribute", ref _DerivedTypeShapeAttribute);
    private Option<INamedTypeSymbol?> _DerivedTypeShapeAttribute;

    public INamedTypeSymbol? MarshalerType => GetOrResolveType("PolyType.IMarshaler`2", ref _Marshaler);
    private Option<INamedTypeSymbol?> _Marshaler;

    public INamedTypeSymbol? FSharpCompilationMappingAttribute => GetOrResolveType("Microsoft.FSharp.Core.CompilationMappingAttribute", ref _FSharpCompilationMappingAttribute);
    private Option<INamedTypeSymbol?> _FSharpCompilationMappingAttribute;

    public INamedTypeSymbol? FSharpOption => GetOrResolveType("Microsoft.FSharp.Core.FSharpOption`1", ref _FSharpOptionType);
    private Option<INamedTypeSymbol?> _FSharpOptionType;

    public INamedTypeSymbol? FSharpValueOption => GetOrResolveType("Microsoft.FSharp.Core.FSharpValueOption`1", ref _FSharpValueOptionType);
    private Option<INamedTypeSymbol?> _FSharpValueOptionType;

    public INamedTypeSymbol? DataContractAttribute => GetOrResolveType("System.Runtime.Serialization.DataContractAttribute", ref _DataContractAttribute);
    private Option<INamedTypeSymbol?> _DataContractAttribute;

    public INamedTypeSymbol? DataMemberAttribute => GetOrResolveType("System.Runtime.Serialization.DataMemberAttribute", ref _DataMemberAttribute);
    private Option<INamedTypeSymbol?> _DataMemberAttribute;

    public INamedTypeSymbol? EnumMemberAttribute => GetOrResolveType("System.Runtime.Serialization.EnumMemberAttribute", ref _EnumMemberAttribute);
    private Option<INamedTypeSymbol?> _EnumMemberAttribute;

    public INamedTypeSymbol? IgnoreDataMemberAttribute => GetOrResolveType("System.Runtime.Serialization.IgnoreDataMemberAttribute", ref _IgnoreDataMemberAttribute);
    private Option<INamedTypeSymbol?> _IgnoreDataMemberAttribute;

    public INamedTypeSymbol? KnownTypeAttribute => GetOrResolveType("System.Runtime.Serialization.KnownTypeAttribute", ref _KnownTypeAttribute);
    private Option<INamedTypeSymbol?> _KnownTypeAttribute;
}