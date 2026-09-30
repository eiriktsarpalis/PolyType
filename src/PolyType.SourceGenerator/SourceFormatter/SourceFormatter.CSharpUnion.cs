using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatCSharpUnionTypeShapeFactory(SourceWriter writer, string methodName, CSharpUnionShapeModel model)
    {
        string casesFactory = $"__Create_UnionCases_{model.SourceIdentifier}";
        string indexGetter = $"__GetUnionCaseIndex_{model.SourceIdentifier}";
        string? methodsFactory = CreateMethodsFactoryName(model);
        string? eventsFactory = CreateEventsFactoryName(model);
        string? associatedTypesFactory = GetAssociatedTypesFactoryName(model);
        string? attributesFactory = GetAttributesFactoryName(model);

        writer.WriteLine($$"""
            private global::PolyType.ITypeShape<{{model.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenUnionTypeShape<{{model.Type.FullyQualifiedName}}>
                {
                    IsContextual = {{FormatBool(model.IsContextual)}},
                    UnionKind = global::PolyType.Abstractions.UnionTypeShapeKind.CSharpUnion,
                    BaseTypeFactory = () => {{model.UnderlyingModel.SourceIdentifier}},
                    UnionCasesFactory = {{casesFactory}},
                    GetUnionCaseIndex = {{indexGetter}},
                    MethodsFactory = {{FormatNull(methodsFactory)}},
                    EventsFactory = {{FormatNull(eventsFactory)}},
                    GetAssociatedTypeShape = {{FormatNull(associatedTypesFactory)}},
                    AttributeFactory = {{FormatNull(attributesFactory)}},
                    Provider = this,
                };
            }
            """, trimDefaultAssignmentLines: true);

        writer.WriteLine();
        writer.WriteLine($"private global::PolyType.Abstractions.IUnionCaseShape[] {casesFactory}() => new global::PolyType.Abstractions.IUnionCaseShape[]");
        writer.WriteLine("{");
        writer.Indentation++;
        foreach (CSharpUnionCaseShapeModel unionCase in model.UnionCases)
        {
            writer.WriteLine($$"""
                new global::PolyType.SourceGenModel.SourceGenUnionCaseShape<{{unionCase.Type.FullyQualifiedName}}, {{model.Type.FullyQualifiedName}}>
                {
                    UnionCaseTypeFactory = () => {{GetShapeModel(unionCase.Type).SourceIdentifier}},
                    Marshaler = new __UnionCaseMarshaler_{{model.SourceIdentifier}}_{{unionCase.Index}}(),
                    Name = {{FormatStringLiteral(unionCase.Name)}},
                    Tag = {{unionCase.Index}},
                    IsTagSpecified = false,
                    IsNullable = {{FormatBool(unionCase.IsNullable)}},
                    Index = {{unionCase.Index}},
                },
                """);
        }

        writer.Indentation--;
        writer.WriteLine("};");
        writer.WriteLine();
        FormatCSharpUnionCaseIndex(writer, model, indexGetter);
        foreach (CSharpUnionCaseShapeModel unionCase in model.UnionCases)
        {
            writer.WriteLine();
            FormatCSharpUnionCaseMarshaler(writer, model, unionCase);
        }

        if (methodsFactory is not null)
        {
            writer.WriteLine();
            FormatMethodsFactory(writer, methodsFactory, model);
        }

        if (eventsFactory is not null)
        {
            writer.WriteLine();
            FormatEventsFactory(writer, eventsFactory, model);
        }

        if (associatedTypesFactory is not null)
        {
            writer.WriteLine();
            FormatAssociatedTypesFactory(writer, model, associatedTypesFactory);
        }

        if (attributesFactory is not null)
        {
            writer.WriteLine();
            FormatAttributesFactory(writer, attributesFactory, model.Attributes);
        }
    }

    private static void FormatCSharpUnionCaseIndex(SourceWriter writer, CSharpUnionShapeModel model, string methodName)
    {
        writer.WriteLine($"private int {methodName}(ref {model.Type.FullyQualifiedName} value)");
        writer.WriteLine("{");
        writer.Indentation++;
        writer.WriteLine("return value switch");
        writer.WriteLine("{");
        writer.Indentation++;
        if (model.NullableCaseIndex >= 0)
        {
            writer.WriteLine($"null => {model.NullableCaseIndex},");
        }

        foreach (int index in model.CaseDispatchOrder)
        {
            CSharpUnionCaseShapeModel unionCase = model.UnionCases[index];
            writer.WriteLine($"{FormatCSharpUnionPattern(model, unionCase.PatternType.FullyQualifiedName)} => {index},");
        }

        writer.Indentation--;
        writer.WriteLine("};");
        writer.Indentation--;
        writer.WriteLine("}");
    }

    private static void FormatCSharpUnionCaseMarshaler(SourceWriter writer, CSharpUnionShapeModel model, CSharpUnionCaseShapeModel unionCase)
    {
        string caseType = unionCase.Type.FullyQualifiedName + (unionCase.Type.IsValueType ? "" : "?");
        string unionType = model.Type.FullyQualifiedName + (model.Type.IsValueType ? "" : "?");
        string argument = (unionCase.RequiresInArgument ? "in " : "") + "value!";
        string invocation = unionCase.CreatorKind switch
        {
            UnionCaseCreatorKind.Constructor => $"new {model.Type.FullyQualifiedName}({argument})",
            UnionCaseCreatorKind.StaticFactory => $"{unionCase.FactoryDeclaringType!.Value.FullyQualifiedName}.Create({argument})",
            UnionCaseCreatorKind.ConstrainedFactory => $"Create<{model.Type.FullyQualifiedName}>({argument})",
            _ => throw new InvalidOperationException(),
        };

        writer.WriteLine($$"""
            private sealed class __UnionCaseMarshaler_{{model.SourceIdentifier}}_{{unionCase.Index}} : global::PolyType.IMarshaler<{{unionCase.Type.FullyQualifiedName}}, {{model.Type.FullyQualifiedName}}>
            {
                public {{unionType}} Marshal({{caseType}} value) => {{invocation}};

                public {{caseType}} Unmarshal({{unionType}} value)
                {
                    switch (value)
                    {
            """);

        writer.Indentation += 3;
        if (unionCase.IsNullable)
        {
            writer.WriteLine($$"""
                case null:
                    return default({{caseType}});
                """);
        }

        writer.WriteLine($$"""
            case {{FormatCSharpUnionPattern(model, $"{unionCase.PatternType.FullyQualifiedName} caseValue")}}:
                return caseValue;
            """);
#if DEBUG
        writer.WriteLine("#pragma warning disable CS0162 // The default case also rejects malformed union payloads.", disableIndentation: true);
#endif
        writer.WriteLine("""
            default:
                __ThrowInvalidUnionCase(nameof(value));
                return default;
            """);
#if DEBUG
        writer.WriteLine("#pragma warning restore CS0162", disableIndentation: true);
#endif
        writer.Indentation--;
        writer.WriteLine("}");
        writer.Indentation--;
        writer.WriteLine("}");
        if (unionCase.CreatorKind is UnionCaseCreatorKind.ConstrainedFactory)
        {
            writer.WriteLine();
            string parameterModifier = unionCase.RequiresInArgument ? "in " : "";
            writer.WriteLine($$"""
                private static {{model.Type.FullyQualifiedName}} Create<TUnion>({{parameterModifier}}{{caseType}} value)
                    where TUnion : {{unionCase.FactoryDeclaringType!.Value.FullyQualifiedName}}
                    => TUnion.Create({{argument}});
                """);
        }

        writer.Indentation--;
        writer.WriteLine("}");
    }

    private static string FormatCSharpUnionPattern(CSharpUnionShapeModel model, string pattern)
    {
        // RC1 can match the union wrapper for direct type patterns. Use Value property
        // patterns until the RC2 compiler fix allows matching the payload type directly.
        string declaringType = model.ValuePatternType is { } type ? type.FullyQualifiedName + " " : "";
        return $"{declaringType}{{ Value: {pattern} }}";
    }
}
