using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatOptionalTypeShapeFactory(SourceWriter writer, string methodName, OptionalShapeModel optionalShapeModel)
    {
        string? methodFactoryMethodName = CreateMethodsFactoryName(optionalShapeModel);
        string? eventFactoryMethodName = CreateEventsFactoryName(optionalShapeModel);
        string? associatedTypesFactoryMethodName = GetAssociatedTypesFactoryName(optionalShapeModel);

        // Disable CS8622 to avoid a dependency on MaybeNullWhenAttribute in netfx
        writer.WriteLine("#pragma warning disable CS8622 // Nullability warning for out parameter mismatch", disableIndentation: true);
        writer.WriteLine($$"""
            private global::PolyType.ITypeShape<{{optionalShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenOptionalTypeShape<{{optionalShapeModel.Type.FullyQualifiedName}}, {{optionalShapeModel.ElementType.FullyQualifiedName}}>
                {
                    ElementType = {{GetShapeModel(optionalShapeModel.ElementType).SourceIdentifier}},
                    NoneConstructor = {{FormatNoneCtor()}},
                    SomeConstructor = {{FormatSomeCtor()}},
                    Deconstructor = {{FormatDeconstructor()}},
                    CreateMethodsFunc = {{FormatNull(methodFactoryMethodName)}},
                    CreateEventsFunc = {{FormatNull(eventFactoryMethodName)}},
                    GetAssociatedTypeShapeFunc = {{FormatNull(associatedTypesFactoryMethodName)}},
                    Provider = this,
                };
            }
            """, trimNullAssignmentLines: true);
        writer.WriteLine("#pragma warning restore CS8767", disableIndentation: true);

        if (methodFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatMethodsFactory(writer, methodFactoryMethodName, optionalShapeModel);
        }

        if (eventFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatEventsFactory(writer, eventFactoryMethodName, optionalShapeModel);
        }

        if (associatedTypesFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatAssociatedTypesFactory(writer, optionalShapeModel, associatedTypesFactoryMethodName);
        }

        string FormatNoneCtor() =>
            optionalShapeModel.Kind switch
            {
                OptionalKind.NullableOfT => "static () => null",
                OptionalKind.FSharpOption or OptionalKind.FSharpValueOption => $"static () => {optionalShapeModel.Type.FullyQualifiedName}.None",
                _ => throw new InvalidOperationException(),
            };

        string FormatSomeCtor() =>
            optionalShapeModel.Kind switch
            {
                OptionalKind.NullableOfT => "static t => t",
                OptionalKind.FSharpOption or OptionalKind.FSharpValueOption => $"static t => {optionalShapeModel.Type.FullyQualifiedName}.Some(t)",
                _ => throw new InvalidOperationException(),
            };

        string FormatDeconstructor()
        {
            string optionalTypeSuffix = optionalShapeModel.Type.IsValueType ? "" : "?";
            string parameters = $$"""static ({{optionalShapeModel.Type.FullyQualifiedName}}{{optionalTypeSuffix}} optional, out {{optionalShapeModel.ElementType.FullyQualifiedName}} element) => """;
            string expr = optionalShapeModel.Kind switch
            {
                OptionalKind.NullableOfT => $$"""{ if (optional is null) { element = default!; return false; } else { element = optional.Value; return true; } }""",
                OptionalKind.FSharpOption => $$"""{ if (optional is null) { element = default!; return false; } else { element = optional.Value; return true; } }""",
                OptionalKind.FSharpValueOption => $$"""{ if (optional.IsNone) { element = default!; return false; } else { element = optional.Value; return true; } }""",
                _ => throw new InvalidOperationException(),
            };

            return parameters + expr;
        }
    }
}
