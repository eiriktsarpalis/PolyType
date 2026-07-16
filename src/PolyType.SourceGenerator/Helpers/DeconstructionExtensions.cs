namespace PolyType.SourceGenerator.Helpers;

internal static class DeconstructionExtensions
{
    extension<TKey, TValue>(KeyValuePair<TKey, TValue> pair)
    {
        internal void Deconstruct(out TKey key, out TValue value) => (key, value) = (pair.Key, pair.Value);
    }
}
