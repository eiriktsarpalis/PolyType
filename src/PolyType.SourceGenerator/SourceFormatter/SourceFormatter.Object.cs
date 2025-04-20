using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;
using System.Text;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatObjectTypeShapeFactory(SourceWriter writer, string methodName, ObjectShapeModel objectShapeModel)
    {
        string? propertiesFactoryMethodName = objectShapeModel.Properties.Length > 0 ? $"__CreateProperties_{objectShapeModel.SourceIdentifier}" : null;
        string? constructorFactoryMethodName = objectShapeModel.Constructor != null ? $"__CreateConstructor_{objectShapeModel.SourceIdentifier}" : null;

        writer.WriteLine($$"""
            private global::PolyType.Abstractions.ITypeShape<{{objectShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
            {
                return new global::PolyType.SourceGenModel.SourceGenObjectTypeShape<{{objectShapeModel.Type.FullyQualifiedName}}>
                {
                    CreatePropertiesFunc = {{FormatNullOrThrowPartial(propertiesFactoryMethodName, !objectShapeModel.Requirements.HasFlag(TypeShapeRequirements.Properties))}},
                    CreateConstructorFunc = {{FormatNullOrThrowPartial(constructorFactoryMethodName, !objectShapeModel.Requirements.HasFlag(TypeShapeRequirements.Constructor))}},
                    IsRecordType = {{FormatBool(objectShapeModel.IsRecordType)}},
                    IsTupleType = {{FormatBool(objectShapeModel.IsTupleType)}},
                    Provider = this,
                    AssociatedTypeShapes = {{FormatAssociatedTypeShapes(objectShapeModel)}},
                };
            }
            """, trimNullAssignmentLines: true);

        if (objectShapeModel.Requirements.HasFlag(TypeShapeRequirements.Properties) && propertiesFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatPropertyFactory(writer, propertiesFactoryMethodName, objectShapeModel);
        }

        if (objectShapeModel.Requirements.HasFlag(TypeShapeRequirements.Constructor) && constructorFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatConstructorFactory(writer, constructorFactoryMethodName, objectShapeModel, objectShapeModel.Constructor!);
        }

        FormatMemberAccessors(writer, objectShapeModel);
    }

    private static string FormatNullOrThrowPartial(string? stringExpr, bool missing)
        => missing ? "() => throw new global::System.NotImplementedException(\"This shape is not fully implemented.\")" : FormatNull(stringExpr);

    private string FormatAssociatedTypeShapes(ObjectShapeModel objectShapeModel)
    {
        AssociatedTypeId[] associatedTypeShapes = [..
            from associatedType in objectShapeModel.AssociatedTypes
            select associatedType.Key];
        if (associatedTypeShapes.Length == 0)
        {
            return "null";
        }

        StringBuilder builder = new();
        builder.Append("static associatedType => associatedType switch { ");
        foreach (AssociatedTypeId associatedType in associatedTypeShapes)
        {
            builder.Append($"\"{associatedType.Open}\" or \"{associatedType.Closed}\" => {provider.ProviderDeclaration.Id.FullyQualifiedName}.{ProviderSingletonProperty}.{GetShapeModel(associatedType.ClosedTypeId).SourceIdentifier}, ");
        }

        builder.Append("_ => null }");

        return builder.ToString();
    }

    private static void FormatMemberAccessors(SourceWriter writer, ObjectShapeModel objectShapeModel)
    {
        foreach (PropertyShapeModel property in objectShapeModel.Properties)
        {
            if (property.IsField)
            {
                if (!property.IsGetterAccessible)
                {
                    writer.WriteLine();
                    FormatFieldAccessor(writer, objectShapeModel, property);
                }
            }
            else
            {
                if (property is { EmitGetter: true, IsGetterAccessible: false })
                {
                    writer.WriteLine();
                    FormatPropertyGetterAccessor(writer, objectShapeModel, property);
                }

                if ((!property.IsSetterAccessible || property.IsInitOnly) &&
                    (property.EmitSetter || IsUsedByConstructor(property)))
                {
                    writer.WriteLine();
                    FormatPropertySetterAccessor(writer, objectShapeModel, property);
                }

                bool IsUsedByConstructor(PropertyShapeModel property)
                {
                    if (objectShapeModel.Constructor is not { } ctor)
                    {
                        return false;
                    }

                    foreach (var parameter in ctor.RequiredMembers.Concat(ctor.OptionalMembers))
                    {
                        if (parameter.Name == property.Name)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        if (objectShapeModel.Constructor is { IsAccessible: false } ctor)
        {
            writer.WriteLine();
            FormatConstructorAccessor(writer, objectShapeModel, ctor);
        }
    }
}
