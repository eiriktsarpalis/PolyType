using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Debugging;

/// <summary>
/// Base debugger proxy view for any <see cref="ITypeShape"/> implementation.
/// </summary>
[ExcludeFromCodeCoverage]
internal abstract class TypeShapeDebugView(ITypeShape typeShape)
{
    public Type Type => typeShape.Type;
    public TypeShapeKind Kind => typeShape.Kind;
    public ITypeShapeProvider Provider => typeShape.Provider;
    public ICustomAttributeProvider? AttributeProvider => typeShape.AttributeProvider;
    public IReadOnlyList<IMethodShape> Methods => typeShape.Methods;
    public IReadOnlyList<IEventShape> Events => typeShape.Events;
}

/// <summary>
/// Debugger proxy for <see cref="IObjectTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ObjectTypeShapeDebugView(IObjectTypeShape typeShape) : TypeShapeDebugView(typeShape)
{
    public bool IsRecordType => typeShape.IsRecordType;
    public bool IsTupleType => typeShape.IsTupleType;
    public IConstructorShape? Constructor => typeShape.Constructor;
    public IPropertyShape[] Properties => typeShape.Properties.ToArray();
}

/// <summary>
/// Debugger proxy for <see cref="IEnumerableTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EnumerableTypeShapeDebugView(IEnumerableTypeShape typeShape) : TypeShapeDebugView(typeShape)
{
    public ITypeShape ElementType => typeShape.ElementType;
    public bool IsSetType => typeShape.IsSetType;
}

/// <summary>
/// Debugger proxy for <see cref="IDictionaryTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class DictionaryTypeShapeDebugView(IDictionaryTypeShape typeShape) : TypeShapeDebugView(typeShape)
{
    public ITypeShape KeyType => typeShape.KeyType;
    public ITypeShape ValueType => typeShape.ValueType;
    public CollectionConstructionStrategy ConstructionStrategy => typeShape.ConstructionStrategy;
}

/// <summary>
/// Debugger proxy for <see cref="IEnumTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EnumTypeShapeDebugView(IEnumTypeShape typeShape) : TypeShapeDebugView(typeShape)
{
    public ITypeShape UnderlyingType => typeShape.UnderlyingType;
}

/// <summary>
/// Debugger proxy for <see cref="IOptionalTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class OptionalTypeShapeDebugView(IOptionalTypeShape typeShape) : TypeShapeDebugView(typeShape)
{
    public ITypeShape ElementType => typeShape.ElementType;
}

/// <summary>
/// Debugger proxy for <see cref="ISurrogateTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class SurrogateTypeShapeDebugView(ISurrogateTypeShape typeShape) : TypeShapeDebugView(typeShape)
{
    public ITypeShape SurrogateType => typeShape.SurrogateType;
}

/// <summary>
/// Debugger proxy for <see cref="IUnionTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class UnionTypeShapeDebugView(IUnionTypeShape typeShape) : TypeShapeDebugView(typeShape)
{
    public ITypeShape BaseType => typeShape.BaseType;
    public IUnionCaseShape[] UnionCases => typeShape.UnionCases.ToArray();
}

/// <summary>
/// Debugger proxy for <see cref="IFunctionTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class FunctionTypeShapeDebugView(IFunctionTypeShape typeShape) : TypeShapeDebugView(typeShape)
{
    public ITypeShape ReturnType => typeShape.ReturnType;
    public bool IsVoidLike => typeShape.IsVoidLike;
    public bool IsAsync => typeShape.IsAsync;
    public IParameterShape[] Parameters => typeShape.Parameters.ToArray();
}

/// <summary>
/// Debugger proxy for <see cref="IConstructorShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ConstructorShapeDebugView(IConstructorShape ctorShape)
{
    public IObjectTypeShape DeclaringType => ctorShape.DeclaringType;
    public bool IsPublic => ctorShape.IsPublic;
    public IParameterShape[] Parameters => ctorShape.Parameters.ToArray();
}

/// <summary>
/// Debugger proxy for <see cref="IMethodShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class MethodShapeDebugView(IMethodShape methodShape)
{
    public string Name => methodShape.Name;
    public ITypeShape DeclaringType => methodShape.DeclaringType;
    public ITypeShape ReturnType => methodShape.ReturnType;
    public bool IsPublic => methodShape.IsPublic;
    public bool IsStatic => methodShape.IsStatic;
    public bool IsVoidLike => methodShape.IsVoidLike;
    public bool IsAsync => methodShape.IsAsync;
    public IParameterShape[] Parameters => methodShape.Parameters.ToArray();
}

/// <summary>
/// Debugger proxy for <see cref="IParameterShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ParameterShapeDebugView(IParameterShape parameterShape)
{
    public int Position => parameterShape.Position;
    public string Name => parameterShape.Name;
    public ParameterKind Kind => parameterShape.Kind;
    public bool HasDefaultValue => parameterShape.HasDefaultValue;
    public object? DefaultValue => parameterShape.DefaultValue;
    public bool IsRequired => parameterShape.IsRequired;
    public bool IsNonNullable => parameterShape.IsNonNullable;
    public bool IsPublic => parameterShape.IsPublic;
    public ITypeShape ParameterType => parameterShape.ParameterType;
}

/// <summary>
/// Debugger proxy for <see cref="IPropertyShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class PropertyShapeDebugView(IPropertyShape propertyShape)
{
    public string Name => propertyShape.Name;
    public int Position => propertyShape.Position;
    public bool HasGetter => propertyShape.HasGetter;
    public bool HasSetter => propertyShape.HasSetter;
    public bool IsField => propertyShape.IsField;
    public bool IsGetterPublic => propertyShape.IsGetterPublic;
    public bool IsSetterPublic => propertyShape.IsSetterPublic;
    public bool IsGetterNonNullable => propertyShape.IsGetterNonNullable;
    public bool IsSetterNonNullable => propertyShape.IsSetterNonNullable;
    public ITypeShape PropertyType => propertyShape.PropertyType;
}

/// <summary>
/// Debugger proxy for <see cref="IEventShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EventShapeDebugView(IEventShape eventShape)
{
    public string Name => eventShape.Name;
    public bool IsStatic => eventShape.IsStatic;
    public ITypeShape DeclaringType => eventShape.DeclaringType;
    public IFunctionTypeShape HandlerType => eventShape.HandlerType;
}

/// <summary>
/// Debugger proxy for <see cref="IUnionCaseShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class UnionCaseShapeDebugView(IUnionCaseShape unionCaseShape)
{
    public string Name => unionCaseShape.Name;
    public int Tag => unionCaseShape.Tag;
    public bool IsTagSpecified => unionCaseShape.IsTagSpecified;
    public int Index => unionCaseShape.Index;
    public ITypeShape UnionCaseType => unionCaseShape.UnionCaseType;
}
