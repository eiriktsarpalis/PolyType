using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

internal static class Throw
{
    /// <summary>Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.</summary>
    /// <typeparam name="T">The type of the argument to validate as non-null.</typeparam>
    /// <param name="argument">The reference type argument to validate as non-null.</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
    /// <returns>The validated non-null value.</returns>
    public static T IfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : class
    {
        if (argument is null)
        {
            Throw(paramName);
        }

        return argument;

        [DoesNotReturn]
        static void Throw(string? paramName) => throw new ArgumentNullException(paramName);
    }
}