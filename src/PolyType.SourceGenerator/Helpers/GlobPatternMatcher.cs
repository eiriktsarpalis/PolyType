using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace PolyType.SourceGenerator.Helpers;

/// <summary>
/// Provides glob pattern matching functionality for type symbols.
/// </summary>
internal sealed class GlobPatternMatcher
{
    private readonly List<(string Pattern, Regex? Regex, AttributeData AttributeData, bool Matched)> _patterns = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobPatternMatcher"/> class.
    /// </summary>
    /// <param name="patterns">The glob patterns with their associated attribute data.</param>
    public GlobPatternMatcher(IEnumerable<(string Pattern, AttributeData AttributeData)> patterns)
    {
        foreach ((string pattern, AttributeData attributeData) in patterns)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                // Track empty patterns so we can report warnings for them
                _patterns.Add((pattern, null, attributeData, Matched: false));
                continue;
            }

            // Check if pattern contains glob wildcard characters
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                // Pattern has wildcards, compile as regex
                string regexPattern = ConvertGlobToRegex(pattern);
                Regex regex = new Regex(regexPattern, RegexOptions.None);
                _patterns.Add((pattern, regex, attributeData, Matched: false));
            }
            else
            {
                // No wildcards, use exact matching (no regex needed)
                _patterns.Add((pattern, null, attributeData, Matched: false));
            }
        }
    }

    /// <summary>
    /// Checks if a type symbol matches any of the configured glob patterns.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to match.</param>
    /// <returns>True if the type symbol matches any pattern, false otherwise.</returns>
    public bool Matches(INamedTypeSymbol typeSymbol)
    {
        // Get the fully qualified name and strip "global::" prefix if present
        string fullyQualifiedName = typeSymbol.GetFullyQualifiedName();
        string nameForMatching = fullyQualifiedName.StartsWith("global::", StringComparison.Ordinal)
            ? fullyQualifiedName.Substring(8)
            : fullyQualifiedName;

        for (int i = 0; i < _patterns.Count; i++)
        {
            (string pattern, Regex? regex, AttributeData _, bool matched) = _patterns[i];
            
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            bool isMatch;
            if (regex is not null)
            {
                // Pattern has wildcards, use regex
                isMatch = regex.IsMatch(nameForMatching);
            }
            else
            {
                // No wildcards, use exact matching
                isMatch = nameForMatching == pattern;
            }

            if (isMatch)
            {
                if (!matched)
                {
                    // Update the matched flag
                    _patterns[i] = (pattern, regex, _patterns[i].AttributeData, Matched: true);
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the patterns that did not match any types, along with their attribute data.
    /// </summary>
    /// <returns>An enumerable of tuples containing unmatched patterns and their attribute data.</returns>
    public IEnumerable<(string Pattern, AttributeData AttributeData)> GetUnmatchedPatterns()
    {
        foreach ((string pattern, Regex? _, AttributeData attributeData, bool matched) in _patterns)
        {
            if (!matched)
            {
                yield return (pattern, attributeData);
            }
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
