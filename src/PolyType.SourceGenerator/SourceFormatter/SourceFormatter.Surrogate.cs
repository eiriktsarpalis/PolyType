using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatSurrogateTypeShapeFactory(SourceWriter writer, string methodName, SurrogateShapeModel surrogateShapeModel)
    {
        string? methodFactoryMethodName = CreateMethodsFactoryName(surrogateShapeModel);

        writer.WriteLine($$"""
           private global::PolyType.Abstractions.ITypeShape<{{surrogateShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
           {
               return new global::PolyType.SourceGenModel.SourceGenSurrogateTypeShape<{{surrogateShapeModel.Type.FullyQualifiedName}}, {{surrogateShapeModel.SurrogateType.FullyQualifiedName}}>
               {
                   Marshaler = new {{surrogateShapeModel.MarshalerType.FullyQualifiedName}}(),
                   SurrogateType = {{GetShapeModel(surrogateShapeModel.SurrogateType).SourceIdentifier}},
                   CreateMethodsFunc = {{FormatNull(methodFactoryMethodName)}},
                   Provider = this,
               };
           }
           """);

        if (methodFactoryMethodName is not null)
        {
            writer.WriteLine();
            FormatMethodsFactory(writer, methodFactoryMethodName, surrogateShapeModel);
        }
    }
}