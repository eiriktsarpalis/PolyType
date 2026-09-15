using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;
using System.Diagnostics;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatPropertyFactory(SourceWriter writer, string methodName, ObjectShapeModel type)
    {
        writer.WriteLine($"private global::PolyType.Abstractions.IPropertyShape[] {methodName}() => new global::PolyType.Abstractions.IPropertyShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        foreach (PropertyShapeModel property in type.Properties)
        {
            if (i > 0)
            {
                writer.WriteLine();
            }

            // Suppress property getters that are nullable, but are not Nullable<T>
            bool suppressGetter = property.PropertyTypeContainsNullabilityAnnotations || property is
            {
                PropertyType.SpecialType: not SpecialType.System_Nullable_T,
                IsGetterNonNullable: false
            };

            // Suppress non-nullable Nullable<T> property setters (i.e. setters with [DisallowNull] annotation)
            bool suppressSetter = property.PropertyTypeContainsNullabilityAnnotations || property is 
            { 
                PropertyType.SpecialType: SpecialType.System_Nullable_T,
                IsSetterNonNullable: true,
            };

            string? attributeFactoryName = GetAttributeFactoryName(property);

            writer.WriteLine($$"""
                new global::PolyType.SourceGenModel.SourceGenPropertyShape<{{type.Type.FullyQualifiedName}}, {{property.PropertyType.FullyQualifiedName}}>
                {
                    Position = {{i}},
                    Name = {{FormatStringLiteral(property.Name)}},
                    DeclaringType = (global::PolyType.Abstractions.IObjectTypeShape<{{type.Type.FullyQualifiedName}}>){{type.SourceIdentifier}},
                    PropertyType = {{GetShapeModel(property.PropertyType).SourceIdentifier}},
                    Getter = {{(property.EmitGetter ? $"static (ref {type.Type.FullyQualifiedName} obj) => {FormatGetterBody("obj", type, property)}{(suppressGetter ? "!" : "")}" : "null")}},
                    Setter = {{(property.EmitSetter ? $"static (ref {type.Type.FullyQualifiedName} obj, {property.PropertyType.FullyQualifiedName} value) => {FormatSetterBody("obj", "value" + (suppressSetter ? "!" : ""), type, property)}" : "null")}},
                    AttributeFactory = {{FormatNull(attributeFactoryName)}},
                    MemberInfoFactory = {{FormatMemberInfoFactory(type, property)}},
                    IsField = {{FormatBool(property.IsField)}},
                    IsGetterPublic = {{FormatBool(property.IsGetterPublic)}},
                    IsSetterPublic = {{FormatBool(property.IsSetterPublic)}},
                    IsGetterNonNullable = {{FormatBool(property.IsGetterNonNullable)}},
                    IsSetterNonNullable = {{FormatBool(property.IsSetterNonNullable)}},
                },
                """, trimDefaultAssignmentLines: true);

            i++;

            static string FormatMemberInfoFactory(ObjectShapeModel type, PropertyShapeModel property)
            {
                if (type.IsTupleType)
                {
                    return "null";
                }

                return property.IsField
                    ? $$"""static () => typeof({{property.DeclaringType.FullyQualifiedName}}).GetField({{FormatStringLiteral(property.UnderlyingMemberName)}}, {{InstanceBindingFlagsConstMember}})"""
                    : $$"""static () => typeof({{property.DeclaringType.FullyQualifiedName}}).GetProperty({{FormatStringLiteral(property.UnderlyingMemberName)}}, {{InstanceBindingFlagsConstMember}}, null, typeof({{property.PropertyType.FullyQualifiedName}}), global::System.Type.EmptyTypes, null)""";
            }

            static string FormatGetterBody(string objParam, ObjectShapeModel declaringType, PropertyShapeModel property)
            {
                if (!property.IsGetterAccessible)
                {
                    string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";
                    if (property.IsField)
                    {
                        string fieldAccessorName = GetFieldAccessorName(declaringType, property.UnderlyingMemberName, property.DeclaringTypeIndex, property.GenericDeclaringType);
                        return $"{fieldAccessorName}({refPrefix}{objParam})";
                    }
                    else
                    {
                        string propertyGetterAccessorName = GetPropertyGetterAccessorName(declaringType, property.UnderlyingMemberName, property.DeclaringTypeIndex, property.GenericDeclaringType);
                        return $"{propertyGetterAccessorName}({refPrefix}{objParam})";
                    }
                }

                if (property.RequiresDisambiguation)
                {
                    objParam = $"(({property.DeclaringType.FullyQualifiedName}){objParam})";
                }

                return $"{objParam}.{RoslynHelpers.EscapeKeywordIdentifier(property.UnderlyingMemberName)}";
            }

            static string FormatSetterBody(string objParam, string valueParam, ObjectShapeModel declaringType, PropertyShapeModel property)
            {
                if (!property.IsSetterAccessible)
                {
                    string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";
                    if (property.IsField)
                    {
                        string fieldAccessorName = GetFieldAccessorName(declaringType, property.UnderlyingMemberName, property.DeclaringTypeIndex, property.GenericDeclaringType);
                        return property.CanUseUnsafeAccessors
                            ? $"{fieldAccessorName}({refPrefix}{objParam}) = {valueParam}"
                            : $"{fieldAccessorName}_set({refPrefix}{objParam}, {valueParam})";
                    }
                    else
                    {
                        string propertySetterAccessorName = GetPropertySetterAccessorName(declaringType, property.UnderlyingMemberName, property.DeclaringTypeIndex, property.GenericDeclaringType);
                        return $"{propertySetterAccessorName}({refPrefix}{objParam}, {valueParam})";
                    }
                }

                if (property.RequiresDisambiguation)
                {
                    objParam = $"(({property.DeclaringType.FullyQualifiedName}){objParam})";
                }

                return $"{objParam}.{RoslynHelpers.EscapeKeywordIdentifier(property.UnderlyingMemberName)} = {valueParam}";
            }
        }

        writer.Indentation--;
        writer.WriteLine("};");

        foreach (PropertyShapeModel property in type.Properties)
        {
            if (GetAttributeFactoryName(property) is { } factoryName)
            {
                writer.WriteLine();
                FormatAttributesFactory(writer, factoryName, property.Attributes);
            }
        }

        string? GetAttributeFactoryName(PropertyShapeModel property) =>
            property.Attributes.Length > 0
            ? $"__AttributeFactory_{type.SourceIdentifier}_{property.UnderlyingMemberName.Replace(".", "_")}"
            : null;
    }

    private static string GetFieldAccessorName(TypeShapeModel declaringType, string underlyingMemberName, int declaringTypeIndex = 0, GenericTypeModel? genericType = null)
    {
        string suffix = declaringTypeIndex == 0 ? "" : $"_{declaringTypeIndex}";
        return QualifyAccessorName(declaringType, $"__FieldAccessor_{declaringType.SourceIdentifier}{suffix}_{underlyingMemberName}", declaringTypeIndex, genericType);
    }

    private static string GetPropertyGetterAccessorName(TypeShapeModel declaringType, string underlyingMemberName, int declaringTypeIndex = 0, GenericTypeModel? genericType = null)
    {
        string suffix = declaringTypeIndex == 0 ? "" : $"_{declaringTypeIndex}";
        return QualifyAccessorName(declaringType, $"__GetAccessor_{declaringType.SourceIdentifier}{suffix}_{underlyingMemberName}", declaringTypeIndex, genericType);
    }

    private static string GetPropertySetterAccessorName(TypeShapeModel declaringType, string underlyingMemberName, int declaringTypeIndex = 0, GenericTypeModel? genericType = null)
    {
        string suffix = declaringTypeIndex == 0 ? "" : $"_{declaringTypeIndex}";
        return QualifyAccessorName(declaringType, $"__SetAccessor_{declaringType.SourceIdentifier}{suffix}_{underlyingMemberName}", declaringTypeIndex, genericType);
    }

    private void FormatFieldAccessor(SourceWriter writer, ObjectShapeModel declaringType, PropertyShapeModel property)
    {
        Debug.Assert(property.IsField);
        string accessorName = GetFieldAccessorName(declaringType, property.UnderlyingMemberName, property.DeclaringTypeIndex);
        string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";

        if (!property.CanUseUnsafeAccessors)
        {
            // Emit a reflection-based workaround.
            writer.WriteLine();
            writer.WriteLine($$"""
                private static global::System.Reflection.FieldInfo {{accessorName}}_FieldInfo => __s_{{accessorName}}_FieldInfo ??= typeof({{property.DeclaringType.FullyQualifiedName}}).GetField({{FormatStringLiteral(property.UnderlyingMemberName)}}, {{InstanceBindingFlagsConstMember}})!;
                private static global::System.Reflection.FieldInfo? __s_{{accessorName}}_FieldInfo;

                private static {{property.PropertyType.FullyQualifiedName}} {{accessorName}}({{refPrefix}}{{property.DeclaringType.FullyQualifiedName}} obj)
                {
                    return ({{property.PropertyType.FullyQualifiedName}}){{accessorName}}_FieldInfo.GetValue(obj)!;
                }
                """);

            if (property.EmitSetter)
            {
                writer.WriteLine();
                writer.WriteLine($$"""
                    private static void {{accessorName}}_set({{refPrefix}}{{property.DeclaringType.FullyQualifiedName}} obj, {{property.PropertyType.FullyQualifiedName}} value)
                    {
                        object boxedObj = obj;
                        {{accessorName}}_FieldInfo.SetValue(boxedObj, value);
                        obj = ({{property.DeclaringType.FullyQualifiedName}})boxedObj;
                    }
                    """);
            }

            return;
        }

        string modifiers = GetUnsafeAccessorModifiers(property.GenericDeclaringType);
        string receiverType = property.GenericDeclaringType?.FullyQualifiedName ?? property.DeclaringType.FullyQualifiedName;
        string fieldType = property.OpenPropertyTypeName ?? property.PropertyType.FullyQualifiedName;
        FormatUnsafeAccessor(writer, declaringType, property.DeclaringTypeIndex, property.GenericDeclaringType, $"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = {FormatStringLiteral(property.UnderlyingMemberName)})]
            {modifiers} ref {fieldType} {accessorName}({refPrefix}{receiverType} obj);
            """);
    }

    private void FormatPropertyGetterAccessor(SourceWriter writer, ObjectShapeModel declaringType, PropertyShapeModel property)
    {
        Debug.Assert(!property.IsField);
        string accessorName = GetPropertyGetterAccessorName(declaringType, property.UnderlyingMemberName, property.DeclaringTypeIndex);
        string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";
        string propertyGetter = "get_" + property.UnderlyingMemberName;

        if (!property.CanUseUnsafeAccessors)
        {
            // Emit a reflection-based workaround.
            string delegateType = property.DeclaringType.IsValueType
                ? $"global::PolyType.Abstractions.Getter<{property.DeclaringType.FullyQualifiedName}, {property.PropertyType.FullyQualifiedName}>"
                : $"global::System.Func<{property.DeclaringType.FullyQualifiedName}, {property.PropertyType.FullyQualifiedName}>";

            writer.WriteLine();
            writer.WriteLine($$"""
                private static {{delegateType}}? {{accessorName}}_Delegate;
                private static {{property.PropertyType.FullyQualifiedName}} {{accessorName}}({{refPrefix}}{{property.DeclaringType.FullyQualifiedName}} obj)
                {
                    return ({{accessorName}}_Delegate ??= CreateDelegate()).Invoke({{refPrefix}}obj);
                    static {{delegateType}} CreateDelegate()
                    {
                        global::System.Reflection.MethodInfo methodInfo = typeof({{property.DeclaringType.FullyQualifiedName}}).GetMethod({{FormatStringLiteral(propertyGetter)}}, {{InstanceBindingFlagsConstMember}})!;
                        return ({{delegateType}})global::System.Delegate.CreateDelegate(typeof({{delegateType}}), methodInfo)!;
                    }
                }
                """);

            return;
        }

        string modifiers = GetUnsafeAccessorModifiers(property.GenericDeclaringType);
        string receiverType = property.GenericDeclaringType?.FullyQualifiedName ?? property.DeclaringType.FullyQualifiedName;
        string propertyType = property.OpenPropertyTypeName ?? property.PropertyType.FullyQualifiedName;
        FormatUnsafeAccessor(writer, declaringType, property.DeclaringTypeIndex, property.GenericDeclaringType, $"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(propertyGetter)})]
            {modifiers} {propertyType} {accessorName}({refPrefix}{receiverType} obj);
            """);
    }

    private void FormatPropertySetterAccessor(SourceWriter writer, ObjectShapeModel declaringType, PropertyShapeModel property)
    {
        Debug.Assert(!property.IsField);
        string accessorName = GetPropertySetterAccessorName(declaringType, property.UnderlyingMemberName, property.DeclaringTypeIndex);
        string refPrefix = property.DeclaringType.IsValueType ? "ref " : "";
        string propertySetter = "set_" + property.UnderlyingMemberName;

        if (!property.CanUseUnsafeAccessors)
        {
            // Emit a reflection-based workaround.
            string delegateType = property.DeclaringType.IsValueType
                ? $"global::PolyType.Abstractions.Setter<{property.DeclaringType.FullyQualifiedName}, {property.PropertyType.FullyQualifiedName}>" 
                : $"global::System.Action<{property.DeclaringType.FullyQualifiedName}, {property.PropertyType.FullyQualifiedName}>";

            writer.WriteLine();
            writer.WriteLine($$"""
                private static {{delegateType}}? {{accessorName}}_Delegate;
                private static void {{accessorName}}({{refPrefix}}{{property.DeclaringType.FullyQualifiedName}} obj, {{property.PropertyType.FullyQualifiedName}} value)
                {
                    ({{accessorName}}_Delegate ??= CreateDelegate()).Invoke({{refPrefix}}obj, value);
                    static {{delegateType}} CreateDelegate()
                    {
                        global::System.Reflection.MethodInfo methodInfo = typeof({{property.DeclaringType.FullyQualifiedName}}).GetMethod({{FormatStringLiteral(propertySetter)}}, {{InstanceBindingFlagsConstMember}})!;
                        return ({{delegateType}})global::System.Delegate.CreateDelegate(typeof({{delegateType}}), methodInfo)!;
                    }
                }
                """);

            return;
        }

        string modifiers = GetUnsafeAccessorModifiers(property.GenericDeclaringType);
        string receiverType = property.GenericDeclaringType?.FullyQualifiedName ?? property.DeclaringType.FullyQualifiedName;
        string propertyType = property.OpenPropertyTypeName ?? property.PropertyType.FullyQualifiedName;
        FormatUnsafeAccessor(writer, declaringType, property.DeclaringTypeIndex, property.GenericDeclaringType, $"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(propertySetter)})]
            {modifiers} void {accessorName}({refPrefix}{receiverType} obj, {propertyType} value);
            """);
    }
}
