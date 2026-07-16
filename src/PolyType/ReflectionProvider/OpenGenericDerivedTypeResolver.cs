// Implements the structural unification algorithm used to close open generic derived types
// against a constructed base type. This file contains the reflection-side mirror of the
// source-generator algorithm in PolyType.SourceGenerator.Helpers.OpenGenericDerivedTypeHelpers.
// Any structural change here MUST be applied on both sides to keep reflection and source-gen
// behaviour in sync.
//
// The algorithm is a port of the resolver added in dotnet/runtime#127318 (System.Text.Json
// support for open generic [JsonDerivedType]), with failure semantics aligned to the refinements
// in dotnet/runtime#130808. See those PRs for the supported and rejected patterns.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.ReflectionProvider;

internal static class OpenGenericDerivedTypeResolver
{
    // Enumerates every ancestor of `type` whose generic type definition matches
    // `baseTypeDefinition`. For interface bases this yields every implementing instantiation
    // (a type can implement the same interface definition with different type arguments); for
    // class bases it yields at most the first match found while walking the base-type chain
    // (only one such instantiation is reachable).
    //
    // Mirrors PolyType.Roslyn.Helpers.RoslynHelpers.GetCompatibleGenericBaseTypes.
    [RequiresUnreferencedCode("Touches derived-type interfaces supplied via [DerivedTypeShape]; the interface metadata is rooted at the attribute usage site and survives trimming.")]
    public static IEnumerable<Type> GetMatchingGenericBaseTypes(this Type type, Type baseTypeDefinition)
    {
        Debug.Assert(baseTypeDefinition.IsGenericTypeDefinition);

        if (baseTypeDefinition.IsInterface)
        {
            foreach (Type iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == baseTypeDefinition)
                {
                    yield return iface;
                }
            }

            // Note: do NOT yield break here. Type.GetInterfaces() does not include `type` itself,
            // so when `type` IS the interface we're looking for, the fall-through to the
            // BaseType walk below picks it up via the self-check on the first iteration
            // (Type.BaseType returns null for interfaces, so the loop terminates immediately).
        }

        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == baseTypeDefinition)
            {
                yield return current;
                yield break;
            }
        }
    }

    // Attempts to unify a `pattern` type (which may contain generic parameter references) with
    // a `target` type, recording bindings in `substitution`. Returns true if the pattern
    // matches the target under some extension of the current substitution.
    public static bool TryUnifyWith(this Type pattern, Type target, IDictionary<Type, Type> substitution)
    {
        if (pattern.IsGenericParameter)
        {
            if (substitution.TryGetValue(pattern, out Type? existing))
            {
                return existing == target;
            }

            substitution[pattern] = target;
            return true;
        }

        if (pattern.IsArray)
        {
            if (!target.IsArray)
            {
                return false;
            }

            if (pattern.GetArrayRank() != target.GetArrayRank())
            {
                return false;
            }

            // Distinguish single-dim zero-based arrays (T[]) from non-SZ rank-1 arrays (T[*]).
#if NET
            if (pattern.IsSZArray != target.IsSZArray)
            {
                return false;
            }
#endif

            return pattern.GetElementType()!.TryUnifyWith(target.GetElementType()!, substitution);
        }

        if (pattern.IsPointer)
        {
            return target.IsPointer
                && pattern.GetElementType()!.TryUnifyWith(target.GetElementType()!, substitution);
        }

        if (pattern.IsByRef)
        {
            return target.IsByRef
                && pattern.GetElementType()!.TryUnifyWith(target.GetElementType()!, substitution);
        }

        if (pattern.IsGenericType)
        {
            if (!target.IsGenericType || pattern.GetGenericTypeDefinition() != target.GetGenericTypeDefinition())
            {
                return false;
            }

            Type[] patternArgs = pattern.GetGenericArguments();
            Type[] targetArgs = target.GetGenericArguments();
            if (patternArgs.Length != targetArgs.Length)
            {
                return false;
            }

            for (int i = 0; i < patternArgs.Length; i++)
            {
                if (!patternArgs[i].TryUnifyWith(targetArgs[i], substitution))
                {
                    return false;
                }
            }

            return true;
        }

        return pattern == target;
    }

    // Adds every generic-parameter type referenced inside `pattern` (directly or through
    // generic / array / pointer / byref wrappers) to `set`.
    public static void CollectReferencedParameters(Type pattern, HashSet<Type> set)
    {
        if (pattern.IsGenericParameter)
        {
            set.Add(pattern);
            return;
        }

        if (pattern.HasElementType)
        {
            CollectReferencedParameters(pattern.GetElementType()!, set);
            return;
        }

        if (pattern.IsGenericType)
        {
            foreach (Type arg in pattern.GetGenericArguments())
            {
                CollectReferencedParameters(arg, set);
            }
        }
    }

    // Closes `openDerivedType` against the constructed `baseType` via structural unification.
    //
    // Mirrors the source-gen resolver in
    // PolyType.SourceGenerator.Parser.TryResolveOpenGenericDerivedType. Both implementations --
    // the structural unbound pre-check, the per-ancestor unification, and the ambiguity
    // detection -- must be kept in lockstep so that reflection and source-gen produce the same
    // closed type for the same registration.
    //
    // Known intentional asymmetry: source-gen rejects a managed value type (e.g. a struct
    // containing reference fields) for a `where T : unmanaged` constraint because the generated
    // C# would not compile. The reflection resolver delegates constraint validation to
    // Type.MakeGenericType, which only enforces the underlying value-type part of the
    // constraint at runtime.
    [RequiresUnreferencedCode(ReflectionTypeShapeProvider.RequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(ReflectionTypeShapeProvider.RequiresDynamicCodeMessage)]
    public static bool TryResolveOpenGenericDerivedType(
        Type openDerivedType,
        Type baseType,
        [NotNullWhen(true)] out Type? closedDerivedType,
        [NotNullWhen(false)] out string? failureReason)
    {
        closedDerivedType = null;
        failureReason = null;

        if (!baseType.IsGenericType)
        {
            failureReason = "the derived type is not assignable to the base type";
            return false;
        }

        Type baseTypeDefinition = baseType.GetGenericTypeDefinition();
        Type[] baseTypeArgs = baseType.GetGenericArguments();

        // Find every ancestor of the open derived type whose generic type definition matches
        // the base type definition. For classes there is at most one such ancestor; for
        // interfaces a derived type can implement the same interface definition multiple times
        // with different type arguments (e.g. Derived<T> : IBase<T>, IBase<List<T>>).
        List<Type> matchingBases = new();
        foreach (Type match in openDerivedType.GetMatchingGenericBaseTypes(baseTypeDefinition))
        {
            matchingBases.Add(match);
        }

        if (matchingBases.Count == 0)
        {
            failureReason = "the derived type is not assignable to the base type";
            return false;
        }

        // The full set of generic parameters we must bind includes the parameters of the
        // derived type itself plus any parameters declared by enclosing generic types
        // (e.g. Outer<T>.Derived needs T bound from the outer class).
        Type[] requiredParams = openDerivedType.GetGenericArguments();

        // Structural unbound pre-check: every required parameter must appear at least once
        // somewhere in some matching ancestor's type arguments. If a parameter never appears
        // at all, no closed base could ever bind it.
        HashSet<Type> referencedParams = new();
        foreach (Type mb in matchingBases)
        {
            foreach (Type arg in mb.GetGenericArguments())
            {
                CollectReferencedParameters(arg, referencedParams);
            }
        }

        foreach (Type required in requiredParams)
        {
            if (!referencedParams.Contains(required))
            {
                failureReason = $"the type parameter '{required.Name}' of the derived type is not bound by the base type's arguments";
                return false;
            }
        }

        Type[]? successfulArgs = null;
        int successCount = 0;

        foreach (Type matchingBase in matchingBases)
        {
            Type[] matchingBaseArgs = matchingBase.GetGenericArguments();
            Debug.Assert(
                matchingBaseArgs.Length == baseTypeArgs.Length,
                "matchingBase and baseTypeArgs share the same generic type definition, so arity must match.");

            var substitution = new Dictionary<Type, Type>(requiredParams.Length);
            bool unified = true;
            for (int i = 0; i < matchingBaseArgs.Length; i++)
            {
                if (!matchingBaseArgs[i].TryUnifyWith(baseTypeArgs[i], substitution))
                {
                    unified = false;
                    break;
                }
            }

            if (!unified)
            {
                continue;
            }

            // Unification succeeded for every position. Every required parameter of the
            // derived type definition must be bound by this ancestor; otherwise the
            // resulting closed type would have unbound type arguments. A sibling ancestor
            // may still bind this parameter, so failure here is not fatal.
            Type[] closedArgs = new Type[requiredParams.Length];
            bool allBound = true;
            for (int i = 0; i < requiredParams.Length; i++)
            {
                if (!substitution.TryGetValue(requiredParams[i], out Type? boundArg))
                {
                    allBound = false;
                    break;
                }

                closedArgs[i] = boundArg;
            }

            if (!allBound)
            {
                continue;
            }

            successCount++;
            if (successCount == 1)
            {
                successfulArgs = closedArgs;
            }
            else
            {
                failureReason = "the derived type matches the base type through multiple ancestors";
                return false;
            }
        }

        if (successCount == 0 || successfulArgs is null)
        {
            failureReason = "the base type's arguments do not match the derived type's base specification";
            return false;
        }

        try
        {
            closedDerivedType = openDerivedType.MakeGenericType(successfulArgs);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or TypeLoadException)
        {
            failureReason = "the closed derived type would violate one of its declared generic constraints";
            return false;
        }
    }
}
