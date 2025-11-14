using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;
using PolyType.SourceGenerator.Model;
using PolyType.SourceGenModel;
using System.Diagnostics;
using System.Text;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private const RefKind RefReadOnlyParameter = (RefKind)4; // RefKind.RefReadOnlyParameter

    private void FormatConstructorFactory(SourceWriter writer, string methodName, ObjectShapeModel type, ConstructorShapeModel constructor)
    {
        string constructorArgumentStateFQN = FormatConstructorArgumentStateFQN(type, constructor);
        string? constructorParameterFactoryName = constructor.TotalArity > 0 ? $"__CreateConstructorParameters_{type.SourceIdentifier}" : null;
        string? attributeFactoryName = constructor.Attributes.Length > 0 ? $"__CreateConstructorAttributes_{type.SourceIdentifier}" : null;

        writer.WriteLine($"private global::PolyType.Abstractions.IConstructorShape {methodName}()");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($$"""
            return new global::PolyType.SourceGenModel.SourceGenConstructorShape<{{type.Type.FullyQualifiedName}}, {{constructorArgumentStateFQN}}>
            {
                DeclaringType = (global::PolyType.Abstractions.IObjectTypeShape<{{type.Type.FullyQualifiedName}}>){{type.SourceIdentifier}},
                ParametersFactory = {{FormatNull(constructorParameterFactoryName)}},
                DefaultConstructor = {{FormatDefaultCtor(type, constructor)}},
                ArgumentStateConstructor = {{FormatArgumentStateCtor(type, constructor, constructorArgumentStateFQN)}},
                ParameterizedConstructor = {{FormatParameterizedCtor(type, constructor, constructorArgumentStateFQN)}},
                MethodBaseFactory = {{FormatConstructorInfoResolver(type, constructor)}},
                AttributeFactory = {{FormatNull(attributeFactoryName)}},
                IsPublic = {{FormatBool(constructor.IsPublic)}},
            };
            """, trimDefaultAssignmentLines: true);

        writer.Indentation--;
        writer.WriteLine('}');
        
        if (constructorParameterFactoryName != null)
        {
            writer.WriteLine();
            FormatParameterFactory(writer, type, constructorParameterFactoryName, constructor, constructorArgumentStateFQN);
        }

        if (attributeFactoryName is not null)
        {             
            writer.WriteLine();
            FormatAttributesFactory(writer, attributeFactoryName, constructor.Attributes);
        }

        if (FormatRequiredParametersMaskFieldName(type) is { } requiredParametersMaskFieldName)
        {
            Debug.Assert(constructor.TotalArity > 0);
            writer.WriteLine();
            FormatRequiredParametersMaskField(
                writer,
                requiredParametersMaskFieldName,
                constructor.TotalArity,
                constructor.ArgumentStateType,
                constructor.GetAllParameters());
        }
        
        static string FormatConstructorInfoResolver(ObjectShapeModel type, ConstructorShapeModel constructor)
        {
            if (type.IsTupleType || constructor.IsStaticFactory || constructor.IsFSharpUnitConstructor)
            {
                return "null";
            }

            string parameterTypes = constructor.Parameters.Length == 0
                ? "global::System.Type.EmptyTypes"
                : $$"""new global::System.Type[] { {{string.Join(", ", constructor.Parameters.Select(FormatParameterTypeExpr))}} }""";

            return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, null, {parameterTypes}, null)";
        }

        static string FormatArgumentStateCtor(ObjectShapeModel type, ConstructorShapeModel constructor, string constructorArgumentStateFQN)
        {
            if (constructor.TotalArity == 0)
            {
                return "null";
            }

            string requiredMembersMaskFieldName = FormatRequiredParametersMaskFieldName(type)!;
            string stateValueExpr = constructor.TotalArity switch
            {
                _ when !constructor.Parameters.Any(p => p.HasDefaultValue) => "default!",
                1 => FormatDefaultValueExpr(constructor.GetAllParameters().First()),
                _ => FormatTupleConstructor(constructor.GetAllParameters().Select(FormatDefaultValueExpr)),
            };

            return $"static () => new({stateValueExpr}, count: {constructor.TotalArity}, requiredArgumentsMask: {requiredMembersMaskFieldName})";
            static string FormatTupleConstructor(IEnumerable<string> parameters)
                => $"({string.Join(", ", parameters)})";
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
                        1 => $"new ({stateVar}.Arguments)",
                        _ => $"{stateVar}.Arguments",
                    };
                }

                if (type.IsTupleType)
                {
                    Debug.Assert(constructor.Parameters.Length > 0);
                    Debug.Assert(constructor.RequiredMembers.Length == 0);

                    if (constructor.Parameters.Length == 1)
                    {
                        return $"new({stateVar}.Arguments)";
                    }

                    var sb = new StringBuilder();
                    int indentation = 0;
                    for (int i = 0; i < constructor.Parameters.Length; i++)
                    {
                        if (i % 7 == 0)
                        {
                            sb.Append("new(");
                            indentation++;
                        }

                        sb.Append($"{FormatCtorParameterExpr(constructor.Parameters[i], isSingleParameter: false)}, ");
                    }

                    sb.Length -= 2;
                    sb.Append(')', indentation);
                    return sb.ToString();
                }

                string? memberInitializerBlock = constructor.RequiredMembers.Length switch
                {
                    0 => null,
                    _ when !constructor.IsAccessible => null, // Can't use member initializers with unsafe accessors
                    1 when constructor.TotalArity == 1 => $$""" { {{constructor.RequiredMembers[0].UnderlyingMemberName}} = {{stateVar}}.Arguments }""",
                    _ => $$""" { {{FormatInitializerBody()}} }""",
                };

                string castPrefix = constructor.ResultRequiresCast ? $"({type.Type.FullyQualifiedName})" : "";
                string constructorName = FormatConstructorName(type, constructor);
                string constructorExpr = constructor.Parameters.Length switch
                {
                    0 when constructor.StaticFactoryIsProperty => castPrefix + constructorName,
                    0 when memberInitializerBlock is null => $"{castPrefix}{constructorName}()",
                    0 => $"{castPrefix}{constructorName}{memberInitializerBlock}",
                    1 when constructor.TotalArity == 1 => $"{castPrefix}{constructorName}({FormatCtorParameterExpr(constructor.Parameters[0], isSingleParameter: true)})",
                    _ => $"{castPrefix}{constructorName}({FormatCtorArgumentsBody()}){memberInitializerBlock}",
                };

                // Initialize required members using regular assignments if the constructor is not accessible.
                string? requiredMemberAssignments = constructor.RequiredMembers.Length > 0 && !constructor.IsAccessible
                    ? FormatRequiredMemberAssignments()
                    : null;

                // Initialize optional members using conditional assignments.
                string? optionalMemberAssignments = constructor.OptionalMembers.Length > 0
                    ? FormatOptionalMemberAssignments()
                    : null;

                return (requiredMemberAssignments, optionalMemberAssignments) switch
                {
                    (null, null) => constructorExpr,
                    (null, string optionalAssignments) => $$"""{ var obj = {{constructorExpr}}; {{optionalAssignments}} return obj; }""",
                    (string requiredAssignments, null) => $$"""{ var obj = {{constructorExpr}}; {{requiredAssignments}} return obj; }""",
                    (string requiredAssignments, string optionalAssignments) => $$"""{ var obj = {{constructorExpr}}; {{requiredAssignments}} {{optionalAssignments}} return obj; }""",
                };

                string FormatCtorArgumentsBody() => string.Join(", ", constructor.Parameters.Select(p => FormatCtorParameterExpr(p, isSingleParameter: constructor.TotalArity == 1)));
                string FormatInitializerBody() => string.Join(", ", constructor.RequiredMembers.Select(p => $"{p.UnderlyingMemberName} = {FormatCtorParameterExpr(p, isSingleParameter: constructor.TotalArity == 1)}"));
                string FormatRequiredMemberAssignments() => string.Join(" ", constructor.RequiredMembers.Select(p => FormatMemberAssignment(p, isSingleParameter: constructor.TotalArity == 1)));
                string FormatOptionalMemberAssignments() => string.Join(" ", constructor.OptionalMembers.Select(p => FormatOptionalMemberAssignment(p, isSingleParameter: constructor.TotalArity == 1)));
                string FormatOptionalMemberAssignment(ParameterShapeModel parameter, bool isSingleParameter)
                {
                    Debug.Assert(parameter.Kind is ParameterKind.OptionalMember);
                    string assignmentBody = FormatMemberAssignment(parameter, isSingleParameter);
                    return $"if (state.IsArgumentSet({parameter.Position})) {assignmentBody}";
                }

                string FormatMemberAssignment(ParameterShapeModel parameter, bool isSingleParameter)
                {
                    if (parameter.IsInitOnlyProperty || !parameter.IsAccessible)
                    {
                        string refPrefix = parameter.DeclaringType.IsValueType ? "ref " : "";
                        if (parameter.IsField)
                        {
                            string accessorName = GetFieldAccessorName(type, parameter.UnderlyingMemberName);
                            string ctorParameterExpr = FormatCtorParameterExpr(parameter, isSingleParameter);
                            return parameter.CanUseUnsafeAccessors
                                ? $"{accessorName}({refPrefix}obj) = {ctorParameterExpr};"
                                : $"{accessorName}_set({refPrefix}obj, {ctorParameterExpr});";
                        }
                        else
                        {
                            string accessorName = GetPropertySetterAccessorName(type, parameter.UnderlyingMemberName);
                            return $"{accessorName}({refPrefix}obj, {FormatCtorParameterExpr(parameter, isSingleParameter)});";
                        }
                    }

                    return $"obj.{RoslynHelpers.EscapeKeywordIdentifier(parameter.UnderlyingMemberName)} = {FormatCtorParameterExpr(parameter, isSingleParameter)};";
                }

                string FormatCtorParameterExpr(ParameterShapeModel parameter, bool isSingleParameter)
                {
                    // Reserved for cases where we have Nullable<T> ctor parameters with [DisallowNull] annotation.
                    bool requiresSuppression = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is
                    {
                        ParameterType.SpecialType: SpecialType.System_Nullable_T,
                        IsNonNullable: true
                    };

                    string refPrefix = parameter.RefKind switch
                    {
                        RefKind.Ref or RefReadOnlyParameter => "ref ",
                        RefKind.In => "in ",
                        RefKind.Out => "out ",
                        _ => ""
                    };
                    
                    return isSingleParameter
                        ? $"{refPrefix}{stateVar}.Arguments{(requiresSuppression ? "!" : "")}"
                        : $"{refPrefix}{stateVar}.Arguments.Item{parameter.Position + 1}{(requiresSuppression ? "!" : "")}";
                }
            }
        }

        static string FormatDefaultCtor(ObjectShapeModel declaringType, ConstructorShapeModel constructor)
        {
            if (constructor.IsFSharpUnitConstructor)
            {
                return "static () => null!";
            }

            string castPrefix = constructor.ResultRequiresCast ? $"({declaringType.Type.FullyQualifiedName})" : "";
            return constructor.TotalArity switch
            {
                0 when declaringType.IsValueTupleType => $"static () => default({declaringType.Type.FullyQualifiedName})",
                0 when constructor.StaticFactoryIsProperty => $"static () => {castPrefix}{FormatConstructorName(declaringType, constructor)}",
                0 => $"static () => {castPrefix}{FormatConstructorName(declaringType, constructor)}()",
                _ => "null",
            };
        }

        static string FormatConstructorName(ObjectShapeModel declaringType, ConstructorShapeModel constructor)
        {
            return constructor switch
            {
                { StaticFactoryName: string factoryName } => factoryName,
                { IsAccessible: false } => GetConstructorAccessorName(declaringType),
                _ => $"new {constructor.DeclaringType.FullyQualifiedName}",
            };
        }

        static string? FormatRequiredParametersMaskFieldName(ObjectShapeModel objectModel)
        {
            if (objectModel.Constructor?.TotalArity is > 0)
            {
                return $"__RequiredMembersMask_{objectModel.SourceIdentifier}";
            }

            return null;
        }
    }

    private void FormatParameterFactory(SourceWriter writer, ObjectShapeModel type, string methodName, ConstructorShapeModel constructor, string constructorArgumentStateFQN)
    {
        writer.WriteLine($"private global::PolyType.Abstractions.IParameterShape[] {methodName}() => new global::PolyType.Abstractions.IParameterShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        var allParams = constructor.Parameters.Concat(constructor.RequiredMembers).Concat(constructor.OptionalMembers);

        int i = 0;
        foreach (ParameterShapeModel parameter in allParams)
        {
            if (i > 0)
            {
                writer.WriteLine();
            }

            string? attributeFactoryName = GetAttributeFactoryName(parameter);

            writer.WriteLine($$"""
                new global::PolyType.SourceGenModel.SourceGenParameterShape<{{constructorArgumentStateFQN}}, {{parameter.ParameterType.FullyQualifiedName}}>
                {
                    Position = {{parameter.Position}},
                    Name = {{FormatStringLiteral(parameter.Name)}},
                    ParameterType = {{GetShapeModel(parameter.ParameterType).SourceIdentifier}},
                    Kind = {{FormatParameterKind(parameter)}},
                    IsRequired = {{FormatBool(parameter.IsRequired)}},
                    IsNonNullable = {{FormatBool(parameter.IsNonNullable)}},
                    IsPublic = {{FormatBool(parameter.IsPublic)}},
                    HasDefaultValue = {{FormatBool(parameter.HasDefaultValue)}},
                    DefaultValue = {{FormatDefaultValueExpr(parameter)}},
                    Getter = static (ref {{constructorArgumentStateFQN}} state) => {{FormatGetterBody(constructor, parameter)}},
                    Setter = static (ref {{constructorArgumentStateFQN}} state, {{parameter.ParameterType.FullyQualifiedName}} value) => {{FormatSetterBody(constructor, parameter)}},
                    AttributeFactory = {{FormatNull(attributeFactoryName)}},
                    ReflectionInfoFactory = {{FormatAttributeProviderFunc(type, constructor, parameter)}},
                },
                """, trimDefaultAssignmentLines: true);

            i++;

            static string FormatAttributeProviderFunc(ObjectShapeModel type, ConstructorShapeModel constructor, ParameterShapeModel parameter)
            {
                if (type.IsTupleType || constructor.IsStaticFactory)
                {
                    return "null";
                }

                if (parameter.Kind is not ParameterKind.MethodParameter)
                {
                    return parameter.IsField
                        ? $$"""static () => typeof({{parameter.DeclaringType.FullyQualifiedName}}).GetField({{FormatStringLiteral(parameter.UnderlyingMemberName)}}, {{InstanceBindingFlagsConstMember}})"""
                        : $$"""static () => typeof({{parameter.DeclaringType.FullyQualifiedName}}).GetProperty({{FormatStringLiteral(parameter.UnderlyingMemberName)}}, {{InstanceBindingFlagsConstMember}}, null, typeof({{parameter.ParameterType}}), global::System.Type.EmptyTypes, null)""";
                }

                string parameterTypes = constructor.Parameters.Length == 0
                    ? "global::System.Type.EmptyTypes"
                    : $$"""new global::System.Type[] { {{string.Join(", ", constructor.Parameters.Select(FormatParameterTypeExpr))}} }""";

                return $"static () => typeof({constructor.DeclaringType.FullyQualifiedName}).GetConstructor({InstanceBindingFlagsConstMember}, null, {parameterTypes}, null)?.GetParameters()[{parameter.Position}]";
            }

            static string FormatGetterBody(ConstructorShapeModel constructor, ParameterShapeModel parameter)
            {
                // Suppress non-nullable Nullable<T> property getters (i.e. setters with [DisallowNull] annotation)
                bool suppressGetter = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is
                {
                    ParameterType.SpecialType: SpecialType.System_Nullable_T,
                    IsNonNullable: true,
                };

                return constructor.TotalArity switch
                {
                    1 => $"state.Arguments{(suppressGetter ? "!" : "")}",
                    _ => $"state.Arguments.Item{parameter.Position + 1}{(suppressGetter ? "!" : "")}",
                };
            }

            static string FormatSetterBody(ConstructorShapeModel constructor, ParameterShapeModel parameter)
            {
                // Suppress non-nullable Nullable<T> property setters (i.e. setters with [DisallowNull] annotation)
                bool suppressSetter = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is 
                { 
                    ParameterType.SpecialType: SpecialType.System_Nullable_T,
                    IsNonNullable: true,
                };
                
                string assignValueExpr = constructor.TotalArity switch
                {
                    1 => $"state.Arguments = value{(suppressSetter ? "!" : "")}",
                    _ => $"state.Arguments.Item{parameter.Position + 1} = value{(suppressSetter ? "!" : "")}",
                };

                return $$"""{ {{assignValueExpr}}; state.MarkArgumentSet({{parameter.Position}}); }""";
            }

            static string FormatParameterKind(ParameterShapeModel parameter)
            {
                string identifier = parameter.Kind switch
                {
                    ParameterKind.MethodParameter => "MethodParameter",
                    ParameterKind.RequiredMember or
                    ParameterKind.OptionalMember => "MemberInitializer",
                    _ => throw new InvalidOperationException($"Unsupported parameter kind: {parameter.Kind}"),
                };

                return $"global::PolyType.Abstractions.ParameterKind.{identifier}";
            }
        }

        writer.Indentation--;
        writer.WriteLine("};");

        foreach (ParameterShapeModel parameter in allParams)
        {
            string? attributeFactoryName = GetAttributeFactoryName(parameter);
            if (attributeFactoryName is not null)
            {
                writer.WriteLine();
                FormatAttributesFactory(writer, attributeFactoryName, parameter.Attributes);
            }
        }

        string? GetAttributeFactoryName(ParameterShapeModel parameter) =>
            parameter.Attributes.Length > 0
            ? $"__CreateConstructorParameterAttributes_{type.SourceIdentifier}_{parameter.Position}"
            : null;
    }

    private static string FormatDefaultValueExpr(ParameterShapeModel parameter)
    {
        return parameter switch
        {
            { DefaultValueExpr: string defaultValueExpr } => defaultValueExpr,
            { ParameterType.IsValueType: true } => "default",
            _ => "default!",
        };
    }

    private static string FormatConstructorArgumentStateFQN(ObjectShapeModel type, ConstructorShapeModel constructorModel)
    {
        string typeParameter = FormatArgumentStateTypeTypeParameter();
        return constructorModel.ArgumentStateType switch
        {
            ArgumentStateType.EmptyArgumentState => $"global::PolyType.SourceGenModel.EmptyArgumentState",
            ArgumentStateType.SmallArgumentState => $"global::PolyType.SourceGenModel.SmallArgumentState<{typeParameter}>",
            ArgumentStateType.LargeArgumentState => $"global::PolyType.SourceGenModel.LargeArgumentState<{typeParameter}>",
            _ => throw new InvalidOperationException(constructorModel.ArgumentStateType.ToString()),
        };

        string FormatArgumentStateTypeTypeParameter()
        {
            if (type.IsValueTupleType && constructorModel.TotalArity > 1)
            {
                // For value tuple types, just use the type as the argument state.
                return constructorModel.DeclaringType.FullyQualifiedName;
            }

            return constructorModel.TotalArity switch
            {
                1 => constructorModel.GetAllParameters().First().ParameterType.FullyQualifiedName,
                _ => FormatTupleType(constructorModel.GetAllParameters().Select(p => p.ParameterType.FullyQualifiedName)),
            };

            static string FormatTupleType(IEnumerable<string> parameterTypes)
                => $"({string.Join(", ", parameterTypes)})";
        }
    }

    private static string GetConstructorAccessorName(ObjectShapeModel declaringType)
    {
        return $"__CtorAccessor_{declaringType.SourceIdentifier}";
    }

    private static void FormatConstructorAccessor(SourceWriter writer, ObjectShapeModel declaringType, ConstructorShapeModel constructorModel)
    {
        Debug.Assert(!constructorModel.IsAccessible);

        StringBuilder parameterSignature = new();
        foreach (ParameterShapeModel parameter in constructorModel.Parameters)
        {
            string refPrefix = parameter.RefKind switch
            {
                RefKind.Ref or RefReadOnlyParameter => "ref ",
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

        string accessorName = GetConstructorAccessorName(declaringType);

        if (!constructorModel.CanUseUnsafeAccessors)
        {
            // Emit a reflection-based workaround.
            string parameterTypes = constructorModel.Parameters.Length == 0
                ? "global::System.Type.EmptyTypes"
                : $$"""new global::System.Type[] { {{string.Join(", ", constructorModel.Parameters.Select(FormatParameterTypeExpr))}} }""";

            writer.WriteLine($$"""
                private static global::System.Reflection.ConstructorInfo? __s_{{accessorName}}_CtorInfo;
                private static {{constructorModel.DeclaringType.FullyQualifiedName}} {{accessorName}}({{parameterSignature}})
                {
                    global::System.Reflection.ConstructorInfo ctorInfo = __s_{{accessorName}}_CtorInfo ??= typeof({{constructorModel.DeclaringType.FullyQualifiedName}}).GetConstructor({{InstanceBindingFlagsConstMember}}, null, {{parameterTypes}}, null)!;
                    object?[] paramArray = new object?[] { {{string.Join(", ", constructorModel.Parameters.Select(p => p.Name))}} };
                    return ({{constructorModel.DeclaringType.FullyQualifiedName}})ctorInfo.Invoke(paramArray);
                }
                """);

            return;
        }

        writer.WriteLine($"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Constructor)]
            private static extern {constructorModel.DeclaringType.FullyQualifiedName} {accessorName}({parameterSignature});
            """);
    }

    private static void FormatRequiredParametersMaskField(
        SourceWriter writer,
        string fieldName,
        int totalArity,
        ArgumentStateType argumentStateType,
        IEnumerable<ParameterShapeModel> parameters)
    {
        Debug.Assert(argumentStateType is ArgumentStateType.SmallArgumentState or ArgumentStateType.LargeArgumentState);
        Debug.Assert(totalArity > 0 && totalArity == parameters.Count());

        string requiredMembersMaskType = argumentStateType switch
        {
            ArgumentStateType.SmallArgumentState => "const ulong",
            ArgumentStateType.LargeArgumentState => "static readonly global::PolyType.SourceGenModel.ValueBitArray",
            _ => throw new InvalidOperationException(argumentStateType.ToString()),
        };

        string requiredMembersMaskExpr = FormatRequiredParametersMaskExpr();
        writer.WriteLine($"private {requiredMembersMaskType} {fieldName} = {requiredMembersMaskExpr};");

        string FormatRequiredParametersMaskExpr()
        {
            if (argumentStateType is ArgumentStateType.SmallArgumentState)
            {
                int i = 0;
                ulong mask = 0UL;
                foreach (ParameterShapeModel parameter in parameters)
                {
                    if (parameter.IsRequired)
                    {
                        mask |= 1UL << i;
                    }

                    i++;
                }

                return $"0x{mask:X}UL";
            }
            else
            {
                ValueBitArray requiredMask = new(totalArity);
                int i = 0;
                foreach (ParameterShapeModel parameter in parameters)
                {
                    if (parameter.IsRequired)
                    {
                        requiredMask[i] = true;
                    }

                    i++;
                }

                byte[] bytes = requiredMask.Bytes.ToArray();
                return $"new(new byte[] {{ {string.Join(", ", bytes.Select(i => $"0x{i:X2}"))} }}, {requiredMask.Length})";
            }
        }
    }

    private static string FormatParameterTypeExpr(ParameterShapeModel parameter)
    {
        return parameter.RefKind is RefKind.None
            ? $"typeof({parameter.ParameterType.FullyQualifiedName})"
            : $"typeof({parameter.ParameterType.FullyQualifiedName}).MakeByRefType()";
    }

}
