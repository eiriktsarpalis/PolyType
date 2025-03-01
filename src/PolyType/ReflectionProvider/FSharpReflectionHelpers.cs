using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PolyType.ReflectionProvider;

internal sealed record FSharpUnionInfo(Type Type, bool IsOptional, MemberInfo TagReader, FSharpUnionCaseInfo[] UnionCases);
internal sealed record FSharpUnionCaseInfo(int Tag, string Name, Type DeclaringType, PropertyInfo[] Properties, MethodInfo Constructor);

[RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
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

        FSharpReflectionAccessor accessor = s_reflectionAccessors.GetValue(fsharpCoreAssembly, static fsharpCoreAssembly => new(fsharpCoreAssembly));
        metadata = accessor.GetMetadata(type);
        return true;
    }

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
    private sealed class FSharpReflectionAccessor
    {
        private readonly MethodInfo _getUnionCases;
        private readonly MethodInfo _getConstructor;
        private readonly MethodInfo _getTagReader;

        private readonly PropertyInfo _tag;
        private readonly PropertyInfo _name;
        private readonly MethodInfo _getProperties;

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

        private static T ThrowIfNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        {
            return value ?? throw new InvalidOperationException(name);
        }
    }
}