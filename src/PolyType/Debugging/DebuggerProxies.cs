using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.Debugging;

/// <summary>
/// Base debugger proxy view for any <see cref="ITypeShape"/> implementation.
/// </summary>
[ExcludeFromCodeCoverage]
internal abstract class TypeShapeDebugView(ITypeShape typeShape) : ITypeShape
{
    public Type Type => typeShape.Type;
    public TypeShapeKind Kind => typeShape.Kind;
    public ITypeShapeProvider Provider => typeShape.Provider;
    public ICustomAttributeProvider? AttributeProvider => typeShape.AttributeProvider;
    public IReadOnlyList<IMethodShape> Methods => typeShape.Methods;
    public IReadOnlyList<IEventShape> Events => typeShape.Events;

    object? ITypeShape.Accept(TypeShapeVisitor visitor, object? state) => typeShape.Accept(visitor, state);
    ITypeShape? ITypeShape.GetAssociatedTypeShape(Type associatedType) => typeShape.GetAssociatedTypeShape(associatedType);
    object? ITypeShape.Invoke(ITypeShapeFunc func, object? state) => typeShape.Invoke(func, state);
}

/// <summary>
/// Debugger proxy for <see cref="IObjectTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ObjectTypeShapeDebugView(IObjectTypeShape typeShape) : TypeShapeDebugView(typeShape), IObjectTypeShape
{
    public bool IsRecordType => typeShape.IsRecordType;
    public bool IsTupleType => typeShape.IsTupleType;
    public IConstructorShape? Constructor => typeShape.Constructor;
    public IReadOnlyList<IPropertyShape> Properties => typeShape.Properties;
}

/// <summary>
/// Debugger proxy for <see cref="IEnumerableTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EnumerableTypeShapeDebugView(IEnumerableTypeShape typeShape) : TypeShapeDebugView(typeShape), IEnumerableTypeShape
{
    public ITypeShape ElementType => typeShape.ElementType;
    public bool IsSetType => typeShape.IsSetType;
    public int Rank => typeShape.Rank;
    public bool IsAsyncEnumerable => typeShape.IsAsyncEnumerable;
    public CollectionConstructionStrategy ConstructionStrategy => typeShape.ConstructionStrategy;
    public CollectionComparerOptions SupportedComparer => typeShape.SupportedComparer;
}

/// <summary>
/// Debugger proxy for <see cref="IDictionaryTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class DictionaryTypeShapeDebugView(IDictionaryTypeShape typeShape) : TypeShapeDebugView(typeShape), IDictionaryTypeShape
{
    public ITypeShape KeyType => typeShape.KeyType;
    public ITypeShape ValueType => typeShape.ValueType;
    public CollectionConstructionStrategy ConstructionStrategy => typeShape.ConstructionStrategy;
    public CollectionComparerOptions SupportedComparer => typeShape.SupportedComparer;
    public DictionaryInsertionMode AvailableInsertionModes => typeShape.AvailableInsertionModes;
}

/// <summary>
/// Debugger proxy for <see cref="IEnumTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EnumTypeShapeDebugView(IEnumTypeShape typeShape) : TypeShapeDebugView(typeShape), IEnumTypeShape
{
    public ITypeShape UnderlyingType => typeShape.UnderlyingType;
}

/// <summary>
/// Debugger proxy for <see cref="IOptionalTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class OptionalTypeShapeDebugView(IOptionalTypeShape typeShape) : TypeShapeDebugView(typeShape), IOptionalTypeShape
{
    public ITypeShape ElementType => typeShape.ElementType;
}

/// <summary>
/// Debugger proxy for <see cref="ISurrogateTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class SurrogateTypeShapeDebugView(ISurrogateTypeShape typeShape) : TypeShapeDebugView(typeShape), ISurrogateTypeShape
{
    public ITypeShape SurrogateType => typeShape.SurrogateType;
}

/// <summary>
/// Debugger proxy for <see cref="IUnionTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class UnionTypeShapeDebugView(IUnionTypeShape typeShape) : TypeShapeDebugView(typeShape), IUnionTypeShape
{
    public ITypeShape BaseType => typeShape.BaseType;
    public IReadOnlyList<IUnionCaseShape> UnionCases => typeShape.UnionCases;
}

/// <summary>
/// Debugger proxy for <see cref="IFunctionTypeShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class FunctionTypeShapeDebugView(IFunctionTypeShape typeShape) : TypeShapeDebugView(typeShape), IFunctionTypeShape
{
    public ITypeShape ReturnType => typeShape.ReturnType;
    public bool IsVoidLike => typeShape.IsVoidLike;
    public bool IsAsync => typeShape.IsAsync;
    public IReadOnlyList<IParameterShape> Parameters => typeShape.Parameters;
}

/// <summary>
/// Debugger proxy for <see cref="IConstructorShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ConstructorShapeDebugView(IConstructorShape ctorShape) : IConstructorShape
{
    public IObjectTypeShape DeclaringType => ctorShape.DeclaringType;
    public bool IsPublic => ctorShape.IsPublic;
    public IReadOnlyList<IParameterShape> Parameters => ctorShape.Parameters;
    public ICustomAttributeProvider? AttributeProvider => ctorShape.AttributeProvider;
    object? IConstructorShape.Accept(TypeShapeVisitor visitor, object? state) => ctorShape.Accept(visitor, state);
}

/// <summary>
/// Debugger proxy for <see cref="IMethodShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class MethodShapeDebugView(IMethodShape methodShape) : IMethodShape
{
    public string Name => methodShape.Name;
    public ITypeShape DeclaringType => methodShape.DeclaringType;
    public ITypeShape ReturnType => methodShape.ReturnType;
    public bool IsPublic => methodShape.IsPublic;
    public bool IsStatic => methodShape.IsStatic;
    public bool IsVoidLike => methodShape.IsVoidLike;
    public bool IsAsync => methodShape.IsAsync;
    public IReadOnlyList<IParameterShape> Parameters => methodShape.Parameters;
    public ICustomAttributeProvider? AttributeProvider => methodShape.AttributeProvider;
    object? IMethodShape.Accept(TypeShapeVisitor visitor, object? state) => methodShape.Accept(visitor, state);
}

/// <summary>
/// Debugger proxy for <see cref="IParameterShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class ParameterShapeDebugView(IParameterShape parameterShape) : IParameterShape
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
    public ICustomAttributeProvider? AttributeProvider => parameterShape.AttributeProvider;
    object? IParameterShape.Accept(TypeShapeVisitor visitor, object? state) => parameterShape.Accept(visitor, state);
}

/// <summary>
/// Debugger proxy for <see cref="IPropertyShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class PropertyShapeDebugView(IPropertyShape propertyShape) : IPropertyShape
{
    public IObjectTypeShape DeclaringType => propertyShape.DeclaringType;
    public ITypeShape PropertyType => propertyShape.PropertyType;
    public string Name => propertyShape.Name;
    public int Position => propertyShape.Position;
    public bool HasGetter => propertyShape.HasGetter;
    public bool HasSetter => propertyShape.HasSetter;
    public bool IsField => propertyShape.IsField;
    public bool IsGetterPublic => propertyShape.IsGetterPublic;
    public bool IsSetterPublic => propertyShape.IsSetterPublic;
    public bool IsGetterNonNullable => propertyShape.IsGetterNonNullable;
    public bool IsSetterNonNullable => propertyShape.IsSetterNonNullable;
    public ICustomAttributeProvider? AttributeProvider => propertyShape.AttributeProvider;
    object? IPropertyShape.Accept(TypeShapeVisitor visitor, object? state) => propertyShape.Accept(visitor, state);
}

/// <summary>
/// Debugger proxy for <see cref="IEventShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EventShapeDebugView(IEventShape eventShape) : IEventShape
{
    public ITypeShape DeclaringType => eventShape.DeclaringType;
    public string Name => eventShape.Name;
    public bool IsPublic => eventShape.IsPublic;
    public bool IsStatic => eventShape.IsStatic;
    public IFunctionTypeShape HandlerType => eventShape.HandlerType;
    public ICustomAttributeProvider? AttributeProvider => eventShape.AttributeProvider;
    object? IEventShape.Accept(TypeShapeVisitor visitor, object? state) => eventShape.Accept(visitor, state);
}

/// <summary>
/// Debugger proxy for <see cref="IUnionCaseShape"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class UnionCaseShapeDebugView(IUnionCaseShape unionCaseShape) : IUnionCaseShape
{
    public string Name => unionCaseShape.Name;
    public int Tag => unionCaseShape.Tag;
    public bool IsTagSpecified => unionCaseShape.IsTagSpecified;
    public int Index => unionCaseShape.Index;
    public ITypeShape UnionCaseType => unionCaseShape.UnionCaseType;
    object? IUnionCaseShape.Accept(TypeShapeVisitor visitor, object? state) => unionCaseShape.Accept(visitor, state);
}
