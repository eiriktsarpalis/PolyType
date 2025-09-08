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

    public IReadOnlyList<IEventShape> Events => _events ?? CommonHelpers.ExchangeIfNull(ref _events, GetEvents().AsReadOnlyList());
    private IReadOnlyList<IEventShape>? _events;

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

        return provider.GetTypeShape(closedType);
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
                yield return ReflectionTypeShapeProvider.CreateMethodShapeInfo(methodInfo, shapeAttribute, ctx);
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
    }

    private IEnumerable<IEventShape> GetEvents()
    {
        const BindingFlags AllEvents = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (EventInfo eventInfo in typeof(T).GetEvents(AllEvents))
        {
            EventShapeAttribute? eventAttr = eventInfo.GetCustomAttribute<EventShapeAttribute>();
            if (IncludeEvent(eventInfo, eventAttr))
            {
                yield return Provider.CreateEvent(this, eventInfo, eventAttr?.Name ?? eventInfo.Name);
            }
        }

        bool IncludeEvent(EventInfo eventInfo, EventShapeAttribute? eventAttr)
        {
            if (eventAttr is not null)
            {
                return !eventAttr.Ignore;
            }

            MethodInfo? accessor = eventInfo.AddMethod ?? eventInfo.RemoveMethod;
            if (accessor is not { IsPublic: true })
            {
                return false;
            }

            MethodShapeFlags requiredFlag = accessor.IsStatic ? MethodShapeFlags.PublicStatic : MethodShapeFlags.PublicInstance;
            if ((Options.IncludeMethods & requiredFlag) == 0)
            {
                return false; // Skip events that are not included in the shape by default.
            }

            return true;
        }
    }
}
