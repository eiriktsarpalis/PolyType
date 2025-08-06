using PolyType.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.ReflectionProvider;

[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal abstract class ReflectionTypeShape<T>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options) : ITypeShape<T>
{
    public abstract TypeShapeKind Kind { get; }
    public abstract object? Accept(TypeShapeVisitor visitor, object? state = null);
    public ReflectionTypeShapeProvider Provider => provider;
    public ReflectionTypeShapeOptions Options => options;
    public Type Type => typeof(T);

    public IReadOnlyList<IMethodShape> Methods => _methods ?? CommonHelpers.ExchangeIfNull(ref _methods, GetMethods().AsReadOnlyList());
    private IReadOnlyList<IMethodShape>? _methods;

    ITypeShapeProvider ITypeShape.Provider => provider;
    ICustomAttributeProvider? ITypeShape.AttributeProvider => typeof(T);
    object? ITypeShape.Invoke(ITypeShapeFunc func, object? state) => func.Invoke(this, state);

    public ITypeShape? GetAssociatedTypeShape(Type associatedType)
    {
        if (associatedType.IsGenericTypeDefinition && this.Type.GenericTypeArguments.Length != associatedType.GetTypeInfo().GenericTypeParameters.Length)
        {
            throw new ArgumentException($"Related type arity ({associatedType.GenericTypeArguments.Length}) mismatch with original type ({this.Type.GenericTypeArguments.Length}).");
        }

        Type closedType = associatedType.IsGenericTypeDefinition
            ? associatedType.MakeGenericType(Type.GenericTypeArguments)
            : associatedType;

        return provider.GetShape(closedType);
    }

    private IEnumerable<IMethodShape> GetMethods()
    {
        foreach (MethodShapeInfo methodShapeInfo in GetMethodShapeInfos())
        {
            yield return Provider.CreateMethod(this, methodShapeInfo);
        }
    }

    private IEnumerable<MethodShapeInfo> GetMethodShapeInfos()
    {
        NullabilityInfoContext? ctx = ReflectionTypeShapeProvider.CreateNullabilityInfoContext();
        const BindingFlags AllMethods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (MethodInfo methodInfo in typeof(T).GetAllMethods(AllMethods))
        {
            MethodShapeAttribute? shapeAttribute = methodInfo.GetCustomAttribute<MethodShapeAttribute>();
            if (IncludeMethod(methodInfo, shapeAttribute, Options.IncludeMethods))
            {
                yield return CreateMethodShapeInfo(methodInfo, shapeAttribute, ctx);
            }
        }

        static bool IncludeMethod(MethodInfo methodInfo, MethodShapeAttribute? shapeAttribute, MethodShapeFlags flags)
        {
            if (methodInfo.IsSpecialName || methodInfo.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
            {
                return false; // Skip methods that are special names (getters, setters, events) or compiler-generated.
            }

            if (shapeAttribute is not null)
            {
                return !shapeAttribute.Ignore; // Skip methods explicitly marked as ignored.
            }

            if (!methodInfo.IsPublic || methodInfo.IsSpecialName)
            {
                return false; // Skip methods that are not public or special names (like property getters/setters).
            }

            if (methodInfo.DeclaringType == typeof(object) || methodInfo.DeclaringType == typeof(ValueType))
            {
                return false; // Skip GetHashCode, ToString, Equals, and other object methods.
            }

            MethodShapeFlags requiredFlag = methodInfo.IsStatic ? MethodShapeFlags.PublicStatic : MethodShapeFlags.PublicInstance;
            if ((flags & requiredFlag) == 0)
            {
                return false; // Skip methods that are not included in the shape by default.
            }

            return true;
        }

        static MethodShapeInfo CreateMethodShapeInfo(
            MethodInfo methodInfo,
            MethodShapeAttribute? shapeAttribute,
            NullabilityInfoContext? nullabilityCtx)
        {
            if (methodInfo.IsGenericMethodDefinition)
            {
                throw new NotSupportedException($"Cannot generate shape for generic method '{methodInfo}'.");
            }

            ParameterInfo[] parameters = methodInfo.GetParameters();
            if (parameters.FirstOrDefault(param => param.IsOut || !param.GetEffectiveParameterType().CanBeGenericArgument()) is { } param)
            {
                throw new NotSupportedException($"Method '{methodInfo}' contains unsupported parameter type '{param.Name}'.");
            }

            if (methodInfo.ReturnType != typeof(void) && !methodInfo.ReturnParameter.GetEffectiveParameterType().CanBeGenericArgument())
            {
                throw new NotSupportedException($"Method '{methodInfo}' has an unsupported return type '{methodInfo.ReturnType}'.");
            }

            int i = 0;
            var parameterShapeInfos = new MethodParameterShapeInfo[parameters.Length];
            foreach (ParameterInfo parameter in parameters)
            {
                ParameterShapeAttribute? parameterShapeAttribute = parameter.GetCustomAttribute<ParameterShapeAttribute>();
                string? paramName = parameterShapeAttribute?.Name ?? parameter.Name;
                if (string.IsNullOrEmpty(paramName))
                {
                    throw new NotSupportedException($"The method '{typeof(T)}.{methodInfo.Name}' has had its parameter names trimmed.");
                }

                bool? isRequired = parameterShapeAttribute?.IsRequiredSpecified is true ? parameterShapeAttribute.IsRequired : null;
                parameterShapeInfos[i++] = new MethodParameterShapeInfo(
                    parameter,
                    isNonNullable: parameter.IsNonNullableAnnotation(nullabilityCtx),
                    logicalName: paramName,
                    isRequired: isRequired);
            }

            string name = shapeAttribute?.Name ?? methodInfo.Name;
            Type returnType = methodInfo.GetEffectiveReturnType() ?? typeof(Unit);
            return new MethodShapeInfo(returnType, methodInfo, parameterShapeInfos, name: name);
        }
    }
}
