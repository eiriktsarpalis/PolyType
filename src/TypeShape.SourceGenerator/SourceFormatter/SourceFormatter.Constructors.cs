﻿using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;

namespace TypeShape.SourceGenerator;

internal static partial class SourceFormatter
{
    private const string BitArrayFQN = "global::System.Collections.BitArray";
    private const string FlagsArgumentStateLabel = "Flags";

    private static void FormatConstructorFactory(SourceWriter writer, string methodName, ObjectShapeModel type, ConstructorShapeModel constructor)
    {
        string constructorArgumentStateFQN = FormatConstructorArgumentStateFQN(type, constructor);
        string? constructorParameterFactoryName = constructor.TotalArity > 0 ? FormatConstructorParameterFactoryName(type) : null;
        
        writer.WriteLine($"private global::TypeShape.Abstractions.IConstructorShape {methodName}()");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($$"""
            return new global::TypeShape.SourceGenModel.SourceGenConstructorShape<{{type.Type.FullyQualifiedName}}, {{constructorArgumentStateFQN}}>
            {
                DeclaringType = (global::TypeShape.Abstractions.IObjectTypeShape<{{type.Type.FullyQualifiedName}}>){{type.Type.GeneratedPropertyName}},
                ParameterCount = {{constructor.TotalArity}},
                GetParametersFunc = {{FormatNull(constructorParameterFactoryName)}},
                DefaultConstructorFunc = {{FormatDefaultCtor(type, constructor)}},
                ArgumentStateConstructorFunc = {{FormatArgumentStateCtor(constructor, constructorArgumentStateFQN)}},
                ParameterizedConstructorFunc = {{FormatParameterizedCtor(type, constructor, constructorArgumentStateFQN)}},
                AttributeProviderFunc = {{FormatAttributeProviderFunc(type, constructor)}},
                IsPublic = {{FormatBool(constructor.IsPublic)}},
            };
            """, trimNullAssignmentLines: true);

        writer.Indentation--;
        writer.WriteLine('}');
        
        if (constructorParameterFactoryName != null)
        {
            writer.WriteLine();
            FormatConstructorParameterFactory(writer, type, constructorParameterFactoryName, constructor, constructorArgumentStateFQN);
        }
        
        static string FormatAttributeProviderFunc(ObjectShapeModel type, ConstructorShapeModel constructor)
        {
            if (type.IsTupleType || constructor.IsStaticFactory)
            {
                return "null";
            }

            string parameterTypes = constructor.Parameters.Length == 0
                ? "global::System.Array.Empty<global::System.Type>()"
                : $$"""new[] { {{string.Join(", ", constructor.Parameters.Select(p => $"typeof({p.ParameterType.FullyQualifiedName})"))}} }""";

            return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, {parameterTypes})";
        }

        static string FormatArgumentStateCtor(ConstructorShapeModel constructor, string constructorArgumentStateFQN)
        {
            if (constructor.TotalArity == 0)
            {
                return "null";
            }
            
            string argumentCtorExpr = (constructor.Parameters.Length, constructor.RequiredMembers.Length, constructor.OptionalMemberFlagsType) switch
            {
                (0, 0, OptionalMemberFlagsType.None) => "null!",
                (1, 0, OptionalMemberFlagsType.None) => FormatDefaultValueExpr(constructor.Parameters[0]),
                (0, 1, OptionalMemberFlagsType.None) => FormatDefaultValueExpr(constructor.RequiredMembers[0]),
                (_, _, not OptionalMemberFlagsType.BitArray) when !constructor.Parameters.Any(p => p.HasDefaultValue) => $"default({constructorArgumentStateFQN})",
                (_, _, OptionalMemberFlagsType.None) => 
                    FormatTupleConstructor(
                        constructor.Parameters
                        .Concat(constructor.RequiredMembers)
                        .Select(FormatDefaultValueExpr)),
                (_, _, OptionalMemberFlagsType flagType) =>
                    FormatTupleConstructor(
                        constructor.Parameters
                        .Concat(constructor.RequiredMembers)
                        .Concat(constructor.OptionalMembers)
                        .Select(FormatDefaultValueExpr)
                        .Append(
                            flagType is OptionalMemberFlagsType.BitArray 
                            ? $"new {BitArrayFQN}({constructor.OptionalMembers.Length})"
                            : "default")),
            };

            return $"static () => {argumentCtorExpr}";
            static string FormatTupleConstructor(IEnumerable<string> elementValues)
                => $"({string.Join(", ", elementValues)})";
        }

        static string FormatParameterizedCtor(ObjectShapeModel type, ConstructorShapeModel constructor, string constructorArgumentStateFQN)
        {
            if (constructor.TotalArity == 0)
            {
                return "null";
            }
            
            return $"static (ref {constructorArgumentStateFQN} state) => {FormatParameterizedCtorExpr(type, constructor, "state")}";
            static string FormatParameterizedCtorExpr(ObjectShapeModel type, ConstructorShapeModel constructor, string stateVar)
            {
                if (type.IsValueTupleType)
                {
                    return constructor.TotalArity switch
                    {
                        0 => $"default({type.Type.FullyQualifiedName})",
                        1 => $"new ({stateVar})",
                        _ => stateVar,
                    };
                }

                if (type.IsTupleType)
                {
                    Debug.Assert(constructor.Parameters.Length > 0);
                    Debug.Assert(constructor.RequiredMembers.Length == 0);

                    if (constructor.Parameters.Length == 1)
                    {
                        return $"new ({stateVar})";
                    }

                    var sb = new StringBuilder();
                    int indentation = 0;
                    for (int i = 0; i < constructor.Parameters.Length; i++)
                    {
                        if (i % 7 == 0)
                        {
                            sb.Append("new (");
                            indentation++;
                        }

                        sb.Append($"{FormatCtorParameterExpr(constructor.Parameters[i])}, ");
                    }

                    sb.Length -= 2;
                    sb.Append(')', indentation);
                    return sb.ToString();
                }

                string? memberInitializerBlock = constructor.RequiredMembers.Length switch
                {
                    0 => null,
                    _ when !constructor.IsAccessible => null, // Can't use member initializers with unsafe accessors
                    1 when constructor.TotalArity == 1 => $$""" { {{constructor.RequiredMembers[0].UnderlyingMemberName}} = {{stateVar}} }""",
                    _ => $$""" { {{FormatInitializerBody()}} }""",
                };

                string constructorExpr = constructor.Parameters.Length switch
                {
                    0 when memberInitializerBlock is null => $"{FormatConstructorName(constructor)}()",
                    0 => $"{FormatConstructorName(constructor)}{memberInitializerBlock}",
                    1 when constructor.TotalArity == 1 => $"{FormatConstructorName(constructor)}({FormatCtorParameterExpr(constructor.Parameters[0], isSingleParameter: true)})",
                    _ => $"{FormatConstructorName(constructor)}({FormatCtorArgumentsBody()}){memberInitializerBlock}",
                };

                // Initialize required members using regular assignments if the constructor is not accessible.
                string? requiredMemberAssignments = constructor.RequiredMembers.Length > 0 && !constructor.IsAccessible
                    ? FormatRequiredMemberAssignments()
                    : null;

                // Initialize optional members using conditional assignments.
                string? optionalMemberAssignments = constructor.OptionalMembers.Length > 0
                    ? FormatOptionalMemberAssignments()
                    : null;

                return requiredMemberAssignments is null && optionalMemberAssignments is null 
                    ? constructorExpr
                    : $$"""{ var obj = {{constructorExpr}}; {{requiredMemberAssignments}} {{optionalMemberAssignments}} return obj; }""";

                string FormatCtorArgumentsBody() => string.Join(", ", constructor.Parameters.Select(p => FormatCtorParameterExpr(p)));
                string FormatInitializerBody() => string.Join(", ", constructor.RequiredMembers.Select(p => $"{p.UnderlyingMemberName} = {FormatCtorParameterExpr(p)}"));
                string FormatRequiredMemberAssignments() => string.Join(" ", constructor.RequiredMembers.Select(FormatMemberAssignment));
                string FormatOptionalMemberAssignments() => string.Join(" ", constructor.OptionalMembers.Select(FormatOptionalMemberAssignment));
                string FormatOptionalMemberAssignment(ConstructorParameterShapeModel parameter)
                {
                    Debug.Assert(parameter.Kind is ParameterKind.OptionalMember);
                    int flagOffset = parameter.Position - constructor.Parameters.Length - constructor.RequiredMembers.Length;
                    Debug.Assert(flagOffset >= 0);
                    string conditionalExpr = constructor.OptionalMemberFlagsType is OptionalMemberFlagsType.BitArray
                        ? $"{stateVar}.{FlagsArgumentStateLabel}[{flagOffset}]"
                        : $"({stateVar}.{FlagsArgumentStateLabel} & {1 << flagOffset}) != 0";
                    
                    string assignmentBody = FormatMemberAssignment(parameter);

                    return $"if ({conditionalExpr}) {assignmentBody}";
                }

                string FormatMemberAssignment(ConstructorParameterShapeModel parameter)
                {
                    if (parameter.IsInitOnlyProperty || !parameter.IsAccessible)
                    {
                        string refPrefix = parameter.DeclaringType.IsValueType ? "ref " : "";
                        if (parameter.IsField)
                        {
                            string accessorName = GetFieldAccessorName(parameter.DeclaringType, parameter.UnderlyingMemberName);
                            return $"{accessorName}({refPrefix}obj) = {FormatCtorParameterExpr(parameter)};";
                        }
                        else
                        {
                            string accessorName = GetPropertySetterAccessorName(parameter.DeclaringType, parameter.UnderlyingMemberName);
                            return $"{accessorName}({refPrefix}obj, {FormatCtorParameterExpr(parameter)});";
                        }
                    }

                    return $"obj.{parameter.UnderlyingMemberName} = {FormatCtorParameterExpr(parameter)};";
                }

                string FormatCtorParameterExpr(ConstructorParameterShapeModel parameter, bool isSingleParameter = false)
                {
                    // Reserved for cases where we have Nullable<T> ctor parameters with [DisallowNull] annotation.
                    bool requiresSuppression = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is
                    {
                        ParameterType.SpecialType: SpecialType.System_Nullable_T,
                        IsNonNullable: true
                    };

                    string refPrefix = parameter.RefKind switch
                    {
                        RefKind.Ref or
                        RefKind.RefReadOnlyParameter => "ref ",
                        RefKind.In => "in ",
                        RefKind.Out => "out ",
                        _ => ""
                    };
                    
                    return isSingleParameter
                        ? $"{refPrefix}{stateVar}{(requiresSuppression ? "!" : "")}"
                        : $"{refPrefix}{stateVar}.Item{parameter.Position + 1}{(requiresSuppression ? "!" : "")}";
                }
            }
        }

        static string FormatDefaultCtor(ObjectShapeModel type, ConstructorShapeModel constructor)
            => constructor.TotalArity switch
            {
                0 when type.IsValueTupleType => $"static () => default({type.Type.FullyQualifiedName})",
                0 => $"static () => {FormatConstructorName(constructor)}()",
                _ => "null",
            };

        static string FormatConstructorName(ConstructorShapeModel constructor)
        {
            return constructor switch
            {
                { StaticFactoryName: string factoryName } => factoryName,
                { IsAccessible: false } => GetConstructorAccessorName(constructor.DeclaringType),
                _ => $"new {constructor.DeclaringType.FullyQualifiedName}",
            };
        }

        static string FormatConstructorParameterFactoryName(TypeShapeModel type) =>
            $"CreateConstructorParameters_{type.Type.GeneratedPropertyName}";
    }

    private static void FormatConstructorParameterFactory(SourceWriter writer, ObjectShapeModel type, string methodName, ConstructorShapeModel constructor, string constructorArgumentStateFQN)
    {
        writer.WriteLine($"private global::TypeShape.Abstractions.IConstructorParameterShape[] {methodName}() => new global::TypeShape.Abstractions.IConstructorParameterShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        int i = 0;
        foreach (ConstructorParameterShapeModel parameter in constructor.Parameters
                                                            .Concat(constructor.RequiredMembers)
                                                            .Concat(constructor.OptionalMembers))
        {
            if (i > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($$"""
                new global::TypeShape.SourceGenModel.SourceGenConstructorParameterShape<{{constructorArgumentStateFQN}}, {{parameter.ParameterType.FullyQualifiedName}}>
                {
                    Position = {{parameter.Position}},
                    Name = {{FormatStringLiteral(parameter.Name)}},
                    ParameterType = {{parameter.ParameterType.GeneratedPropertyName}},
                    Kind = {{FormatParameterKind(parameter)}},
                    IsRequired = {{FormatBool(parameter.IsRequired)}},
                    IsNonNullable = {{FormatBool(parameter.IsNonNullable)}},
                    IsPublic = {{FormatBool(parameter.IsPublic)}},
                    HasDefaultValue = {{FormatBool(parameter.HasDefaultValue)}},
                    DefaultValue = {{FormatDefaultValueExpr(parameter)}},
                    Setter = static (ref {{constructorArgumentStateFQN}} state, {{parameter.ParameterType.FullyQualifiedName}} value) => {{FormatSetterBody(constructor, parameter)}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(type, constructor, parameter)}},
                },
                """, trimNullAssignmentLines: true);

            i++;

            static string FormatAttributeProviderFunc(ObjectShapeModel type, ConstructorShapeModel constructor, ConstructorParameterShapeModel parameter)
            {
                if (type.IsTupleType || constructor.IsStaticFactory)
                {
                    return "null";
                }

                if (parameter.Kind is not ParameterKind.ConstructorParameter)
                {
                    return parameter.IsField
                        ? $$"""static () => typeof({{parameter.DeclaringType.FullyQualifiedName}}).GetField({{FormatStringLiteral(parameter.UnderlyingMemberName)}}, {{InstanceBindingFlagsConstMember}})"""
                        : $$"""static () => typeof({{parameter.DeclaringType.FullyQualifiedName}}).GetProperty({{FormatStringLiteral(parameter.UnderlyingMemberName)}}, {{InstanceBindingFlagsConstMember}}, null, typeof({{parameter.ParameterType}}), [], null)""";
                }

                string parameterTypes = constructor.Parameters.Length == 0
                    ? "global::System.Array.Empty<global::System.Type>()"
                    : $$"""new[] { {{string.Join(", ", constructor.Parameters.Select(p => $"typeof({p.ParameterType.FullyQualifiedName})"))}} }""";

                return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, {parameterTypes})?.GetParameters()[{parameter.Position}]";
            }

            static string FormatSetterBody(ConstructorShapeModel constructor, ConstructorParameterShapeModel parameter)
            {
                // Suppress non-nullable Nullable<T> property setters (i.e. setters with [DisallowNull] annotation)
                bool suppressSetter = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is 
                { 
                    ParameterType.SpecialType: SpecialType.System_Nullable_T,
                    IsNonNullable: true,
                };
                
                string assignValueExpr = constructor.TotalArity switch
                {
                    1 when constructor.OptionalMembers.Length == 0 => $"state = value{(suppressSetter ? "!" : "")}",
                    _ => $"state.Item{parameter.Position + 1} = value{(suppressSetter ? "!" : "")}",
                };

                if (parameter.Kind is ParameterKind.OptionalMember)
                {
                    int flagOffset = parameter.Position - constructor.Parameters.Length - constructor.RequiredMembers.Length;
                    Debug.Assert(flagOffset >= 0);
                    string setFlagExpr = constructor.OptionalMemberFlagsType is OptionalMemberFlagsType.BitArray
                        ? $"state.{FlagsArgumentStateLabel}[{flagOffset}] = true"
                        : $"state.{FlagsArgumentStateLabel} |= {1 << flagOffset}";

                    return $$"""{ {{assignValueExpr}}; {{setFlagExpr}}; }""";
                }

                return assignValueExpr;
            }

            static string FormatParameterKind(ConstructorParameterShapeModel parameter)
            {
                string identifier = parameter.Kind switch
                {
                    ParameterKind.ConstructorParameter => "ConstructorParameter",
                    ParameterKind.RequiredMember or
                    ParameterKind.OptionalMember => parameter.IsField ? "FieldInitializer" : "PropertyInitializer",
                    _ => throw new InvalidOperationException($"Unsupported parameter kind: {parameter.Kind}"),
                };

                return $"global::TypeShape.Abstractions.ConstructorParameterKind.{identifier}";
            }
        }

        writer.Indentation--;
        writer.WriteLine("};");
    }

    private static string FormatDefaultValueExpr(ConstructorParameterShapeModel constructorParameter)
    {
        return constructorParameter switch
        {
            { DefaultValueExpr: string defaultValueExpr } => defaultValueExpr,
            { ParameterType.IsValueType: true } => "default",
            _ => "default!",
        };
    }

    private static string FormatConstructorArgumentStateFQN(ObjectShapeModel type, ConstructorShapeModel constructorModel)
    {
        if (type.IsValueTupleType && constructorModel.TotalArity > 1)
        {
            // For tuple types, just use the type as the argument state.
            return constructorModel.DeclaringType.FullyQualifiedName;
        }

        string? optionalParameterFlagTypeFQN = constructorModel.OptionalMemberFlagsType switch
        {
            OptionalMemberFlagsType.Byte => "byte",
            OptionalMemberFlagsType.UShort => "ushort",
            OptionalMemberFlagsType.UInt32 => "uint",
            OptionalMemberFlagsType.ULong => "ulong",
            OptionalMemberFlagsType.BitArray => BitArrayFQN,
            _ => null,
        };

        return (constructorModel.Parameters.Length, constructorModel.RequiredMembers.Length, optionalParameterFlagTypeFQN) switch
        {
            (0, 0, null) => "object?",
            (1, 0, null) => constructorModel.Parameters[0].ParameterType.FullyQualifiedName,
            (0, 1, null) => constructorModel.RequiredMembers[0].ParameterType.FullyQualifiedName,
            (_, _, null) => FormatTupleType(
                constructorModel.Parameters
                .Concat(constructorModel.RequiredMembers)
                .Select(p => p.ParameterType.FullyQualifiedName)),

            (_, _, { }) =>
                FormatTupleType(
                    constructorModel.Parameters
                    .Concat(constructorModel.RequiredMembers)
                    .Concat(constructorModel.OptionalMembers)
                    .Select(p => p.ParameterType.FullyQualifiedName)
                    .Append($"{optionalParameterFlagTypeFQN} {FlagsArgumentStateLabel}"))
        };

        static string FormatTupleType(IEnumerable<string> parameterTypes)
            => $"({string.Join(", ", parameterTypes)})";
    }

    private static string GetConstructorAccessorName(TypeId declaringType)
    {
        return $"{declaringType.GeneratedPropertyName}_CtorAccessor";
    }

    private static void FormatConstructorAccessor(SourceWriter writer, ConstructorShapeModel constructorModel)
    {
        Debug.Assert(!constructorModel.IsAccessible);

        StringBuilder parameterSignature = new();
        foreach (ConstructorParameterShapeModel parameter in constructorModel.Parameters)
        {
            string refPrefix = parameter.RefKind switch
            {
                RefKind.Ref or
                RefKind.RefReadOnlyParameter => "ref ",
                RefKind.In => "in ",
                RefKind.Out => "out ",
                _ => ""
            };

            parameterSignature.Append($"{refPrefix}{parameter.ParameterType.FullyQualifiedName} {parameter.Name}, ");
        }

        if (parameterSignature.Length > 0)
        {
            parameterSignature.Length -= 2;
        }

        string accessorName = GetConstructorAccessorName(constructorModel.DeclaringType);
        writer.WriteLine($"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Constructor)]
            private static extern {constructorModel.DeclaringType.FullyQualifiedName} {accessorName}({parameterSignature});
            """);
    }
}
