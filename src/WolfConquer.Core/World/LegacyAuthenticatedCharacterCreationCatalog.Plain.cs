namespace WolfConquer.Core.World;

internal sealed partial class LegacyAuthenticatedCharacterCreationCatalog
{
    // Only these concrete BCL stores are read; no arbitrary collection callouts.
    private sealed class PlainList<T>
    {
        private readonly IReadOnlyList<T> source;
        internal T[] Values { get; }
        internal PlainList(IReadOnlyList<T> source)
        {
            if (source is not T[] && source is not List<T>) throw Invalid();
            this.source = source; Values = source.ToArray();
            if (Values.Any(value => value is null)) throw Invalid();
        }
        internal bool IsCurrent(IReadOnlyList<T> actual)
        {
            if (!ReferenceEquals(source, actual) || actual.Count != Values.Length) return false;
            for (int index = 0; index < Values.Length; index++)
                if (typeof(T).IsValueType ? !EqualityComparer<T>.Default.Equals(actual[index], Values[index]) :
                    !ReferenceEquals(actual[index], Values[index])) return false;
            return true;
        }
    }

    private sealed class PlainIndex<T> where T : class
    {
        private readonly Dictionary<int, T> source;
        private readonly KeyValuePair<int, T>[] entries;
        internal PlainIndex(IReadOnlyDictionary<int, T> source)
        {
            this.source = source as Dictionary<int, T> ?? throw Invalid();
            if (!ReferenceEquals(this.source.Comparer, EqualityComparer<int>.Default)) throw Invalid();
            entries = this.source.ToArray();
            if (entries.Any(row => row.Value is null)) throw Invalid();
        }
        internal bool TryGet(int key, out T? value) => source.TryGetValue(key, out value);
        internal bool IsCurrent(IReadOnlyDictionary<int, T> actual) => ReferenceEquals(source, actual) && source.Count == entries.Length &&
            entries.All(row => source.TryGetValue(row.Key, out var value) && ReferenceEquals(value, row.Value));
    }
}
