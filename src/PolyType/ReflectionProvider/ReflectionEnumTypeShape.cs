﻿using PolyType.Abstractions;
using PolyType.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal sealed class ReflectionEnumTypeShape<TEnum, TUnderlying>(ReflectionTypeShapeProvider provider) : ReflectionTypeShape<TEnum>(provider), IEnumTypeShape<TEnum, TUnderlying>
    where TEnum : struct, Enum
    where TUnderlying : unmanaged
{
    private Dictionary<string, TUnderlying>? _members;

    public override TypeShapeKind Kind => TypeShapeKind.Enum;
    public override object? Accept(TypeShapeVisitor visitor, object? state = null) => visitor.VisitEnum(this, state);
    public ITypeShape<TUnderlying> UnderlyingType => Provider.GetShape<TUnderlying>();
    ITypeShape IEnumTypeShape.UnderlyingType => UnderlyingType;
    public IReadOnlyDictionary<string, TUnderlying> Members => _members ?? InitializeMembers();

    private Dictionary<string, TUnderlying> InitializeMembers()
    {
        FieldInfo[] fields = typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static);
        Dictionary<string, TUnderlying> members = new(fields.Length, StringComparer.Ordinal);
        foreach (FieldInfo field in fields)
        {
            if (field.IsSpecialName || !field.IsLiteral)
            {
                continue;
            }

            object? value = field.GetRawConstantValue();
            if (value is TUnderlying underlyingValue)
            {
                var shapeAttribute = field.GetCustomAttribute<EnumMemberShapeAttribute>();
                members.Add(shapeAttribute?.Name ?? field.Name, underlyingValue);
            }
        }

        return Interlocked.CompareExchange(ref _members, members, null) ?? members;
    }
}
