using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    private sealed class CatalogRead
    {
        private readonly List<ICatalogCurrent> reads = [];
        private int budget = 16 * 1024 * 1024;
        internal bool IsCurrent() => reads.All(read => read.IsCurrent());
        internal T[] Rows<T>(IReadOnlyList<T> source)
        {
            var read = new CatalogRows<T>(source, budget);
            budget -= read.Saved.Length;
            reads.Add(read);
            return read.Saved;
        }
        internal uint[] Numbers(IReadOnlyList<string> source, int count)
        {
            var fields = Rows(source);
            if (fields.Length != count && (fields.Length != count + 1 || fields[^1] != "")) throw InvalidCatalog();
            return fields.Take(count).Select(Number).ToArray();
        }
        internal static uint Number(string value) => uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

        internal bool Unique<K, T>(IReadOnlyDictionary<K, T> source, T[] rows, Func<T, K> key) where K : notnull where T : class
        {
            var map = new CatalogMap<K, T>(source); reads.Add(map);
            var expected = new Dictionary<K, T>();
            foreach (var row in rows) if (!expected.TryAdd(key(row), row)) return false;
            return map.Saved.Length == expected.Count && map.Saved.All(pair =>
                expected.TryGetValue(pair.Key, out var row) && ReferenceEquals(row, pair.Value));
        }

        internal bool Grouped<K, T>(IReadOnlyDictionary<K, IReadOnlyList<T>> source, T[] rows, Func<T, K> key)
            where K : notnull where T : class
        {
            var map = new CatalogMap<K, IReadOnlyList<T>>(source); reads.Add(map);
            var expected = rows.GroupBy(key).ToDictionary(group => group.Key, group => group.ToArray());
            if (map.Saved.Length != expected.Count) return false;
            foreach (var pair in map.Saved)
            {
                if (!expected.TryGetValue(pair.Key, out var group)) return false;
                var actual = Rows(pair.Value);
                if (actual.Length != group.Length) return false;
                for (int index = 0; index < actual.Length; index++) if (!ReferenceEquals(actual[index], group[index])) return false;
            }
            return true;
        }
    }

    private interface ICatalogCurrent { bool IsCurrent(); }

    private sealed class CatalogRows<T> : ICatalogCurrent
    {
        private static readonly FieldInfo? Backing = typeof(ReadOnlyCollection<T>).GetField("list", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly IReadOnlyList<T> source;
        private readonly T[]? array;
        private readonly List<T>? list;
        private readonly List<T>.Enumerator witness;
        internal readonly T[] Saved;
        internal CatalogRows(IReadOnlyList<T> source, int budget)
        {
            this.source = source ?? throw InvalidCatalog();
            object actual = source;
            if (source.GetType() == typeof(ReadOnlyCollection<T>) && Backing is { IsInitOnly: true })
                actual = Backing.GetValue(source) ?? throw InvalidCatalog();
            if (actual.GetType() == typeof(T[])) array = (T[])actual;
            else if (actual.GetType() == typeof(List<T>)) { list = (List<T>)actual; witness = list.GetEnumerator(); }
            else throw InvalidCatalog();
            int count = array?.Length ?? list!.Count;
            if (count > 262_144 || count > budget) throw InvalidCatalog();
            Saved = array is not null ? (T[])array.Clone() : list!.ToArray();
            if (Saved.Any(value => value is null) || !IsCurrent()) throw InvalidCatalog();
        }
        public bool IsCurrent()
        {
            try
            {
                if (!ReferenceEquals(source, array) && !ReferenceEquals(source, list) &&
                    !ReferenceEquals(Backing!.GetValue(source), (object?)array ?? list)) return false;
                if (array is not null)
                {
                    if (array.Length != Saved.Length) return false;
                    for (int index = 0; index < Saved.Length; index++) if (!SameCatalogValue(array[index], Saved[index])) return false;
                    return true;
                }
                var iterator = witness; int at = 0;
                while (iterator.MoveNext()) if (at == Saved.Length || !SameCatalogValue(iterator.Current, Saved[at++])) return false;
                return at == Saved.Length;
            }
            catch (InvalidOperationException) { return false; }
        }
    }

    private sealed class CatalogMap<K, V> : ICatalogCurrent where K : notnull
    {
        private static readonly FieldInfo? Backing = typeof(ReadOnlyDictionary<K, V>).GetField("m_dictionary", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly IReadOnlyDictionary<K, V> source;
        private readonly Dictionary<K, V> actual;
        private readonly Dictionary<K, V>.Enumerator witness;
        internal readonly KeyValuePair<K, V>[] Saved;
        internal CatalogMap(IReadOnlyDictionary<K, V> source)
        {
            this.source = source ?? throw InvalidCatalog();
            if (source.GetType() == typeof(Dictionary<K, V>)) actual = (Dictionary<K, V>)source;
            else if (source.GetType() == typeof(ReadOnlyDictionary<K, V>) && Backing is { IsInitOnly: true } &&
                Backing.GetValue(source) is Dictionary<K, V> map && map.GetType() == typeof(Dictionary<K, V>)) actual = map;
            else throw InvalidCatalog();
            if (!ReferenceEquals(actual.Comparer, EqualityComparer<K>.Default) || actual.Count > 262_144) throw InvalidCatalog();
            witness = actual.GetEnumerator(); Saved = actual.ToArray();
            if (Saved.Any(pair => pair.Value is null) || !IsCurrent()) throw InvalidCatalog();
        }
        public bool IsCurrent()
        {
            try
            {
                if (!ReferenceEquals(actual.Comparer, EqualityComparer<K>.Default) ||
                    !ReferenceEquals(source, actual) && !ReferenceEquals(Backing!.GetValue(source), actual)) return false;
                var iterator = witness; int index = 0;
                while (iterator.MoveNext())
                {
                    if (index == Saved.Length || !EqualityComparer<K>.Default.Equals(iterator.Current.Key, Saved[index].Key) ||
                        !SameCatalogValue(iterator.Current.Value, Saved[index++].Value)) return false;
                }
                return index == Saved.Length;
            }
            catch (InvalidOperationException) { return false; }
        }
    }

    private static bool SameCatalogValue<T>(T left, T right) => typeof(T).IsValueType
        ? EqualityComparer<T>.Default.Equals(left, right) : ReferenceEquals(left, right);
    private static InvalidDataException InvalidCatalog() => new("nativeCombatGearCatalogInvalidOrChanged");
}
