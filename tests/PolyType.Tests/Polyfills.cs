internal static class Polyfills
{
#if !NET
    internal static bool TryGetNonEnumeratedCount<T>(this IEnumerable<T> values, out int count)
    {
        if (values is ICollection<T> collection)
        {
            count = collection.Count;
            return true;
        }

        if (values is IReadOnlyCollection<T> readOnlyCollection)
        {
            count = readOnlyCollection.Count;
            return true;
        }

        count = -1;
        return false;
    }
#endif
}
