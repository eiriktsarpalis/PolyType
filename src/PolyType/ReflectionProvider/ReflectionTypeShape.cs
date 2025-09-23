using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.ReflectionProvider;

// Base does not set a debugger proxy; individual derived kinds will specify theirs.
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
internal abstract class ReflectionTypeShape<T>(ReflectionTypeShapeProvider provider, ReflectionTypeShapeOptions options)
    : ITypeShape<T>
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
        HashSet<string>? resolvedMethodNames = null;
        foreach (MethodInfo methodInfo in typeof(T).ResolveVisibleMembers().OfType<MethodInfo>())
        {
            if (methodInfo is { DeclaringType.IsInterface: true, IsStatic: true, IsAbstract: true })
            {
                continue; // Skip static abstract methods in interfaces.
            }

            MethodShapeAttribute? shapeAttribute = methodInfo.GetCustomAttribute<MethodShapeAttribute>();
            if (IncludeMethod(methodInfo, shapeAttribute, Options.IncludeMethods))
            {
                string name = shapeAttribute?.Name ?? methodInfo.Name;
                if (!(resolvedMethodNames ??= new()).Add(name))
                {
                    throw new NotSupportedException(
                        $"Multiple methods named '{name}' were found on type '{Type}'. " +
                         "Consider renaming one of them or disambiguating via the MethodShapeAttribute.Name property.");
                }

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

            if (!methodInfo.IsPublic)
            {
                return false; // Skip methods that are not public and unannotated.
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
        HashSet<string>? resolvedEventNames = null;
        foreach (EventInfo eventInfo in typeof(T).ResolveVisibleMembers().OfType<EventInfo>())
        {
            EventShapeAttribute? eventAttr = eventInfo.GetCustomAttribute<EventShapeAttribute>();
            if (IncludeEvent(eventInfo, eventAttr))
            {
                string eventName = eventAttr?.Name ?? eventInfo.Name;
                if (!(resolvedEventNames ??= new()).Add(eventName))
                {
                    throw new NotSupportedException(
                        $"Conflicting members named '{eventName}' were found on type '{Type}'. " +
                         "Consider renaming one of them or disambiguating via the EventShapeAttribute.Name property.");
                }

                yield return Provider.CreateEvent(this, eventInfo, eventName);
            }
        }

        bool IncludeEvent(EventInfo eventInfo, EventShapeAttribute? eventAttr)
        {
            if (eventInfo.IsSpecialName || eventInfo.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
            {
                return false; // Skip events that are special names or compiler-generated.
            }

            if (eventAttr is not null)
            {
                return !eventAttr.Ignore;
            }

            MethodInfo? accessor = eventInfo.AddMethod ?? eventInfo.RemoveMethod;
            if (accessor is not { IsPublic: true })
            {
                return false; // Skip events that are not public and unannotated.
            }

            MethodShapeFlags requiredFlag = accessor.IsStatic ? MethodShapeFlags.PublicStatic : MethodShapeFlags.PublicInstance;
            if ((Options.IncludeMethods & requiredFlag) == 0)
            {
                return false; // Skip events that are not included in the shape by default.
            }

            return true;
        }
    }

    protected string DebuggerDisplay => $"{{Type = \"{Type}\", Kind = {Kind}}}";
}
