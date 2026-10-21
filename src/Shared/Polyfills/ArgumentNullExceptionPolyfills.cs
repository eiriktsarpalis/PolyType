#if !NET
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

internal static class ArgumentNullExceptionPolyfills
{
    extension(ArgumentNullException)
    {
        public static void ThrowIfNull(
            [NotNull] object? argument,
            [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                Throw(paramName);
                [DoesNotReturn]
                static void Throw(string? paramName) => throw new ArgumentNullException(paramName);
            }
        }
    }
}
#endif
