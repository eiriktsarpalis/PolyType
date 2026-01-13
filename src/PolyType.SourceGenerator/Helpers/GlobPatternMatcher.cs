using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace PolyType.SourceGenerator.Helpers;

/// <summary>
/// Provides glob pattern matching functionality for type symbols.
/// </summary>
internal sealed class GlobPatternMatcher
{
    private struct PatternEntry
    {
        public string Pattern;
        public Regex? Regex;
        public AttributeData AttributeData;
        public bool Matched;

        public PatternEntry(string pattern, Regex? regex, AttributeData attributeData)
        {
            Pattern = pattern;
            Regex = regex;
            AttributeData = attributeData;
            Matched = false;
        }
    }

    private readonly PatternEntry[] _patterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobPatternMatcher"/> class.
    /// </summary>
    /// <param name="patterns">The glob patterns with their associated attribute data.</param>
    public GlobPatternMatcher(IEnumerable<(string Pattern, AttributeData AttributeData)> patterns)
    {
        var patternList = new List<PatternEntry>();
        
        foreach ((string pattern, AttributeData attributeData) in patterns)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                // Track empty patterns so we can report warnings for them
                patternList.Add(new PatternEntry(pattern, null, attributeData));
                continue;
            }

            // Check if pattern contains glob wildcard characters
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                // Pattern has wildcards, compile as regex
                string regexPattern = ConvertGlobToRegex(pattern);
                Regex regex = new Regex(regexPattern, RegexOptions.None);
                patternList.Add(new PatternEntry(pattern, regex, attributeData));
            }
            else
            {
                // No wildcards, use exact matching (no regex needed)
                patternList.Add(new PatternEntry(pattern, null, attributeData));
            }
        }
        
        _patterns = patternList.ToArray();
    }

    /// <summary>
    /// Checks if a type symbol matches any of the configured glob patterns.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to match.</param>
    /// <returns>True if the type symbol matches any pattern, false otherwise.</returns>
    public bool Matches(INamedTypeSymbol typeSymbol)
    {
        // Get the fully qualified name without the global:: prefix using Roslyn formatting
        string nameForMatching = typeSymbol.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat);

        for (int i = 0; i < _patterns.Length; i++)
        {
            ref PatternEntry patternEntry = ref _patterns[i];
            
            if (string.IsNullOrEmpty(patternEntry.Pattern))
            {
                continue;
            }

            bool isMatch;
            if (patternEntry.Regex is not null)
            {
                // Pattern has wildcards, use regex
                isMatch = patternEntry.Regex.IsMatch(nameForMatching);
            }
            else
            {
                // No wildcards, use exact matching
                isMatch = nameForMatching == patternEntry.Pattern;
            }

            if (isMatch)
            {
                // Update the matched flag directly via ref
                patternEntry.Matched = true;
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
        foreach (PatternEntry entry in _patterns)
        {
            if (!entry.Matched)
            {
                yield return (entry.Pattern, entry.AttributeData);
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
