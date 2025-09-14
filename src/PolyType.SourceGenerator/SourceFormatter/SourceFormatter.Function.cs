using Microsoft.CodeAnalysis;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;
using System.Diagnostics;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatFunctionTypeShapeFactory(SourceWriter writer, string methodName, FunctionShapeModel functionShapeModel)
    {
        string functionArgumentStateFQN = FormatFunctionArgumentStateFQN(functionShapeModel);
        string? functionParameterFactoryName = functionShapeModel.Parameters.Length > 0 ? $"__CreateFunctionParameters_{functionShapeModel.SourceIdentifier}" : null;
        string? requiredParametersMaskFieldName = FormatRequiredParametersMaskFieldName(functionShapeModel);
        string? associatedTypesFactoryMethodName = GetAssociatedTypesFactoryName(functionShapeModel);

        writer.WriteLine($$"""
            private global::PolyType.ITypeShape<{{functionShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenFunctionTypeShape<{{functionShapeModel.Type.FullyQualifiedName}}, {{functionArgumentStateFQN}}, {{functionShapeModel.ReturnType.FullyQualifiedName}}>
                {
                    IsVoidLike = {{FormatBool(IsVoidLike(functionShapeModel))}},
                    IsAsync = {{FormatBool(IsAsync(functionShapeModel))}},
                    ReturnType = {{GetShapeModel(functionShapeModel.ReturnType).SourceIdentifier}},
                    CreateParametersFunc = {{FormatNull(functionParameterFactoryName)}},
                    ArgumentStateConstructor = {{FormatArgumentStateConstructor(functionShapeModel, functionArgumentStateFQN, requiredParametersMaskFieldName)}},
                    FunctionInvoker = {{FormatFunctionInvoker(functionShapeModel, functionArgumentStateFQN)}},
                    FromDelegateFunc = {{FormatNull(FormatFromDelegateFunc(functionShapeModel, functionArgumentStateFQN, requiredParametersMaskFieldName, requireAsync: false))}},
                    FromAsyncDelegateFunc = {{FormatNull(FormatFromDelegateFunc(functionShapeModel, functionArgumentStateFQN, requiredParametersMaskFieldName, requireAsync: true))}},
                    GetAssociatedTypeShapeFunc = {{FormatNull(associatedTypesFactoryMethodName)}},
                    Provider = this,
                };
            }
            """, trimNullAssignmentLines: true);

        if (functionParameterFactoryName != null)
        {
            writer.WriteLine();
            FormatFunctionParameterFactory(writer, functionParameterFactoryName, functionShapeModel, functionArgumentStateFQN);
        }

        if (associatedTypesFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatAssociatedTypesFactory(writer, functionShapeModel, associatedTypesFactoryMethodName);
        }

        if (requiredParametersMaskFieldName is not null)
        {
            writer.WriteLine();
            FormatRequiredParametersMaskField(
                writer,
                requiredParametersMaskFieldName,
                functionShapeModel.Parameters.Length,
                functionShapeModel.ArgumentStateType,
                functionShapeModel.Parameters);
        }

        static bool IsVoidLike(FunctionShapeModel method)
        {
            return method.ReturnTypeKind is MethodReturnTypeKind.Void
                or MethodReturnTypeKind.Task
                or MethodReturnTypeKind.ValueTask
                or FSharpFunctionDataModel.FSharpUnitReturnTypeKind;
        }

        static bool IsAsync(FunctionShapeModel method)
        {
            return method.ReturnTypeKind is MethodReturnTypeKind.Task
                or MethodReturnTypeKind.TaskOfT
                or MethodReturnTypeKind.ValueTask
                or MethodReturnTypeKind.ValueTaskOfT;
        }

        static string FormatArgumentStateConstructor(FunctionShapeModel functionShapeModel, string functionArgumentStateFQN, string? requiredParametersMaskFieldName)
        {
            if (functionShapeModel.Parameters.Length == 0)
            {
                Debug.Assert(functionArgumentStateFQN == "global::PolyType.SourceGenModel.EmptyArgumentState");
                return "static () => global::PolyType.SourceGenModel.EmptyArgumentState.Instance";
            }

            DebugExt.Assert(requiredParametersMaskFieldName is not null);
            string stateValueExpr = functionShapeModel.Parameters.Length switch
            {
                1 => FormatDefaultValueExpr(functionShapeModel.Parameters[0]),
                _ => FormatTupleConstructor(functionShapeModel.Parameters.Select(FormatDefaultValueExpr)),
            };

            return $"static () => new {functionArgumentStateFQN}({stateValueExpr}, count: {functionShapeModel.Parameters.Length}, requiredArgumentsMask: {requiredParametersMaskFieldName})";

            static string FormatTupleConstructor(IEnumerable<string> parameters)
                => $"({string.Join(", ", parameters)})";
        }

        static string FormatFunctionInvoker(FunctionShapeModel functionShapeModel, string functionArgumentStateFQN)
        {
            return $"static (ref {functionShapeModel.Type.FullyQualifiedName} target, ref {functionArgumentStateFQN} state) => {FormatFunctionInvocationExpr(functionShapeModel, "target", "state")}";
        }

        static string FormatFunctionInvocationExpr(FunctionShapeModel functionShapeModel, string targetVar, string stateVar)
        {
            string invokeExpr;

            if (functionShapeModel.IsFsharpFunc)
            {
                // Format a curried invocation chain for F# functions
                string parametersExpression = functionShapeModel.Parameters.Length switch
                {
                    0 => $".Invoke(null!)", // F# function accepting a single unit parameter
                    1 => $".Invoke({FormatFunctionParameterExpr(functionShapeModel.Parameters[0], isSingleParameter: true)})",
                    _ => string.Join("", functionShapeModel.Parameters.Select(p => $".Invoke({FormatFunctionParameterExpr(p, isSingleParameter: false)})")),
                };

                invokeExpr = $"{targetVar}{parametersExpression}";
            }
            else
            {
                string parametersExpression = functionShapeModel.Parameters.Length switch
                {
                    0 => "",
                    1 => FormatFunctionParameterExpr(functionShapeModel.Parameters[0], isSingleParameter: true),
                    _ => string.Join(", ", functionShapeModel.Parameters.Select(p => FormatFunctionParameterExpr(p, isSingleParameter: false)))
                };

                invokeExpr = $"{targetVar}({parametersExpression})";
            }

            // Handle async methods and void returns
            return functionShapeModel.ReturnTypeKind switch
            {
                MethodReturnTypeKind.Void => $"{{ {invokeExpr}; return new(global::PolyType.Abstractions.Unit.Value); }}",
                MethodReturnTypeKind.Task => $"{{ var task = {invokeExpr}; return global::PolyType.Abstractions.Unit.FromTaskAsync(task); }}",
                MethodReturnTypeKind.ValueTask => $"{{ var task = {invokeExpr}; return global::PolyType.Abstractions.Unit.FromValueTaskAsync(task); }}",
                MethodReturnTypeKind.TaskOfT => $"{{ var task = {invokeExpr}; return new(task); }}",
                MethodReturnTypeKind.ValueTaskOfT => invokeExpr,
                FSharpFunctionDataModel.FSharpUnitReturnTypeKind => $"{{ var _ = {invokeExpr}; return new(global::PolyType.Abstractions.Unit.Value); }}",
                _ => $"{{ var result = {invokeExpr}; return new(result); }}",
            };

            static string FormatFunctionParameterExpr(ParameterShapeModel parameter, bool isSingleParameter)
            {
                // Reserved for cases where we have Nullable<T> method parameters with [DisallowNull] annotation.
                bool requiresSuppression = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is
                {
                    ParameterType.SpecialType: SpecialType.System_Nullable_T,
                    IsNonNullable: true
                };

                string refPrefix = FormatRefPrefix(parameter);

                return isSingleParameter
                    ? $"{refPrefix}state.Arguments{(requiresSuppression ? "!" : "")}"
                    : $"{refPrefix}state.Arguments.Item{parameter.Position + 1}{(requiresSuppression ? "!" : "")}";
            }
        }

        static string? FormatFromDelegateFunc(FunctionShapeModel functionShapeModel, string functionArgumentStateFQN, string? requiredParametersMaskFieldName, bool requireAsync)
        {
            if (IsAsync(functionShapeModel) != requireAsync)
            {
                return null;
            }

            if (functionShapeModel.IsFsharpFunc)
            {
                return """static _ => throw new global::System.NotSupportedException("F# function creation from delegates is currently not supported.")""";
            }

            string delegateSignature = string.Join(", ", functionShapeModel.Parameters
                .Select(parameter => $"{FormatRefPrefix(parameter)}{parameter.ParameterType.FullyQualifiedName}{GetNullableSuffix(parameter)} {parameter.Name}"));

            string argumentStateCtorExpr = functionShapeModel.Parameters switch
            {
                [] => "global::PolyType.SourceGenModel.EmptyArgumentState.Instance",
                [var p] => $"new({p.Name}{GetSuppressionSuffix(p)}, count: 1, requiredArgumentsMask: {requiredParametersMaskFieldName}, markAllArgumentsSet: true)",
                _ => $"new(({string.Join(", ", functionShapeModel.Parameters.Select(p => p.Name + GetSuppressionSuffix(p)))}), count: {functionShapeModel.Parameters.Length}, requiredArgumentsMask: {requiredParametersMaskFieldName}, markAllArgumentsSet: true)",
            };

            const string innerFuncVar = "innerFunc";
            const string stateVar = "state";
            const string invokeInnerExpr = $"{innerFuncVar}(ref {stateVar})";
            string tailExpr = functionShapeModel.ReturnTypeKind switch
            {
                MethodReturnTypeKind.Void => invokeInnerExpr,
                MethodReturnTypeKind.ValueTaskOfT or MethodReturnTypeKind.Unrecognized => $"return {invokeInnerExpr}",
                MethodReturnTypeKind.Task or MethodReturnTypeKind.TaskOfT => $"return {invokeInnerExpr}.AsTask()",
                MethodReturnTypeKind.ValueTask => $"return global::PolyType.Abstractions.Unit.ToValueTaskAsync({invokeInnerExpr})",
                _ => throw new InvalidOperationException(),
            };

            return $$"""static {{innerFuncVar}} => ({{delegateSignature}}) => { {{functionArgumentStateFQN}} {{stateVar}} = {{argumentStateCtorExpr}}; {{tailExpr}}; }""";

            static string GetNullableSuffix(ParameterShapeModel parameter) => parameter.NullableAnnotation is NullableAnnotation.Annotated ? "?" : "";
            static string GetSuppressionSuffix(ParameterShapeModel parameter) => parameter.ParameterTypeContainsNullabilityAnnotations ? "!" : "";
        }

        static string? FormatRequiredParametersMaskFieldName(FunctionShapeModel functionShapeModel)
        {
            if (functionShapeModel.Parameters.Length == 0)
            {
                return null;
            }

            return $"__RequiredMembersMask_{functionShapeModel.SourceIdentifier}";
        }
    }

    private static string FormatFunctionArgumentStateFQN(FunctionShapeModel method)
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

    private void FormatFunctionParameterFactory(SourceWriter writer, string methodName, FunctionShapeModel functionShapeModel, string functionArgumentStateFQN)
    {
        writer.WriteLine($"private global::PolyType.Abstractions.IParameterShape[] {methodName}() => new global::PolyType.Abstractions.IParameterShape[]");
        writer.WriteLine('{');
        writer.Indentation++;

        for (int i = 0; i < functionShapeModel.Parameters.Length; i++)
        {
            ParameterShapeModel parameter = functionShapeModel.Parameters[i];
            if (i > 0)
            {
                writer.WriteLine();
            }

            writer.WriteLine($$"""
                new global::PolyType.SourceGenModel.SourceGenParameterShape<{{functionArgumentStateFQN}}, {{parameter.ParameterType.FullyQualifiedName}}>
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
                    Getter = static (ref {{functionArgumentStateFQN}} state) => {{FormatGetterBody(functionShapeModel, parameter)}},
                    Setter = static (ref {{functionArgumentStateFQN}} state, {{parameter.ParameterType.FullyQualifiedName}} value) => {{FormatSetterBody(functionShapeModel, parameter)}},
                    AttributeProviderFunc = {{FormatAttributeProviderFunc(functionShapeModel, parameter)}},
                },
                """, trimNullAssignmentLines: true);

            static string FormatAttributeProviderFunc(FunctionShapeModel functionShapeModel, ParameterShapeModel parameter)
            {
                if (functionShapeModel.IsFsharpFunc)
                {
                    string nestedFunctionGetter = string.Join("", Enumerable.Range(0, parameter.Position).Select(_ => ".GetGenericArguments()[1]"));
                    return $"static () => typeof({functionShapeModel.Type.FullyQualifiedName}){nestedFunctionGetter}.GetMethod(\"Invoke\")!.GetParameters()[0]";
                }

                string parameterTypes = functionShapeModel.Parameters.Length == 0
                    ? "global::System.Type.EmptyTypes"
                    : $$"""new global::System.Type[] { {{string.Join(", ", functionShapeModel.Parameters.Select(FormatParameterTypeExpr))}} }""";

                return $"static () => typeof({functionShapeModel.Type.FullyQualifiedName}).GetMethod(\"Invoke\")?.GetParameters()[{parameter.Position}]";
            }

            static string FormatGetterBody(FunctionShapeModel functionShapeModel, ParameterShapeModel parameter)
            {
                // Suppress non-nullable Nullable<T> property getters (i.e. setters with [DisallowNull] annotation)
                bool suppressGetter = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is
                {
                    ParameterType.SpecialType: SpecialType.System_Nullable_T,
                    IsNonNullable: true,
                };

                return functionShapeModel.Parameters.Length switch
                {
                    1 => $"state.Arguments{(suppressGetter ? "!" : "")}",
                    _ => $"state.Arguments.Item{parameter.Position + 1}{(suppressGetter ? "!" : "")}",
                };
            }

            static string FormatSetterBody(FunctionShapeModel functionShapeModel, ParameterShapeModel parameter)
            {
                // Suppress non-nullable Nullable<T> parameter setters (i.e. setters with [DisallowNull] annotation)
                bool suppressSetter = parameter.ParameterTypeContainsNullabilityAnnotations || parameter is
                {
                    ParameterType.SpecialType: SpecialType.System_Nullable_T,
                    IsNonNullable: true,
                };

                string assignValueExpr = functionShapeModel.Parameters.Length switch
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

    private static string FormatRefPrefix(ParameterShapeModel parameter)
    {
        return parameter.RefKind switch
        {
            RefKind.Ref or RefReadOnlyParameter => "ref ",
            RefKind.In => "in ",
            RefKind.Out => "out ",
            _ => ""
        };
    }
}
