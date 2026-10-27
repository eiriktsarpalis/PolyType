#if !NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

internal static class ArgumentOutOfRangeExceptionExtensions
{
    extension(ArgumentOutOfRangeException)
    {
        public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value < 0)
            {
                Throw(value, paramName, "Non-negative number required.");
            }
        }

        public static void ThrowIfNegativeOrZero(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value <= 0)
            {
                Throw(value, paramName, "Positive number required.");
            }
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Throw(int value, string? paramName, string message) =>
        throw new ArgumentOutOfRangeException(paramName, value, message);
}
#endif
