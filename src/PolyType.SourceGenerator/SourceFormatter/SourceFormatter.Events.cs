using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;
using System.Diagnostics;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private static string? CreateEventsFactoryName(TypeShapeModel declaringType)
    {
        if (declaringType.Events.Length == 0)
        {
            return null;
        }

        return $"__CreateEvents_{declaringType.SourceIdentifier}";
    }

    private void FormatEventsFactory(SourceWriter writer, string methodName, TypeShapeModel declaringType)
    {
        Debug.Assert(declaringType.Events.Length > 0);
        List<string> eventNames = [];

        writer.WriteLine($"private global::PolyType.Abstractions.IEventShape[] {methodName}() => new global::PolyType.Abstractions.IEventShape[]");
        writer.WriteLine('{');
        writer.Indentation++;
        foreach (EventShapeModel eventModel in declaringType.Events)
        {
            string nullableSuffix = declaringType.Type.IsValueType ? "" : "?";
            writer.WriteLine($$"""
                new global::PolyType.SourceGenModel.SourceGenEventShape<{{declaringType.Type.FullyQualifiedName}},{{eventModel.HandlerType.FullyQualifiedName}}>
                {
                    Name = {{FormatStringLiteral(eventModel.Name)}},
                    DeclaringType = {{declaringType.SourceIdentifier}},
                    HandlerType = (global::PolyType.Abstractions.IFunctionTypeShape){{GetShapeModel(eventModel.HandlerType).SourceIdentifier}},
                    IsStatic = {{FormatBool(eventModel.IsStatic)}},
                    IsPublic = {{FormatBool(eventModel.IsPublic)}},
                    AddHandler = static (ref {{declaringType.Type.FullyQualifiedName}}{{nullableSuffix}} obj, {{eventModel.HandlerType.FullyQualifiedName}} handler) => {{FormatHandlerExpr("obj", "handler", declaringType, eventModel, isAdd: true)}},
                    RemoveHandler = static (ref {{declaringType.Type.FullyQualifiedName}}{{nullableSuffix}} obj, {{eventModel.HandlerType.FullyQualifiedName}} handler) => {{FormatHandlerExpr("obj", "handler", declaringType, eventModel, isAdd: false)}},
                    AttributeProviderFunc = static () => typeof({{eventModel.DeclaringType.FullyQualifiedName}}).GetEvent({{FormatStringLiteral(eventModel.UnderlyingMemberName)}}, {{AllBindingFlagsConstMember}}),
                },
                """, trimNullAssignmentLines: true);
        }

        writer.Indentation--;
        writer.WriteLine("};");

        foreach (EventShapeModel eventModel in declaringType.Events)
        {
            if (!eventModel.IsAccessible)
            {
                writer.WriteLine();
                FormatEventAccessor(writer, declaringType, eventModel, isAdd: true);

                writer.WriteLine();
                FormatEventAccessor(writer, declaringType, eventModel, isAdd: false);
            }
        }

        static string FormatHandlerExpr(string objExpr, string handlerExpr, TypeShapeModel declaringType, EventShapeModel eventModel, bool isAdd)
        {
            string refPrefix = declaringType.Type.IsValueType ? "ref " : "";
            string suppressSuffix = eventModel.DeclaringType.IsValueType ? "" : "!";
            string op = isAdd ? "+=" : "-=";
            return eventModel switch
            {
                { IsStatic: true, IsAccessible: true } => $"{eventModel.DeclaringType.FullyQualifiedName}.{eventModel.UnderlyingMemberName} {op} {handlerExpr}",
                { IsStatic: true, IsAccessible: false } => $"{GetEventAccessorName(declaringType, eventModel, isAdd)}({handlerExpr})",
                { IsStatic: false, IsAccessible: true } => $"{ApplyDisambiguation(eventModel, objExpr)}{suppressSuffix}.{eventModel.UnderlyingMemberName} {op} {handlerExpr}",
                { IsStatic: false, IsAccessible: false } => $"{GetEventAccessorName(declaringType, eventModel, isAdd)}({refPrefix}{objExpr}, {handlerExpr})",
            };

            static string ApplyDisambiguation(EventShapeModel eventModel, string objExpr)
            {
                return eventModel.RequiresDisambiguation ? $"(({eventModel.DeclaringType.FullyQualifiedName}?){objExpr})" : $"{objExpr}";
            }
        }
    }

    private static string GetEventAccessorName(TypeShapeModel declaringType, EventShapeModel eventShapeModel, bool isAdd)
    {
        string methodPrefix = isAdd ? "add" : "remove";
        return $"__EventAccessor_{declaringType.SourceIdentifier}_{methodPrefix}_{eventShapeModel.UnderlyingMemberName}";
    }

    private static void FormatEventAccessor(SourceWriter writer, TypeShapeModel declaringType, EventShapeModel eventModel, bool isAdd)
    {
        Debug.Assert(!eventModel.IsAccessible);

        string accessorName = GetEventAccessorName(declaringType, eventModel, isAdd);
        if (!eventModel.CanUseUnsafeAccessors)
        {
            // Emit a reflection-based workaround.
            string eventInfoMethodProp = isAdd ? "AddMethod" : "RemoveMethod";
            if (eventModel.IsStatic)
            {
                writer.WriteLine($$"""
                    private static global::System.Reflection.MethodInfo? __s_{{accessorName}}_MethodInfo;
                    private static void {{accessorName}}({{eventModel.HandlerType.FullyQualifiedName}} handler)
                    {
                        global::System.Reflection.MethodInfo methodInfo = __s_{{accessorName}}_MethodInfo ??= typeof({{eventModel.DeclaringType.FullyQualifiedName}}).GetEvent({{FormatStringLiteral(eventModel.UnderlyingMemberName)}}, {{AllBindingFlagsConstMember}})!.{{eventInfoMethodProp}}!;
                        methodInfo.Invoke(null, new object?[] { handler });
                    }
                    """);
            }
            else
            {
                string refPrefix = declaringType.Type.IsValueType ? "ref " : "";
                string nullableSuffix = declaringType.Type.IsValueType ? "" : "?";
                writer.WriteLine($$"""
                    private static global::System.Reflection.MethodInfo? __s_{{accessorName}}_MethodInfo;
                    private static void {{accessorName}}({{refPrefix}}{{declaringType.Type.FullyQualifiedName}}{{nullableSuffix}} obj, {{eventModel.HandlerType.FullyQualifiedName}} handler)
                    {
                        global::System.Reflection.MethodInfo methodInfo = __s_{{accessorName}}_MethodInfo ??= typeof({{eventModel.DeclaringType.FullyQualifiedName}}).GetEvent({{FormatStringLiteral(eventModel.UnderlyingMemberName)}}, {{AllBindingFlagsConstMember}})!.{{eventInfoMethodProp}}!;
                    """);

                if (declaringType.Type.IsValueType)
                {
                    writer.WriteLine("""
                            object boxedObj = obj!;
                            methodInfo.Invoke(boxedObj, new object?[] { handler });
                            obj = ({{declaringType.Type.FullyQualifiedName}})boxedObj!;
                        }
                        """);
                }
                else
                {
                    writer.WriteLine("""
                            methodInfo.Invoke(obj!, new object?[] { handler });
                        }
                        """);
                }
            }

            return;
        }

        string methodPrefix = isAdd ? "add" : "remove";
        string methodName = $"{methodPrefix}_{eventModel.UnderlyingMemberName}";
        if (eventModel.IsStatic)
        {
            // .NET 10+ supports static event accessors using UnsafeAccessorTypeAttribute
            writer.WriteLine($"""
                [global::System.Runtime.CompilerServices.UnsafeAccessorType({FormatStringLiteral(eventModel.DeclaringType.FullyQualifiedName)})]
                [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = {FormatStringLiteral(methodName)})]
                private static extern void {accessorName}({eventModel.HandlerType.FullyQualifiedName} handler);
                """);
        }
        else
        {
            string refPrefix = declaringType.Type.IsValueType ? "ref " : "";
            string nullableSuffix = declaringType.Type.IsValueType ? "" : "?";
            writer.WriteLine($"""
                [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(methodName)})]
                private static extern void {accessorName}({refPrefix}{declaringType.Type.FullyQualifiedName}{nullableSuffix} obj, {eventModel.HandlerType.FullyQualifiedName} handler);
                """);
        }
    }
}
