using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

#nullable enable

namespace PolyType;

internal static class CommonHelpers
{
    /// <summary>
    /// Atomically exchanges the target reference with the provided value if the target is null.
    /// </summary>
    /// <typeparam name="T">The type to exchange.</typeparam>
    /// <param name="target">The target reference.</param>
    /// <param name="value">The value to populate.</param>
    /// <returns>The value now populating the target.</returns>
    public static T ExchangeIfNull<T>([NotNull] ref T? target, T value) where T : class
    {
        DebugExt.Assert(value is not null);
        return Interlocked.CompareExchange(ref target, value, null) ?? value;
    }

    /// <summary>
    /// Traverses a DAG and returns its nodes applying topological sorting to the result.
    /// </summary>
    /// <typeparam name="T">Element type of the graph.</typeparam>
    /// <param name="entryNode">The entrypoint for the graph.</param>
    /// <param name="getChildren">The function getting the neighbours of the provided node.</param>
    /// <param name="comparer">The comparer determining equality.</param>
    /// <returns>An array of topologically ordered nodes.</returns>
    public static T[] TraverseGraphWithTopologicalSort<T>(T entryNode, Func<T, IReadOnlyCollection<T>> getChildren, IEqualityComparer<T>? comparer = null)
        where T : notnull
    {
        comparer ??= EqualityComparer<T>.Default;

        // Implements Kahn's algorithm.
        // Step 1. Traverse and build the graph, labeling each node with an integer.

        var nodes = new List<T> { entryNode }; // the integer-to-node mapping
        var nodeIndex = new Dictionary<T, int>(comparer) { [entryNode] = 0 }; // the node-to-integer mapping
        var adjacency = new List<bool[]?>(); // the growable adjacency matrix
        var childlessQueue = new Queue<int>(); // the queue of nodes without children or whose children have been visited

        for (int i = 0; i < nodes.Count; i++)
        {
            T next = nodes[i];
            IReadOnlyCollection<T> children = getChildren(next);
            int count = children.Count;

            if (count == 0)
            {
                adjacency.Add(null); // can use null in this row of the adjacency matrix.
                childlessQueue.Enqueue(i);
                continue;
            }

            var adjacencyRow = new bool[Math.Max(nodes.Count, count)];
            foreach (T childNode in children)
            {
                if (!nodeIndex.TryGetValue(childNode, out int index))
                {
                    // this is the first time we're encountering this node.
                    // Assign it an index and append it to the maps.

                    index = nodes.Count;
                    nodeIndex.Add(childNode, index);
                    nodes.Add(childNode);
                }

                // Grow the adjacency row as appropriate.
                if (index >= adjacencyRow.Length)
                {
                    Array.Resize(ref adjacencyRow, index + 1);
                }

                // Set the relevant bit in the adjacency row.
                adjacencyRow[index] = true;
            }

            // Append the row to the adjacency matrix.
            adjacency.Add(adjacencyRow);
        }

        Debug.Assert(childlessQueue.Count > 0, "The graph contains cycles.");

        // Step 2. Build the sorted array, walking from the nodes without children upward.
        var sortedNodes = new T[nodes.Count];
        int idx = sortedNodes.Length;

        do
        {
            int nextIndex = childlessQueue.Dequeue();
            sortedNodes[--idx] = nodes[nextIndex];

            // Iterate over the adjacency matrix, removing any occurrence of nextIndex.
            for (int i = 0; i < adjacency.Count; i++)
            {
                if (adjacency[i] is { } childMap && nextIndex < childMap.Length && childMap[nextIndex])
                {
                    childMap[nextIndex] = false;

                    if (childMap.IndexOf(true) == -1)
                    {
                        // nextIndex was the last child removed from i, add to queue.
                        childlessQueue.Enqueue(i);
                    }
                }
            }
        }
        while (childlessQueue.Count > 0);

        Debug.Assert(idx == 0, "should have populated the entire sortedNodes array.");
        return sortedNodes;
    }

    public static int CombineHashCodes(int h1, int h2)
    {
        // RyuJIT optimizes this to use the ROL instruction
        // Related GitHub pull request: https://github.com/dotnet/coreclr/pull/1830
        uint rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
        return ((int)rol5 + h1) ^ h2;
    }

    /// <summary>
    /// Compares identifiers after normalizing words and acronyms to camel case.
    /// Spaces are preserved and remain significant.
    /// </summary>
    public sealed class CamelCaseInvariantComparer : EqualityComparer<string>
    {
        public static CamelCaseInvariantComparer Instance { get; } = new();

        public override bool Equals(string? left, string? right)
        {
            if (left is null || right is null)
            {
                return left == right;
            }

            if (left.Length != right.Length)
            {
                return false;
            }

            var leftEnumerator = new CamelCaseEnumerator(left.AsSpan());
            var rightEnumerator = new CamelCaseEnumerator(right.AsSpan());

            while (leftEnumerator.MoveNext(out char leftChar))
            {
                if (!rightEnumerator.MoveNext(out char rightChar) || leftChar != rightChar)
                {
                    return false;
                }
            }

            return !rightEnumerator.MoveNext(out _);
        }

        public override int GetHashCode(string text)
        {
            var enumerator = new CamelCaseEnumerator(text.AsSpan());
            int hash = 17;
            while (enumerator.MoveNext(out char value))
            {
                hash = unchecked((hash * 31) + value);
            }

            return hash;
        }

        private ref struct CamelCaseEnumerator(ReadOnlySpan<char> text)
        {
            private readonly ReadOnlySpan<char> _text = text;
            private int _position;
            private WordState _state;

            public bool MoveNext(out char value)
            {
                if (_position < _text.Length)
                {
                    char current = _text[_position++];
                    switch (char.GetUnicodeCategory(current))
                    {
                        case System.Globalization.UnicodeCategory.UppercaseLetter:
                            // The last capital in an acronym starts the next word when followed by lowercase.
                            bool startsWord = _state is WordState.LowercaseLetterOrDigit or WordState.Space ||
                                (_state is WordState.UppercaseLetter && _position < _text.Length && char.IsLower(_text[_position]));
                            value = startsWord ? current : char.ToLowerInvariant(current);
                            _state = WordState.UppercaseLetter;
                            return true;

                        case System.Globalization.UnicodeCategory.LowercaseLetter:
                            value = _state is WordState.Space ? char.ToUpperInvariant(current) : current;
                            _state = WordState.LowercaseLetterOrDigit;
                            return true;

                        case System.Globalization.UnicodeCategory.DecimalDigitNumber:
                            value = current;
                            _state = WordState.LowercaseLetterOrDigit;
                            return true;

                        case System.Globalization.UnicodeCategory.SpaceSeparator:
                            value = current;
                            if (_state is not WordState.NotStarted)
                            {
                                _state = WordState.Space;
                            }

                            return true;

                        default:
                            // Preserve punctuation and UTF-16 surrogates without treating them as letters.
                            value = current;
                            _state = WordState.NotStarted;
                            return true;
                    }
                }

                value = default;
                return false;
            }
        }

        private enum WordState
        {
            NotStarted,
            UppercaseLetter,
            LowercaseLetterOrDigit,
            Space,
        }
    }

    public static EqualityComparer<(T1, T2)> CreateTupleComparer<T1, T2>(IEqualityComparer<T1> left, IEqualityComparer<T2> right)
        => new TupleComparer<T1, T2>(left, right);

    private sealed class TupleComparer<T1, T2>(IEqualityComparer<T1> left, IEqualityComparer<T2> right) : EqualityComparer<(T1, T2)>
    {
        public override bool Equals((T1, T2) x, (T1, T2) y)
            => left.Equals(x.Item1, y.Item1) && right.Equals(y.Item2, y.Item2);

        public override int GetHashCode((T1, T2) obj)
            => CombineHashCodes(
                obj.Item1 is { } item1 ? left.GetHashCode(item1) : 0,
                obj.Item2 is { } item2 ? right.GetHashCode(item2) : 0);
    }

    public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T>? enumerable)
    {
        return enumerable is null or ICollection<T> { Count: 0 } ? Array.Empty<T>() : [.. enumerable];
    }
}