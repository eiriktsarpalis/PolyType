#region CustomGenerationAttributes
using PolyType;
using PolyType.Abstractions;

[assembly: GenerateShapeAttributeAttribute(
    typeof(CSharpSamples.JsonRpcContractAttribute),
    IncludeMethods = MethodShapeFlags.PublicInstance)]

namespace CSharpSamples;

public sealed class JsonRpcContractAttribute : Attribute;

[JsonRpcContract]
public partial interface ICalculator
{
    int Add(int x, int y);
}
#endregion
