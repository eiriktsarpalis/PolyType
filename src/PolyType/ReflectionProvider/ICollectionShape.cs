using System.Reflection;

namespace PolyType.ReflectionProvider;

internal interface ICollectionShape
{
    CollectionConstructorParameterType ClassifyConstructorParameter(ParameterInfo parameter);
}