using System.Text.RegularExpressions;

namespace PolyType.SourceGenerator.Helpers;

/// <summary>
/// Provides glob pattern matching functionality for type names.
/// </summary>
internal sealed class GlobPatternMatcher
{
    private readonly Regex[] _regexPatterns;
    private readonly string[] _exactPatterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobPatternMatcher"/> class.
    /// </summary>
    /// <param name="patterns">The glob patterns to match against.</param>
    public GlobPatternMatcher(IEnumerable<string> patterns)
    {
        List<Regex> regexList = new();
        List<string> exactList = new();

        foreach (string pattern in patterns)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            // Check if pattern contains glob wildcard characters
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                // Pattern has wildcards, compile as regex
                string regexPattern = ConvertGlobToRegex(pattern);
                try
                {
                    regexList.Add(new Regex(regexPattern, RegexOptions.None, TimeSpan.FromMilliseconds(500)));
                }
                catch (ArgumentException)
                {
                    // Invalid regex pattern, fall back to exact match
                    exactList.Add(pattern);
                }
            }
            else
            {
                // No wildcards, use exact matching
                exactList.Add(pattern);
            }
        }

        _regexPatterns = regexList.ToArray();
        _exactPatterns = exactList.ToArray();
    }

    /// <summary>
    /// Checks if a type name matches any of the configured glob patterns.
    /// </summary>
    /// <param name="typeName">The fully qualified type name to match.</param>
    /// <returns>True if the type name matches any pattern, false otherwise.</returns>
    public bool Matches(string typeName)
    {
        // Check exact matches first (faster)
        foreach (string pattern in _exactPatterns)
        {
            if (typeName == pattern)
            {
                return true;
            }
        }

        // Then check regex patterns
        foreach (Regex regex in _regexPatterns)
        {
            try
            {
                if (regex.IsMatch(typeName))
                {
                    return true;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Continue to next pattern if timeout occurs
            }
        }

        return false;
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
