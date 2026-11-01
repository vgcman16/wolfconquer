using System.Collections.ObjectModel;
using System.Reflection;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    private sealed class PlainMap<K, V> : ICurrent where K : notnull
    {
        private static readonly FieldInfo? Backing = typeof(ReadOnlyDictionary<K, V>)
            .GetField("m_dictionary", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly IReadOnlyDictionary<K, V> source;
        internal readonly Dictionary<K, V> Actual;
        private readonly Dictionary<K, V>.Enumerator witness;
        private readonly KeyValuePair<K, V>[] saved;

        internal PlainMap(IReadOnlyDictionary<K, V> source)
        {
            this.source = source;
            if (source.GetType() == typeof(Dictionary<K, V>)) Actual = (Dictionary<K, V>)source;
            else if (source.GetType() == typeof(ReadOnlyDictionary<K, V>) && Backing is { IsInitOnly: true } &&
                Backing.GetValue(source) is Dictionary<K, V> actual && actual.GetType() == typeof(Dictionary<K, V>)) Actual = actual;
            else throw Invalid();
            if (!ReferenceEquals(Actual.Comparer, EqualityComparer<K>.Default)) throw Invalid();
            witness = Actual.GetEnumerator();
            saved = Actual.ToArray();
            if (saved.Any(pair => pair.Value is null)) throw Invalid();
            if (!IsCurrentPlain()) throw Invalid();
        }

        public bool IsCurrentPlain()
        {
            try
            {
                if (!ReferenceEquals(Actual.Comparer, EqualityComparer<K>.Default) ||
                    !ReferenceEquals(source, Actual) && !ReferenceEquals(Backing!.GetValue(source), Actual)) return false;
                var iterator = witness; int index = 0;
                while (iterator.MoveNext())
                {
                    if (index == saved.Length || !EqualityComparer<K>.Default.Equals(iterator.Current.Key, saved[index].Key) ||
                        !Same(iterator.Current.Value, saved[index++].Value)) return false;
                }
                return index == saved.Length;
            }
            catch (InvalidOperationException) { return false; }
        }
    }

    private sealed class PlainList<T> : ICurrent
    {
        private static readonly FieldInfo? Backing = typeof(ReadOnlyCollection<T>)
            .GetField("list", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly IReadOnlyList<T> source;
        private readonly T[]? array;
        private readonly List<T>? list;
        private readonly List<T>.Enumerator iterator;
        private readonly T[] saved;

        internal PlainList(IReadOnlyList<T> source)
        {
            this.source = source ?? throw Invalid();
            object actual = source;
            if (source.GetType() == typeof(ReadOnlyCollection<T>) && Backing is { IsInitOnly: true })
                actual = Backing.GetValue(source) ?? throw Invalid();
            if (actual.GetType() == typeof(T[])) array = (T[])actual;
            else if (actual.GetType() == typeof(List<T>)) { list = (List<T>)actual; iterator = list.GetEnumerator(); }
            else throw Invalid();
            saved = array is not null ? (T[])array.Clone() : list!.ToArray();
            if (saved.Any(value => value is null)) throw Invalid();
            if (!IsCurrentPlain()) throw Invalid();
        }

        public bool IsCurrentPlain()
        {
            try
            {
                if (!ReferenceEquals(source, array) && !ReferenceEquals(source, list) &&
                    !ReferenceEquals(Backing!.GetValue(source), (object?)array ?? list)) return false;
                if (array is not null)
                {
                    if (array.Length != saved.Length) return false;
                    for (int index = 0; index < saved.Length; index++) if (!Same(array[index], saved[index])) return false;
                    return true;
                }
                var current = iterator; int at = 0;
                while (current.MoveNext()) if (at == saved.Length || !Same(current.Current, saved[at++])) return false;
                return at == saved.Length;
            }
            catch (InvalidOperationException) { return false; }
        }
    }

    private static bool Same<T>(T left, T right) => typeof(T).IsValueType
        ? EqualityComparer<T>.Default.Equals(left, right) : ReferenceEquals(left, right);

    // A grouped Magic lookup reads EVERY level discriminator but only the
    // selected row's complete payload. Do not snapshot all57 MythSoul payloads.
    private sealed class FieldAt : ICurrent
    {
        private static readonly FieldInfo? Backing = typeof(ReadOnlyCollection<string>)
            .GetField("list", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly IReadOnlyList<string> source;
        private readonly string[]? array;
        private readonly List<string>? list;
        private readonly List<string>.Enumerator witness;
        private readonly int count, index;
        private readonly string value;
        internal FieldAt(IReadOnlyList<string> source, int index)
        {
            this.source = source; this.index = index;
            object actual = source;
            if (source.GetType() == typeof(ReadOnlyCollection<string>) && Backing is { IsInitOnly: true })
                actual = Backing.GetValue(source) ?? throw Invalid();
            if (actual.GetType() == typeof(string[])) array = (string[])actual;
            else if (actual.GetType() == typeof(List<string>)) { list = (List<string>)actual; witness = list.GetEnumerator(); }
            else throw Invalid();
            count = array?.Length ?? list!.Count;
            if (index >= count) throw Invalid();
            value = array is not null ? array[index] : list![index];
            if (value is null) throw Invalid();
        }
        internal bool IsCurrentFor(IReadOnlyList<string> current) => ReferenceEquals(current, source) && IsCurrentPlain();
        public bool IsCurrentPlain()
        {
            try
            {
                if (!ReferenceEquals(source, array) && !ReferenceEquals(source, list) &&
                    !ReferenceEquals(Backing!.GetValue(source), (object?)array ?? list)) return false;
                if (list is not null) { var iterator = witness; iterator.MoveNext(); }
                return count == (array?.Length ?? list!.Count) &&
                    ReferenceEquals(value, array is not null ? array[index] : list![index]);
            }
            catch (InvalidOperationException) { return false; }
        }
    }
}
