using PolyType.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.ReflectionProvider;

internal sealed record FSharpUnionInfo(Type Type, bool IsOptional, MemberInfo TagReader, FSharpUnionCaseInfo[] UnionCases);
internal sealed record FSharpUnionCaseInfo(int Tag, string Name, Type DeclaringType, PropertyInfo[] Properties, MethodInfo Constructor);
internal sealed record FSharpFuncInfo(
    Type Type,
    Type ReturnType,
    Type EffectiveReturnType,
    FSharpFuncParameter[] Parameters,
    MethodInfo[] CurriedInvocationChain,
    bool IsAsync,
    bool IsVoidLike,
    bool IsUnitReturning)
    : IMethodShapeInfo
{
    public bool IsPublic => true;
    public MethodBase? Method => CurriedInvocationChain[0];
    IParameterShapeInfo[] IMethodShapeInfo.Parameters => Parameters;
}

internal sealed record FSharpFuncParameter(Type Type, string Name, ParameterInfo ParameterInfo) : IParameterShapeInfo
{
    public ParameterKind Kind => ParameterKind.MethodParameter;
    public ICustomAttributeProvider AttributeProvider => ParameterInfo;
    public bool IsByRef => false;
    public bool IsRequired => true;
    public bool IsNonNullable => false;
    public bool IsPublic => true;
    public bool HasDefaultValue => false;
    public object? DefaultValue => null;
}

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
[RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
internal static class FSharpReflectionHelpers
{
    // Conditional weak table to account for multiple assemblies and unloadability.
    private static readonly ConditionalWeakTable<Assembly, FSharpReflectionAccessor> s_reflectionAccessors = new();

    public static bool TryResolveFSharpUnionMetadata(Type type, [NotNullWhen(true)] out FSharpUnionInfo? metadata)
    {
        if (type is { IsClass: false, IsValueType: false } ||
            !TryGetSourceConstructFlags(type, out FSharpSourceConstructFlags flags, out Assembly? fsharpCoreAssembly) ||
            flags is not FSharpSourceConstructFlags.SumType)
        {
            metadata = null;
            return false;
        }

        if (type is { IsGenericType: true, Name: "FSharpList`1", Namespace: "Microsoft.FSharp.Collections" })
        {
            // Exclude F# lists since they're handled as collections and not unions.
            metadata = null;
            return false;
        }

        FSharpReflectionAccessor accessor = GetReflectionAccessor(fsharpCoreAssembly);
        metadata = accessor.GetMetadata(type);
        return true;
    }

    public static bool IsFSharpUnitType(this Type type) => type is { IsValueType: false, Name: "Unit", Namespace: "Microsoft.FSharp.Core" };

    public static bool TryResolveFSharpFuncMetadata(Type type, [NotNullWhen(true)] out FSharpFuncInfo? metadata)
    {
        if (!type.IsClass || !type.IsGenericType ||
            !TryGetSourceConstructFlags(type, out FSharpSourceConstructFlags _, out Assembly? fsharpCoreAssembly))
        {
            metadata = null;
            return false;
        }

        FSharpReflectionAccessor accessor = GetReflectionAccessor(fsharpCoreAssembly);
        metadata = accessor.GetFSharpFuncInfo(type);
        return metadata is not null;
    }

    private static FSharpReflectionAccessor GetReflectionAccessor(Assembly fsharpCoreAssembly) =>
        s_reflectionAccessors.GetValue(fsharpCoreAssembly, static fsharpCoreAssembly => new(fsharpCoreAssembly));

    private static bool TryGetSourceConstructFlags(MemberInfo memberInfo, out FSharpSourceConstructFlags flags, [NotNullWhen(true)] out Assembly? fsharpCoreAssembly)
    {
        // https://fsharp.github.io/fsharp-core-docs/reference/fsharp-core-compilationmappingattribute.html
        fsharpCoreAssembly = null;
        foreach (CustomAttributeData attr in memberInfo.CustomAttributes)
        {
            if (attr.AttributeType is { Name: "CompilationMappingAttribute", Namespace: "Microsoft.FSharp.Core" })
            {
                if (attr.ConstructorArguments is [{ Value: int intFlags }, ..])
                {
                    flags = (FSharpSourceConstructFlags)intFlags;
                    fsharpCoreAssembly = attr.AttributeType.Assembly;
                    return true;
                }
            }
        }

        flags = default;
        fsharpCoreAssembly = null;
        return false;
    }

    [RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
    private sealed class FSharpReflectionAccessor
    {
        private readonly MethodInfo _getUnionCases;
        private readonly MethodInfo _getConstructor;
        private readonly MethodInfo _getTagReader;

        private readonly PropertyInfo _tag;
        private readonly PropertyInfo _name;
        private readonly MethodInfo _getProperties;

        private readonly Type _fsharpFuncType;
        private readonly Type _fsharpUnitType;

        public FSharpReflectionAccessor(Assembly fsharpCoreAssembly)
        {
            // https://fsharp.github.io/fsharp-core-docs/reference/fsharp-reflection.html

            Type fsharpType = fsharpCoreAssembly.GetType("Microsoft.FSharp.Reflection.FSharpType", throwOnError: true)!;
            Type fsharpValue = fsharpCoreAssembly.GetType("Microsoft.FSharp.Reflection.FSharpValue", throwOnError: true)!;
            Type unionCaseInfo = fsharpCoreAssembly.GetType("Microsoft.FSharp.Reflection.UnionCaseInfo", throwOnError: true)!;

            _getUnionCases = ThrowIfNull(fsharpType.GetMethod("GetUnionCases"));
            _getConstructor = ThrowIfNull(fsharpValue.GetMethod("PreComputeUnionConstructorInfo"));
            _getTagReader = ThrowIfNull(fsharpValue.GetMethod("PreComputeUnionTagMemberInfo"));

            _tag = ThrowIfNull(unionCaseInfo.GetProperty("Tag"));
            _name = ThrowIfNull(unionCaseInfo.GetProperty("Name"));
            _getProperties = ThrowIfNull(unionCaseInfo.GetMethod("GetFields"));

            _fsharpFuncType = fsharpCoreAssembly.GetType("Microsoft.FSharp.Core.FSharpFunc`2", throwOnError: true)!;
            _fsharpUnitType = fsharpCoreAssembly.GetType("Microsoft.FSharp.Core.Unit", throwOnError: true)!;
        }

        public FSharpUnionInfo GetMetadata(Type type)
        {
            bool isOptionalType = type is
            {
                IsGenericType: true,
                Name: "FSharpOption`1" or "FSharpValueOption`1",
                Namespace: "Microsoft.FSharp.Core"
            };

            var methodInfo = (MemberInfo)_getTagReader.Invoke(null, [type, null])!;
            var unionCases = (object[])_getUnionCases.Invoke(null, [type, null])!;
            var cases = new FSharpUnionCaseInfo[unionCases.Length];
            for (int i = 0; i < unionCases.Length; i++)
            {
                var unionCase = unionCases[i]!;
                var tag = (int)_tag.GetValue(unionCase)!;
                var name = (string)_name.GetValue(unionCase)!;
                var properties = (PropertyInfo[])_getProperties.Invoke(unionCase, null)!;
                var declaringType = properties.FirstOrDefault()?.DeclaringType ?? type;
                var constructor = (MethodInfo)_getConstructor.Invoke(null, [unionCase, null])!;
                Debug.Assert(tag == i);
                cases[i] = new FSharpUnionCaseInfo(tag, name, declaringType, properties, constructor);
            }

            return new FSharpUnionInfo(type, isOptionalType, methodInfo, cases);
        }

        public FSharpFuncInfo? GetFSharpFuncInfo(Type type)
        {
            if (!IsFSharpFunc(type))
            {
                return null;
            }

            // Uncurry the function parameters.
            List<FSharpFuncParameter> uncurriedParams = [];
            List<MethodInfo> curriedInvocationChain = [];
            Type returnType = type;
            do
            {
                Type[] typeParams = returnType.GetGenericArguments();
                Type argType = typeParams[0];
                MethodInfo invokeMethod = ThrowIfNull(returnType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance, null, [argType], null));
                FSharpFuncParameter parameter = new(argType, $"arg{uncurriedParams.Count + 1}", invokeMethod.GetParameters()[0]);

                uncurriedParams.Add(parameter);
                curriedInvocationChain.Add(invokeMethod);
                returnType = typeParams[1];

            } while (IsFSharpFunc(returnType));

            if (uncurriedParams is [var singleArg] && singleArg.Type == _fsharpUnitType)
            {
                // FSharpFunc<unit, T> is effectively a Func<T>.
                uncurriedParams.Clear();
            }

            Type effectiveReturnType;
            bool isAsync;
            bool isUnitReturnType;
            bool isVoidLike;
            if (returnType == _fsharpUnitType)
            {
                effectiveReturnType = typeof(Unit);
                isUnitReturnType = true;
                isVoidLike = true;
                isAsync = false;
            }
            else
            {
                isUnitReturnType = false;
                isAsync = returnType.IsAsyncType();
                Type? rt = curriedInvocationChain[^1].GetEffectiveReturnType();
                isVoidLike = rt is null;
                effectiveReturnType = rt ?? typeof(Unit);
            }

            return new FSharpFuncInfo(
                type,
                returnType,
                effectiveReturnType,
                uncurriedParams.ToArray(),
                curriedInvocationChain.ToArray(),
                isAsync,
                isVoidLike,
                isUnitReturnType);

            bool IsFSharpFunc(Type type) =>
                type is { IsGenericType: true, IsClass: true } &&
                type.GetGenericTypeDefinition() == _fsharpFuncType;
        }

        private static T ThrowIfNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        {
            return value ?? throw new InvalidOperationException(name);
        }
    }
}