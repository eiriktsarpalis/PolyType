using System.Text.RegularExpressions;

namespace PolyType.SourceGenerator.Helpers;

/// <summary>
/// Provides glob pattern matching functionality for type names.
/// </summary>
internal static class GlobPatternMatcher
{
    /// <summary>
    /// Checks if a type name matches any of the provided glob patterns.
    /// </summary>
    /// <param name="typeName">The fully qualified type name to match.</param>
    /// <param name="patterns">The glob patterns to match against.</param>
    /// <returns>True if the type name matches any pattern, false otherwise.</returns>
    public static bool Matches(string typeName, IEnumerable<string> patterns)
    {
        foreach (string pattern in patterns)
        {
            if (Matches(typeName, pattern))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a type name matches a single glob pattern.
    /// </summary>
    /// <param name="typeName">The fully qualified type name to match.</param>
    /// <param name="pattern">The glob pattern to match against.</param>
    /// <returns>True if the type name matches the pattern, false otherwise.</returns>
    public static bool Matches(string typeName, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        // Convert glob pattern to regex
        string regexPattern = ConvertGlobToRegex(pattern);
        
        try
        {
            return Regex.IsMatch(typeName, regexPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
        }
        catch (RegexMatchTimeoutException)
        {
            // If regex times out, fall back to exact match
            return typeName == pattern;
        }
    }

    /// <summary>
    /// Converts a glob pattern to a regular expression pattern.
    /// </summary>
    /// <param name="pattern">The glob pattern.</param>
    /// <returns>A regex pattern string.</returns>
    private static string ConvertGlobToRegex(string pattern)
    {
        // Escape special regex characters except * and ?
        string escaped = Regex.Escape(pattern);
        
        // Replace escaped glob wildcards with regex equivalents
        // \* (escaped *) -> .* (match any characters)
        // \? (escaped ?) -> . (match single character)
        escaped = escaped.Replace(@"\*", ".*").Replace(@"\?", ".");
        
        // Anchor the pattern to match the entire string
        return "^" + escaped + "$";
    }
}
