using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;
using System.Diagnostics;
using System.Text;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private static string? CreateMethodsFactoryName(TypeShapeModel declaringType)
    {
        if (declaringType.Methods.Length == 0)
        {
            return null;
        }

        return $"__CreateMethods_{declaringType.SourceIdentifier}";
    }

    private void FormatMethodsFactory(SourceWriter writer, string methodName, TypeShapeModel declaringType)
    {
        Debug.Assert(declaringType.Methods.Length > 0);
        List<string> methodNames = [];

        writer.WriteLine($"private global::PolyType.Abstractions.IMethodShape[] {methodName}() => new global::PolyType.Abstractions.IMethodShape[]");
        writer.WriteLine('{');
        writer.Indentation++;
        foreach (MethodShapeModel method in declaringType.Methods)
        {
            string methodShapeFactoryName = $"__CreateMethod_{declaringType.SourceIdentifier}_{method.Position}_{method.UnderlyingMethodName}";
            writer.WriteLine($"{methodShapeFactoryName}(),");
            methodNames.Add(methodShapeFactoryName);
        }
        writer.Indentation--;
        writer.WriteLine("};");

        int i = 0;
        foreach (MethodShapeModel method in declaringType.Methods)
        {
            writer.WriteLine();
            FormatMethodFactory(writer, methodNames[i++], declaringType, method);
        }

        foreach (MethodShapeModel method in declaringType.Methods)
        {
            if (!method.IsAccessible)
            {
                writer.WriteLine();
                FormatMethodAccessor(writer, declaringType, method);
            }
        }
    }

    private void FormatMethodFactory(SourceWriter writer, string methodName, TypeShapeModel declaringType, MethodShapeModel method)
    {
        string methodArgumentStateFQN = FormatMethodArgumentStateFQN(method);
        string? methodParameterFactoryName = method.Parameters.Length > 0 ? $"__CreateMethodParameters_{declaringType.SourceIdentifier}_{method.Position}" : null;

        writer.WriteLine($"private global::PolyType.Abstractions.IMethodShape {methodName}()");
        writer.WriteLine('{');
        writer.Indentation++;

        writer.WriteLine($$"""
            return new global::PolyType.SourceGenModel.SourceGenMethodShape<{{declaringType.Type.FullyQualifiedName}}, {{methodArgumentStateFQN}}, {{method.ReturnType.FullyQualifiedName}}>
            {
                Name = {{FormatStringLiteral(method.Name)}},
                IsPublic = {{FormatBool(method.IsPublic)}},
                IsStatic = {{FormatBool(method.IsStatic)}},
                IsVoidLike = {{FormatBool(IsVoidLike(method))}},
                IsAsync = {{FormatBool(IsAsync(method))}},
                DeclaringType = {{declaringType.SourceIdentifier}},
                ReturnType = {{GetShapeModel(method.ReturnType).SourceIdentifier}},
                CreateParametersFunc = {{FormatNull(methodParameterFactoryName)}},
                AttributeProviderFunc = {{FormatAttributeProviderFunc(declaringType, method)}},
                ArgumentStateConstructor = {{FormatArgumentStateConstructor(declaringType, method, methodArgumentStateFQN)}},
                MethodInvoker = {{FormatMethodInvoker(declaringType, method, methodArgumentStateFQN)}},
            };
            """, trimNullAssignmentLines: true);

        writer.Indentation--;
        writer.WriteLine('}');
        
        if (methodParameterFactoryName != null)
        {
            writer.WriteLine();
            FormatMethodParameterFactory(writer, declaringType, methodParameterFactoryName, method, methodArgumentStateFQN);
        }

        if (FormatRequiredParametersMaskFieldName(declaringType, method) is { } requiredParametersMaskFieldName)
        {
            writer.WriteLine();
            FormatRequiredParametersMaskField(
                writer,
                requiredParametersMaskFieldName,
                method.Parameters.Length,
                method.ArgumentStateType,
                method.Parameters);
        }

        static bool IsVoidLike(MethodShapeModel method)
        {
            return method.ReturnTypeKind is MethodReturnTypeKind.Void 
                or MethodReturnTypeKind.Task 
                or MethodReturnTypeKind.ValueTask;
        }

        static bool IsAsync(MethodShapeModel method)
        {
            return method.ReturnTypeKind is MethodReturnTypeKind.Task 
                or MethodReturnTypeKind.TaskOfT
                or MethodReturnTypeKind.ValueTask 
                or MethodReturnTypeKind.ValueTaskOfT;
        }

        static string FormatAttributeProviderFunc(TypeShapeModel declaringType, MethodShapeModel method)
        {
            string parameterTypes = method.Parameters.Length == 0
                ? "global::System.Type.EmptyTypes"
                : $$"""new global::System.Type[] { {{string.Join(", ", method.Parameters.Select(FormatParameterTypeExpr))}} }""";

            return $"static () => typeof({method.DeclaringType.FullyQualifiedName}).GetMethod({FormatStringLiteral(method.UnderlyingMethodName)}, {AllBindingFlagsConstMember}, null, {parameterTypes}, null)";
        }

        static string FormatArgumentStateConstructor(TypeShapeModel declaringType, MethodShapeModel method, string methodArgumentStateFQN)
        {
            if (method.Parameters.Length == 0)
            {
                Debug.Assert(methodArgumentStateFQN == "global::PolyType.SourceGenModel.EmptyArgumentState");
                return "static () => global::PolyType.SourceGenModel.EmptyArgumentState.Instance";
            }

            string stateValueExpr = method.Parameters.Length switch
            {
                1 => FormatDefaultValueExpr(method.Parameters[0]),
                _ => FormatTupleConstructor(method.Parameters.Select(FormatDefaultValueExpr)),
            };

            string requiredParametersMaskFieldName = FormatRequiredParametersMaskFieldName(declaringType, method)!;
            return $"static () => new {methodArgumentStateFQN}({stateValueExpr}, count: {method.Parameters.Length}, requiredArgumentsMask: {requiredParametersMaskFieldName})";
            
            static string FormatTupleConstructor(IEnumerable<string> parameters)
                => $"({string.Join(", ", parameters)})";
        }

        static string FormatMethodInvoker(TypeShapeModel declaringType, MethodShapeModel method, string methodArgumentStateFQN)
        {
            return $"static (ref {declaringType.Type.FullyQualifiedName}{(declaringType.Type.IsValueType ? "" : "?")} target, ref {methodArgumentStateFQN} state) => {FormatMethodInvocationExpr(declaringType, method, "target", "state")}";
        }

        static string FormatMethodInvocationExpr(TypeShapeModel declaringType, MethodShapeModel method, string targetVar, string stateVar)
        {
            string parametersExpression = method.Parameters.Length switch
            {
                0 => "",
                1 => FormatMethodParameterExpr(method.Parameters[0], isSingleParameter: true),
                _ => string.Join(", ", method.Parameters.Select(p => FormatMethodParameterExpr(p, isSingleParameter: false)))
            };

            string refPrefix = method.DeclaringType.IsValueType ? "ref " : "";
            string targetExpr = method.RequiresDisambiguation ? $"(({method.DeclaringType.FullyQualifiedName}){targetVar}!)" : $"{targetVar}!";
            string invokeExpr = method switch
            {
                { IsStatic: true, IsAccessible: true } => $"{declaringType.Type.FullyQualifiedName}.{method.UnderlyingMethodName}({parametersExpression})",
                { IsStatic: true, IsAccessible: false } => $"{GetMethodAccessorName(declaringType, method)}({parametersExpression})",
                { IsStatic: false, IsAccessible: true } => $"{targetExpr}.{method.UnderlyingMethodName}({parametersExpression})",
                { IsStatic: false, IsAccessible: false } when method.Parameters is [] => $"{GetMethodAccessorName(declaringType, method)}({refPrefix}{targetExpr})",
                { IsStatic: false, IsAccessible: false } => $"{GetMethodAccessorName(declaringType, method)}({refPrefix}{targetExpr}, {parametersExpression})",
            };

            // Handle async methods and void returns
            return method.ReturnTypeKind switch
            {
                MethodReturnTypeKind.Void => $"{{ {invokeExpr}; return new(global::PolyType.Abstractions.Unit.Value); }}",
                MethodReturnTypeKind.Task => $"{{ var task = {invokeExpr}; return global::PolyType.Abstractions.Unit.FromTaskAsync(task); }}",
                MethodReturnTypeKind.ValueTask => $"{{ var task = {invokeExpr}; return global::PolyType.Abstractions.Unit.FromValueTaskAsync(task); }}",
                MethodReturnTypeKind.TaskOfT => $"{{ var task = {invokeExpr}; return new(task); }}",
                MethodReturnTypeKind.ValueTaskOfT => invokeExpr,
                _ => $"{{ var result = {invokeExpr}; return new(result); }}",
            };

            static string FormatMethodParameterExpr(ParameterShapeModel parameter, bool isSingleParameter)
            {
                // Reserved for cases where we have Nullable<T> method parameters with [DisallowNull] annotation.
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
                    ? $"{refPrefix}state.Arguments{(requiresSuppression ? "!" : "")}"
                    : $"{refPrefix}state.Arguments.Item{parameter.Position + 1}{(requiresSuppression ? "!" : "")}";
            }
        }

        static string? FormatRequiredParametersMaskFieldName(TypeShapeModel declaringType, MethodShapeModel method)
        {
            if (method.Parameters.Length == 0)
            {
                return null;
            }

            return $"__RequiredMembersMask_{declaringType.SourceIdentifier}_{method.Position}_{method.UnderlyingMethodName}";
        }
    }

    private void FormatMethodParameterFactory(SourceWriter writer, TypeShapeModel declaringType, string methodName, MethodShapeModel method, string methodArgumentStateFQN)
    {
        writer.WriteLine($"private global::PolyType.Abstractions.IParameterShape[] {methodName}() => new global::PolyType.Abstractions.IParameterShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        for (int i = 0; i < method.Parameters.Length; i++)
        {
            ParameterShapeModel parameter = method.Parameters[i];
            if (i > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($$"""
                new global::PolyType.SourceGenModel.SourceGenParameterShape<{{methodArgumentStateFQN}}, {{parameter.ParameterType.FullyQualifiedName}}>
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
                    Getter = static (ref {{methodArgumentStateFQN}} state) => {{FormatGetterBody(method, parameter)}},
                    Setter = static (ref {{methodArgumentStateFQN}} state, {{parameter.ParameterType.FullyQualifiedName}} value) => {{FormatSetterBody(method, parameter)}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(declaringType, method, parameter)}},
                },
                """, trimNullAssignmentLines: true);

            static string FormatAttributeProviderFunc(TypeShapeModel declaringType, MethodShapeModel method, ParameterShapeModel parameter)
            {
                string parameterTypes = method.Parameters.Length == 0
                    ? "global::System.Type.EmptyTypes"
                    : $$"""new global::System.Type[] { {{string.Join(", ", method.Parameters.Select(FormatParameterTypeExpr))}} }""";

                return $"static () => typeof({method.DeclaringType.FullyQualifiedName}).GetMethod({FormatStringLiteral(method.UnderlyingMethodName)}, {AllBindingFlagsConstMember}, null, {parameterTypes}, null)?.GetParameters()[{parameter.Position}]";
            }

            static string FormatGetterBody(MethodShapeModel method, ParameterShapeModel parameter)
            {
                // Suppress non-nullable Nullable<T> property getters (i.e. setters with [DisallowNull] annotation)
                bool suppressGetter = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is
                {
                    ParameterType.SpecialType: SpecialType.System_Nullable_T,
                    IsNonNullable: true,
                };

                return method.Parameters.Length switch
                {
                    1 => $"state.Arguments{(suppressGetter ? "!" : "")}",
                    _ => $"state.Arguments.Item{parameter.Position + 1}{(suppressGetter ? "!" : "")}",
                };
            }

            static string FormatSetterBody(MethodShapeModel method, ParameterShapeModel parameter)
            {
                // Suppress non-nullable Nullable<T> parameter setters (i.e. setters with [DisallowNull] annotation)
                bool suppressSetter = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is 
                { 
                    ParameterType.SpecialType: SpecialType.System_Nullable_T,
                    IsNonNullable: true,
                };
                
                string assignValueExpr = method.Parameters.Length switch
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
    }

    private static string FormatMethodArgumentStateFQN(MethodShapeModel method)
    {
        string typeParameter = FormatArgumentStateTypeTypeParameter();
        return method.ArgumentStateType switch
        {
            ArgumentStateType.EmptyArgumentState => $"global::PolyType.SourceGenModel.EmptyArgumentState",
            ArgumentStateType.SmallArgumentState => $"global::PolyType.SourceGenModel.SmallArgumentState<{typeParameter}>",
            ArgumentStateType.LargeArgumentState => $"global::PolyType.SourceGenModel.LargeArgumentState<{typeParameter}>",
            _ => throw new InvalidOperationException(method.ArgumentStateType.ToString()),
        };

        string FormatArgumentStateTypeTypeParameter()
        {
            return method.Parameters.Length switch
            {
                1 => method.Parameters[0].ParameterType.FullyQualifiedName,
                _ => FormatTupleType(method.Parameters.Select(p => p.ParameterType.FullyQualifiedName)),
            };

            static string FormatTupleType(IEnumerable<string> parameterTypes)
                => $"({string.Join(", ", parameterTypes)})";
        }
    }

    private static string GetMethodAccessorName(TypeShapeModel declaringType, MethodShapeModel method)
    {
        return $"__MethodAccessor_{declaringType.SourceIdentifier}_{method.Position}_{method.UnderlyingMethodName}";
    }

    private static void FormatMethodAccessor(SourceWriter writer, TypeShapeModel declaringType, MethodShapeModel method)
    {
        Debug.Assert(!method.IsAccessible);

        StringBuilder parameterSignature = new();
        if (!method.IsStatic)
        {
            string refPrefix = method.DeclaringType.IsValueType ? "ref " : "";
            parameterSignature.Append($"{refPrefix}{method.DeclaringType.FullyQualifiedName} target, ");
        }

        foreach (ParameterShapeModel parameter in method.Parameters)
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
            parameterSignature.Length -= 2; // Trim the last comma.
        }

        string allParameters = parameterSignature.ToString();
        string accessorName = GetMethodAccessorName(declaringType, method);

        if (!method.CanUseUnsafeAccessors)
        {
            // Emit a reflection-based workaround.
            string parameterTypes = method.Parameters.Length == 0
                ? "global::System.Type.EmptyTypes"
                : $$"""new global::System.Type[] { {{string.Join(", ", method.Parameters.Select(FormatParameterTypeExpr))}} }""";

            writer.WriteLine($$"""
                private static global::System.Reflection.MethodInfo? __s_{{accessorName}}_MethodInfo;
                private static {{method.UnderlyingReturnType.FullyQualifiedName}} {{accessorName}}({{allParameters}})
                {
                    global::System.Reflection.MethodInfo methodInfo = __s_{{accessorName}}_MethodInfo ??= typeof({{method.DeclaringType}}).GetMethod({{FormatStringLiteral(method.UnderlyingMethodName)}}, {{AllBindingFlagsConstMember}}, null, {{parameterTypes}}, null)!;
                    object?[] paramArray = new object?[] { {{string.Join(", ", method.Parameters.Select(p => p.Name))}} };
                    {{(method.ReturnTypeKind is MethodReturnTypeKind.Void
                        ? CreateInvokeMethodExpr()
                        : $"return ({method.UnderlyingReturnType.FullyQualifiedName}){CreateInvokeMethodExpr()}!;")}};
                }
                """);

            string CreateInvokeMethodExpr() => $"methodInfo.Invoke({(method.IsStatic ? "null" : "target")}, paramArray)";
            return;
        }

        Debug.Assert(!method.IsStatic, "Remove once https://github.com/eiriktsarpalis/PolyType/issues/220 is implemented");
        string methodRefPrefix = method.ReturnsByRef ? "ref " : "";
        writer.WriteLine($"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(method.UnderlyingMethodName)})]
            private static extern {methodRefPrefix}{method.UnderlyingReturnType.FullyQualifiedName} {accessorName}({allParameters});
            """);
    }
}