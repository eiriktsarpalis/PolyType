// Implements the structural unification algorithm used to close open generic derived types
// against a constructed base type. This file contains the source-generator-side mirror of the
// reflection algorithm in PolyType.Utilities.ReflectionUtilities. Any structural change here
// MUST be applied on both sides to keep reflection and source-gen behaviour in sync.
//
// The algorithm is a port of the resolver added in dotnet/runtime#127318 (System.Text.Json
// support for open generic [JsonDerivedType]). See the PR description for the full set of
// supported and rejected patterns.

using Microsoft.CodeAnalysis;
using PolyType.Roslyn.Helpers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PolyType.SourceGenerator.Helpers;

internal static class OpenGenericDerivedTypeHelpers
{
    /// <summary>
    /// Returns the full set of type parameters that must be bound to construct
    /// <paramref name="typeDef"/>: the type parameters of every enclosing type
    /// (outermost first) followed by the type parameters declared on
    /// <paramref name="typeDef"/> itself.
    /// </summary>
    public static List<ITypeParameterSymbol> GetAllTypeParameters(this INamedTypeSymbol typeDef)
    {
        var result = new List<ITypeParameterSymbol>();
        AppendEnclosing(typeDef.ContainingType, result);
        result.AddRange(typeDef.TypeParameters);
        return result;

        static void AppendEnclosing(INamedTypeSymbol? enclosing, List<ITypeParameterSymbol> list)
        {
            if (enclosing is null)
            {
                return;
            }

            AppendEnclosing(enclosing.ContainingType, list);
            list.AddRange(enclosing.TypeParameters);
        }
    }

    /// <summary>
    /// Attempts to unify a <paramref name="pattern"/> type (which may contain type-parameter
    /// references) with a <paramref name="target"/> type, recording bindings in
    /// <paramref name="substitution"/>. Returns <see langword="true"/> if the pattern matches
    /// the target under some extension of the current substitution.
    /// </summary>
    /// <remarks>
    /// Mirrors the reflection-side <c>TryUnifyWith</c> helper. Roslyn doesn't surface SZ-vs-rank-1
    /// distinction or byref types in attribute syntax, so this implementation handles fewer kinds
    /// than its reflection counterpart.
    /// </remarks>
    public static bool TryUnifyWith(this ITypeSymbol pattern, ITypeSymbol target, IDictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
    {
        if (pattern is ITypeParameterSymbol patternParam)
        {
            if (substitution.TryGetValue(patternParam, out ITypeSymbol? existing))
            {
                return SymbolEqualityComparer.Default.Equals(existing, target);
            }

            substitution[patternParam] = target;
            return true;
        }

        if (pattern is IArrayTypeSymbol patternArray)
        {
            return target is IArrayTypeSymbol targetArray
                && patternArray.Rank == targetArray.Rank
                && patternArray.ElementType.TryUnifyWith(targetArray.ElementType, substitution);
        }

        if (pattern is IPointerTypeSymbol patternPointer)
        {
            return target is IPointerTypeSymbol targetPointer
                && patternPointer.PointedAtType.TryUnifyWith(targetPointer.PointedAtType, substitution);
        }

        if (pattern is INamedTypeSymbol { IsGenericType: true } patternNamed)
        {
            if (target is not INamedTypeSymbol { IsGenericType: true } targetNamed)
            {
                return false;
            }

            if (!SymbolEqualityComparer.Default.Equals(patternNamed.OriginalDefinition, targetNamed.OriginalDefinition))
            {
                return false;
            }

            // Walk ContainingType to mirror reflection's Type.GetGenericArguments() flattening
            // behaviour for nested generic types. Without this recursion, the unifier would (a)
            // miss enclosing-type mismatches (e.g. unify Outer<int>.Box<T> with
            // Outer<string>.Box<int>, false accept), and (b) fail to bind type parameters that
            // only appear in the enclosing type (e.g. Outer<T>.Box<int> against
            // Outer<string>.Box<int>, false reject).
            INamedTypeSymbol? patternContaining = patternNamed.ContainingType;
            INamedTypeSymbol? targetContaining = targetNamed.ContainingType;
            if (patternContaining is not null && targetContaining is not null &&
                !patternContaining.TryUnifyWith(targetContaining, substitution))
            {
                return false;
            }

            ImmutableArray<ITypeSymbol> patternArgs = patternNamed.TypeArguments;
            ImmutableArray<ITypeSymbol> targetArgs = targetNamed.TypeArguments;
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

        return SymbolEqualityComparer.Default.Equals(pattern, target);
    }

    /// <summary>
    /// Returns the type that results from applying <paramref name="substitution"/> to every
    /// type-parameter reference inside <paramref name="type"/>. Generic types and array types
    /// are rebuilt recursively; other types are returned unchanged. For nested generic types,
    /// the substitution is also applied to the containing type so that type parameters
    /// declared on the enclosing type are correctly rebound.
    /// </summary>
    public static ITypeSymbol SubstituteTypeParameters(this Compilation compilation, ITypeSymbol type, IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
    {
        if (type is ITypeParameterSymbol param)
        {
            return substitution.TryGetValue(param, out ITypeSymbol? mapped) ? mapped : type;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named)
        {
            INamedTypeSymbol? containingType = named.ContainingType;
            INamedTypeSymbol? substitutedContaining = null;
            bool containingChanged = false;
            if (containingType is { IsGenericType: true })
            {
                substitutedContaining = (INamedTypeSymbol)compilation.SubstituteTypeParameters(containingType, substitution);
                containingChanged = !SymbolEqualityComparer.Default.Equals(substitutedContaining, containingType);
            }

            ImmutableArray<ITypeSymbol> args = named.TypeArguments;
            ITypeSymbol[]? newArgs = null;
            for (int i = 0; i < args.Length; i++)
            {
                ITypeSymbol substituted = compilation.SubstituteTypeParameters(args[i], substitution);
                if (!SymbolEqualityComparer.Default.Equals(substituted, args[i]))
                {
                    newArgs ??= args.ToArray();
                    newArgs[i] = substituted;
                }
            }

            if (newArgs is null && !containingChanged)
            {
                return type;
            }

            ITypeSymbol[] leafArgs = newArgs ?? args.ToArray();

            if (substitutedContaining is null)
            {
                return named.OriginalDefinition.Construct(leafArgs);
            }

            INamedTypeSymbol nestedDef = substitutedContaining
                .GetTypeMembers(named.Name, leafArgs.Length)
                .Single(t => SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, named.OriginalDefinition));
            return leafArgs.Length == 0 ? nestedDef : nestedDef.Construct(leafArgs);
        }

        if (type is IArrayTypeSymbol array)
        {
            ITypeSymbol substituted = compilation.SubstituteTypeParameters(array.ElementType, substitution);
            return SymbolEqualityComparer.Default.Equals(substituted, array.ElementType)
                ? type
                : compilation.CreateArrayTypeSymbol(substituted, array.Rank);
        }

        return type;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="arg"/> satisfies a
    /// <c>where T : new()</c> constraint — i.e. it is a value type, or a non-abstract,
    /// non-static reference type with an accessible public parameterless constructor.
    /// </summary>
    public static bool SatisfiesNewConstraint(this ITypeSymbol arg)
    {
        if (arg.IsValueType)
        {
            return true;
        }

        if (arg is not INamedTypeSymbol named || named.IsAbstract || named.IsStatic)
        {
            return false;
        }

        foreach (IMethodSymbol ctor in named.InstanceConstructors)
        {
            if (ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility == Accessibility.Public)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that every type parameter in <paramref name="parameters"/> has a substitution
    /// in <paramref name="substitution"/> that satisfies the parameter's declared constraints
    /// (reference type, value type, unmanaged, <c>new()</c>, and constraint types). Constraint
    /// types are themselves substituted before checking, to handle F-bounded constraints such
    /// as <c>where T : IFoo&lt;U&gt;</c>.
    /// </summary>
    public static bool TryValidateGenericConstraints(
        this Compilation compilation,
        IReadOnlyList<ITypeParameterSymbol> parameters,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> substitution,
        [NotNullWhen(false)] out ITypeParameterSymbol? failedParameter,
        out ITypeSymbol? failedArgument)
    {
        foreach (ITypeParameterSymbol param in parameters)
        {
            if (!substitution.TryGetValue(param, out ITypeSymbol? arg))
            {
                failedParameter = param;
                failedArgument = null;
                return false;
            }

            if (param.HasReferenceTypeConstraint && !arg.IsReferenceType)
            {
                failedParameter = param;
                failedArgument = arg;
                return false;
            }

            if (param.HasValueTypeConstraint)
            {
                if (!arg.IsValueType || arg is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
                {
                    failedParameter = param;
                    failedArgument = arg;
                    return false;
                }
            }

            if (param.HasUnmanagedTypeConstraint && !arg.IsUnmanagedType)
            {
                failedParameter = param;
                failedArgument = arg;
                return false;
            }

            if (param.HasConstructorConstraint && !arg.SatisfiesNewConstraint())
            {
                failedParameter = param;
                failedArgument = arg;
                return false;
            }

            foreach (ITypeSymbol constraintType in param.ConstraintTypes)
            {
                ITypeSymbol substituted = compilation.SubstituteTypeParameters(constraintType, substitution);

                // Use HasImplicitConversion so generic variance is respected (e.g.
                // `where T : IEnumerable<object>` is satisfied by `List<string>` via the
                // covariant `IEnumerable<out T>`).
                if (!compilation.HasImplicitConversion(arg, substituted))
                {
                    failedParameter = param;
                    failedArgument = arg;
                    return false;
                }
            }
        }

        failedParameter = null;
        failedArgument = null;
        return true;
    }

    /// <summary>
    /// Constructs <paramref name="typeDef"/> using <paramref name="allArgs"/>, accounting for
    /// nesting: the leading args bind enclosing-type parameters (outermost first) and the
    /// trailing args bind <paramref name="typeDef"/>'s own parameters. Non-generic intermediate
    /// enclosing types still need to be re-resolved against the constructed outer so that
    /// references to their generic outers carry the supplied type arguments.
    /// </summary>
    public static INamedTypeSymbol ConstructWithEnclosingTypeArguments(this INamedTypeSymbol typeDef, IReadOnlyList<ITypeSymbol> allArgs)
    {
        int offset = 0;
        INamedTypeSymbol? constructedContaining = ConstructEnclosing(typeDef.ContainingType, allArgs, ref offset);

        int leafParamCount = typeDef.TypeParameters.Length;
        ITypeSymbol[] leafArgs = new ITypeSymbol[leafParamCount];
        for (int i = 0; i < leafParamCount; i++)
        {
            leafArgs[i] = allArgs[offset + i];
        }

        if (constructedContaining is not null)
        {
            INamedTypeSymbol nestedDef = constructedContaining
                .GetTypeMembers(typeDef.Name, leafParamCount)
                .Single(t => SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, typeDef));
            return leafParamCount == 0 ? nestedDef : nestedDef.Construct(leafArgs);
        }

        return leafParamCount == 0 ? typeDef : typeDef.Construct(leafArgs);

        static INamedTypeSymbol? ConstructEnclosing(INamedTypeSymbol? enclosing, IReadOnlyList<ITypeSymbol> allArgs, ref int offset)
        {
            if (enclosing is null)
            {
                return null;
            }

            INamedTypeSymbol? constructedOuter = ConstructEnclosing(enclosing.ContainingType, allArgs, ref offset);

            int paramCount = enclosing.TypeParameters.Length;
            ITypeSymbol[] enclosingArgs = new ITypeSymbol[paramCount];
            for (int i = 0; i < paramCount; i++)
            {
                enclosingArgs[i] = allArgs[offset + i];
            }
            offset += paramCount;

            if (constructedOuter is null)
            {
                return paramCount == 0 ? enclosing : enclosing.Construct(enclosingArgs);
            }

            INamedTypeSymbol nestedDef = constructedOuter
                .GetTypeMembers(enclosing.Name, paramCount)
                .Single(t => SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, enclosing));
            return paramCount == 0 ? nestedDef : nestedDef.Construct(enclosingArgs);
        }
    }

    /// <summary>
    /// Adds every type-parameter symbol referenced inside <paramref name="pattern"/> (directly or
    /// through generic, array, or pointer wrappers, including enclosing-type parameters) to
    /// <paramref name="set"/>.
    /// </summary>
    public static void CollectReferencedParameters(ITypeSymbol pattern, HashSet<ITypeParameterSymbol> set)
    {
        switch (pattern)
        {
            case ITypeParameterSymbol tp:
                set.Add(tp);
                return;

            case IArrayTypeSymbol array:
                CollectReferencedParameters(array.ElementType, set);
                return;

            case IPointerTypeSymbol pointer:
                CollectReferencedParameters(pointer.PointedAtType, set);
                return;

            case INamedTypeSymbol { IsGenericType: true } named:
                if (named.ContainingType is { IsGenericType: true } containing)
                {
                    CollectReferencedParameters(containing, set);
                }

                foreach (ITypeSymbol arg in named.TypeArguments)
                {
                    CollectReferencedParameters(arg, set);
                }
                return;
        }
    }
}

/// <summary>
/// Identifies why an open generic derived type could not be resolved against a constructed base.
/// </summary>
internal enum OpenGenericResolutionFailure
{
    /// <summary>The derived type cannot be assigned to the base type (no matching ancestor).</summary>
    NotAssignable,
    /// <summary>The base type's arguments could not be unified with the derived type's base specification.</summary>
    UnificationFailed,
    /// <summary>One of the derived type's type parameters is not bound by the base type's arguments.</summary>
    UnboundParameter,
    /// <summary>The closed derived type would violate one of its declared generic constraints.</summary>
    ConstraintViolation,
    /// <summary>The derived type matches the base type through multiple distinct ancestors.</summary>
    AmbiguousMatch,
}

internal static class OpenGenericResolutionFailureExtensions
{
    // Returns true when the failure is caused by the registration not matching THIS particular
    // closed base, but where the same registration could plausibly apply to a different
    // instantiation of the base. Such failures are silently skipped rather than reported as
    // diagnostics, allowing a single attribute on an open base to span multiple closed
    // instantiations naturally.
    public static bool IsPerInstantiationFailure(this OpenGenericResolutionFailure failure) =>
        failure is OpenGenericResolutionFailure.UnificationFailed
            or OpenGenericResolutionFailure.ConstraintViolation;
}
