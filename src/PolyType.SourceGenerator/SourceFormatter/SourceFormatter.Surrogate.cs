using PolyType.Roslyn;
using PolyType.SourceGenerator.Model;

namespace PolyType.SourceGenerator;

internal sealed partial class SourceFormatter
{
    private void FormatSurrogateTypeShapeFactory(SourceWriter writer, string methodName, SurrogateShapeModel surrogateShapeModel)
    {
        writer.WriteLine($$"""
           private global::PolyType.Abstractions.ITypeShape<{{surrogateShapeModel.Type.FullyQualifiedName}}> {{methodName}}()
           {
               return new global::PolyType.SourceGenModel.SourceGenSurrogateTypeShape<{{surrogateShapeModel.Type.FullyQualifiedName}}, {{surrogateShapeModel.SurrogateType.FullyQualifiedName}}>
               {
                   Marshaller = new {{surrogateShapeModel.MarshallerType.FullyQualifiedName}}(),
                   SurrogateType = {{GetShapeModel(surrogateShapeModel.SurrogateType).SourceIdentifier}},
                   Provider = this,
               };
           }
           """);
    }
}