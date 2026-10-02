namespace PolyType;

internal enum TypeAccessibility
{
    /// <summary>Private accessibility.</summary>
    Private,

    /// <summary>Private protected accessibility.</summary>
    ProtectedAndInternal,

    /// <summary>Protected accessibility.</summary>
    Protected,

    /// <summary>Internal accessibility.</summary>
    Internal,

    /// <summary>Protected internal accessibility.</summary>
    ProtectedOrInternal,

    /// <summary>Public accessibility.</summary>
    Public,
}

internal static class TypeVisibilityHelpers
{
    // Uses the C# visibility-domain comparison also used by System.Text.Json:
    // https://github.com/dotnet/roslyn/blob/121e7dc868d26be12b9c3fb52b7b9d2ae41a1ac2/src/Compilers/CSharp/Portable/Symbols/TypeSymbolExtensions.cs#L1048
    public static bool IsAtLeastAsVisibleAs<T>(
        T type,
        T baseType,
        Func<T, TypeAccessibility> getAccessibility,
        Func<T, T?> getContainingType,
        Func<T, T, bool> hasInternalAccessTo,
        Func<T, T, bool> isAccessibleViaInheritance,
        Func<T, T, bool> hasSameDefinition)
        where T : class
    {
        for (T? current = type; current is not null; current = getContainingType(current))
        {
            if (!IsAsRestrictive(current))
            {
                return false;
            }
        }

        return true;

        bool IsAsRestrictive(T candidate)
        {
            TypeAccessibility accessibility = getAccessibility(candidate);
            if (accessibility is TypeAccessibility.Public)
            {
                return true;
            }

            T? candidateParent = getContainingType(candidate);
            for (T? current = baseType; current is not null; current = getContainingType(current))
            {
                TypeAccessibility baseAccessibility = getAccessibility(current);
                bool internalAccess = hasInternalAccessTo(current, candidate);
                bool restrictedToAssembly = baseAccessibility is
                    TypeAccessibility.Private or TypeAccessibility.Internal or TypeAccessibility.ProtectedAndInternal;

                switch (accessibility)
                {
                    case TypeAccessibility.Internal:
                        if (restrictedToAssembly && internalAccess)
                        {
                            return true;
                        }

                        break;

                    case TypeAccessibility.ProtectedAndInternal:
                        if (!restrictedToAssembly || !internalAccess)
                        {
                            break;
                        }

                        goto case TypeAccessibility.Protected;

                    case TypeAccessibility.Protected:
                        if (candidateParent is not null &&
                            ((baseAccessibility is TypeAccessibility.Private && IsWithinDerivedType(current, candidateParent)) ||
                             (baseAccessibility is TypeAccessibility.Protected or TypeAccessibility.ProtectedAndInternal &&
                              getContainingType(current) is { } protectedParent &&
                              isAccessibleViaInheritance(candidateParent, protectedParent))))
                        {
                            return true;
                        }

                        break;

                    case TypeAccessibility.ProtectedOrInternal:
                        if (candidateParent is null)
                        {
                            break;
                        }

                        bool inheritedAccess = getContainingType(current) is { } parent &&
                            isAccessibleViaInheritance(candidateParent, parent);
                        if (baseAccessibility switch
                        {
                            TypeAccessibility.Private => internalAccess || IsWithinDerivedType(current, candidateParent),
                            TypeAccessibility.Internal => internalAccess,
                            TypeAccessibility.Protected => inheritedAccess,
                            TypeAccessibility.ProtectedAndInternal => internalAccess || inheritedAccess,
                            TypeAccessibility.ProtectedOrInternal => internalAccess && inheritedAccess,
                            _ => false,
                        })
                        {
                            return true;
                        }

                        break;

                    case TypeAccessibility.Private:
                        if (baseAccessibility is TypeAccessibility.Private && candidateParent is not null)
                        {
                            for (T? privateParent = getContainingType(current); privateParent is not null; privateParent = getContainingType(privateParent))
                            {
                                if (hasSameDefinition(privateParent, candidateParent))
                                {
                                    return true;
                                }
                            }
                        }

                        break;
                }
            }

            return false;
        }

        bool IsWithinDerivedType(T type, T parent)
        {
            for (T? current = getContainingType(type); current is not null; current = getContainingType(current))
            {
                if (isAccessibleViaInheritance(parent, current))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
