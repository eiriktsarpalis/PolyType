using PolyType.Abstractions;
using PolyType.SourceGenModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[DebuggerTypeProxy(typeof(PolyType.Debugging.ObjectTypeShapeDebugView))]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class FSharpUnitTypeShape<TUnit>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ReflectionTypeShape<TUnit>(provider, options),
    IObjectTypeShape<TUnit>
    where TUnit : class?
{
    private UnitConstructorShape? _constructor;

    public bool IsRecordType => false;
    public bool IsTupleType => false;
    public IReadOnlyList<IPropertyShape> Properties => [];
    public IConstructorShape? Constructor => _constructor ?? CommonHelpers.ExchangeIfNull(ref _constructor, new(this));
    public override TypeShapeKind Kind => TypeShapeKind.Object;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitObject(this, state);

    [DebuggerTypeProxy(typeof(PolyType.Debugging.ConstructorShapeDebugView))]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private sealed class UnitConstructorShape(FSharpUnitTypeShape<TUnit> declaringType) : IConstructorShape<TUnit, EmptyArgumentState>
    {
        public IObjectTypeShape<TUnit> DeclaringType => declaringType;
        public bool IsPublic => true;
        public ICustomAttributeProvider? AttributeProvider => null;
        public IReadOnlyList<IParameterShape> Parameters => [];
        IObjectTypeShape IConstructorShape.DeclaringType => DeclaringType;
        public object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitConstructor(this, state);
        public Func<TUnit> GetDefaultConstructor() => () => null!;
        public Func<EmptyArgumentState> GetArgumentStateConstructor()
        {
            throw new InvalidOperationException("F# unit type does not support parameterized constructors.");
        }

        public Constructor<EmptyArgumentState, TUnit> GetParameterizedConstructor()
        {
            throw new InvalidOperationException("F# unit type does not support parameterized constructors.");
        }

        private string DebuggerDisplay => $".ctor({string.Join(", ", Parameters.Select(p => $"{p.ParameterType} {p.Name}"))})";
    }
}
