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
            string? attributeFactory = GetEventAttributeFactoryName(eventModel);
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
                    AttributeFactory = {{FormatNull(attributeFactory)}},
                    EventInfoFactory = static () => typeof({{eventModel.DeclaringType.FullyQualifiedName}}).GetEvent({{FormatStringLiteral(eventModel.UnderlyingMemberName)}}, {{AllBindingFlagsConstMember}}),
                },
                """, trimDefaultAssignmentLines: true);
        }

        writer.Indentation--;
        writer.WriteLine("};");

        foreach (EventShapeModel eventModel in declaringType.Events)
        {
            if (GetEventAttributeFactoryName(eventModel) is string attributeFactoryName)
            {
                FormatAttributesFactory(writer, attributeFactoryName, eventModel.Attributes);
            }
        }

        foreach (EventShapeModel eventModel in declaringType.Events)
        {
            if (!eventModel.IsAccessible)
            {
                FormatEventAccessor(writer, declaringType, eventModel, isAdd: true);
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

        string? GetEventAttributeFactoryName(EventShapeModel eventDataModel) =>
            eventDataModel.Attributes.Length > 0
            ? $"__CreateEventAttributes_{declaringType.SourceIdentifier}_{eventDataModel.UnderlyingMemberName}"
            : null;
    }

    private static string GetEventAccessorName(TypeShapeModel declaringType, EventShapeModel eventShapeModel, bool isAdd, bool qualified = true)
    {
        string methodPrefix = isAdd ? "add" : "remove";
        string suffix = eventShapeModel.DeclaringTypeIndex == 0 ? "" : $"_{eventShapeModel.DeclaringTypeIndex}";
        string name = $"__EventAccessor_{declaringType.SourceIdentifier}{suffix}_{methodPrefix}_{eventShapeModel.UnderlyingMemberName}";
        return qualified ? QualifyAccessorName(declaringType, name, eventShapeModel.DeclaringTypeIndex, eventShapeModel.GenericDeclaringType) : name;
    }

    private void FormatEventAccessor(SourceWriter writer, TypeShapeModel declaringType, EventShapeModel eventModel, bool isAdd)
    {
        Debug.Assert(!eventModel.IsAccessible);

        string accessorName = GetEventAccessorName(declaringType, eventModel, isAdd, qualified: false);
        string refPrefix = eventModel.DeclaringType.IsValueType ? "ref " : "";
        string nullableSuffix = eventModel.DeclaringType.IsValueType ? "" : "?";
        if (!eventModel.CanUseUnsafeAccessors)
        {
            string eventInfoMethodProp = isAdd ? "AddMethod" : "RemoveMethod";
            string delegateType = eventModel.IsStatic
                ? $"global::System.Action<{eventModel.HandlerType.FullyQualifiedName}>"
                : eventModel.DeclaringType.IsValueType
                    ? $"global::PolyType.Abstractions.Setter<{eventModel.DeclaringType.FullyQualifiedName}, {eventModel.HandlerType.FullyQualifiedName}>"
                    : $"global::System.Action<{eventModel.DeclaringType.FullyQualifiedName}, {eventModel.HandlerType.FullyQualifiedName}>";
            string receiverParameter = eventModel.IsStatic ? "" : $"{refPrefix}{eventModel.DeclaringType.FullyQualifiedName}{nullableSuffix} obj, ";
            string receiverArgument = eventModel.IsStatic ? "" : $"{refPrefix}obj!, ";
            writer.WriteLine();
            writer.WriteLine($$"""
                private static {{delegateType}}? {{accessorName}}_Delegate;
                private static void {{accessorName}}({{receiverParameter}}{{eventModel.HandlerType.FullyQualifiedName}} handler)
                {
                    ({{accessorName}}_Delegate ??= CreateDelegate()).Invoke({{receiverArgument}}handler);
                    static {{delegateType}} CreateDelegate()
                    {
                        global::System.Reflection.MethodInfo methodInfo = typeof({{eventModel.DeclaringType.FullyQualifiedName}}).GetEvent({{FormatStringLiteral(eventModel.UnderlyingMemberName)}}, {{AllBindingFlagsConstMember}})!.{{eventInfoMethodProp}}!;
                        return ({{delegateType}})global::System.Delegate.CreateDelegate(typeof({{delegateType}}), methodInfo);
                    }
                }
                """);

            return;
        }

        Debug.Assert(!eventModel.IsStatic);
        string methodPrefix = isAdd ? "add" : "remove";
        string methodName = $"{methodPrefix}_{eventModel.UnderlyingMemberName}";
        string modifiers = GetUnsafeAccessorModifiers(eventModel.GenericDeclaringType);
        string receiverType = eventModel.GenericDeclaringType?.FullyQualifiedName ?? eventModel.DeclaringType.FullyQualifiedName;
        string handlerType = eventModel.OpenHandlerTypeName ?? eventModel.HandlerType.FullyQualifiedName;
        FormatUnsafeAccessor(writer, declaringType, eventModel.DeclaringTypeIndex, eventModel.GenericDeclaringType, $"""
            [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {FormatStringLiteral(methodName)})]
            {modifiers} void {accessorName}({refPrefix}{receiverType}{nullableSuffix} obj, {handlerType} handler);
            """);
    }
}
