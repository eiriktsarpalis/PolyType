using System.Text.RegularExpressions;

namespace PolyType.SourceGenerator.Helpers;

/// <summary>
/// Provides glob pattern matching functionality for type names.
/// </summary>
internal sealed class GlobPatternMatcher
{
    private readonly List<Regex> _regexes;
    private readonly string[] _patterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobPatternMatcher"/> class.
    /// </summary>
    /// <param name="patterns">The glob patterns to match against.</param>
    public GlobPatternMatcher(IEnumerable<string> patterns)
    {
        _patterns = patterns.ToArray();
        _regexes = new List<Regex>(_patterns.Length);

        for (int i = 0; i < _patterns.Length; i++)
        {
            if (!string.IsNullOrEmpty(_patterns[i]))
            {
                string regexPattern = ConvertGlobToRegex(_patterns[i]);
                try
                {
                    _regexes.Add(new Regex(regexPattern, RegexOptions.None, TimeSpan.FromMilliseconds(500)));
                }
                catch (ArgumentException)
                {
                    // Invalid regex pattern, will fall back to exact pattern match
                }
            }
        }
    }

    /// <summary>
    /// Checks if a type name matches any of the configured glob patterns.
    /// </summary>
    /// <param name="typeName">The fully qualified type name to match.</param>
    /// <returns>True if the type name matches any pattern, false otherwise.</returns>
    public bool Matches(string typeName)
    {
        // First try regex matches
        foreach (Regex regex in _regexes)
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

        // Fall back to exact pattern match for invalid or empty patterns
        foreach (string pattern in _patterns)
        {
            if (typeName == pattern)
            {
                return true;
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
