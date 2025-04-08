namespace PolyType.SourceGenerator.Helpers;

internal static class DeconstructionExtensions
{
    internal static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value) => (key, value) = (pair.Key, pair.Value);
}
