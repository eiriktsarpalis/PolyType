﻿using PolyType.Roslyn;
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
                    CreatePropertiesFunc = {{FormatNull(propertiesFactoryMethodName)}},
                    CreateConstructorFunc = {{FormatNull(constructorFactoryMethodName)}},
                    IsRecordType = {{FormatBool(objectShapeModel.IsRecordType)}},
                    IsTupleType = {{FormatBool(objectShapeModel.IsTupleType)}},
                    Provider = this,
                    AssociatedTypeFactories = {{FormatAssociatedTypeFactory(objectShapeModel)}},
                };
            }
            """, trimNullAssignmentLines: true);

        if (propertiesFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatPropertyFactory(writer, propertiesFactoryMethodName, objectShapeModel);
        }

        if (constructorFactoryMethodName != null)
        {
            writer.WriteLine();
            FormatConstructorFactory(writer, constructorFactoryMethodName, objectShapeModel, objectShapeModel.Constructor!);
        }

        FormatMemberAccessors(writer, objectShapeModel);
    }

    private static string FormatAssociatedTypeFactory(ObjectShapeModel objectShapeModel)
    {
        if (objectShapeModel.AssociatedTypes.Length == 0)
        {
            return "null";
        }

        StringBuilder builder = new();
        builder.Append("static associatedType => associatedType switch { ");
        foreach (AssociatedTypeId associatedType in objectShapeModel.AssociatedTypes)
        {
            builder.Append($"\"{associatedType.Open}\" or \"{associatedType.Closed}\" => () => new {associatedType.CSharpTypeName}(), ");
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
