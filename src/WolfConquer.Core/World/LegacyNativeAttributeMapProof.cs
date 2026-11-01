using System.Collections.ObjectModel;
using System.Reflection;

namespace WolfConquer.Core.World;

// Data witness only. Resource/action parsing and admission remain with their
// separate owners; no caller validator or publication capability is accepted.
internal sealed class LegacyNativeAttributeMapProof
{
    private static readonly FieldInfo? ReadOnlyBacking = typeof(ReadOnlyDictionary<string, string>)
        .GetField("m_dictionary", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly IReadOnlyDictionary<string, string> source;
    private readonly Dictionary<string, string> backing;
    private readonly IEqualityComparer<string> comparer;
    private readonly Dictionary<string, string>.Enumerator witness;
    private readonly KeyValuePair<string, string>[] rows;
    internal IReadOnlyDictionary<string, string> Fields { get; }
    internal IEqualityComparer<string> CapturedComparer => comparer;

    internal LegacyNativeAttributeMapProof(IReadOnlyDictionary<string, string> source) : this(source, false) { }
    internal static LegacyNativeAttributeMapProof CaptureRaw(IReadOnlyDictionary<string, string> source) => new(source, true);

    private LegacyNativeAttributeMapProof(IReadOnlyDictionary<string, string> source, bool preserveSourceComparer)
    {
        this.source = source ?? throw Invalid();
        if (source.GetType() == typeof(Dictionary<string, string>))
            backing = (Dictionary<string, string>)source;
        else if (source.GetType() == typeof(ReadOnlyDictionary<string, string>) &&
            ReadOnlyBacking is { IsInitOnly: true } &&
            ReadOnlyBacking.GetValue(source) is Dictionary<string, string> actual &&
            actual.GetType() == typeof(Dictionary<string, string>)) backing = actual;
        else throw Invalid();
        comparer = backing.Comparer;
        if (!ReferenceEquals(comparer, StringComparer.Ordinal) && !ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase) &&
            !ReferenceEquals(comparer, EqualityComparer<string>.Default)) throw Invalid();
        witness = backing.GetEnumerator();
        var captured = new List<KeyValuePair<string, string>>();
        var fields = new Dictionary<string, string>(preserveSourceComparer ? comparer : StringComparer.OrdinalIgnoreCase);
        var iterator = witness;
        while (iterator.MoveNext())
        {
            var pair = iterator.Current;
            if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !fields.TryAdd(pair.Key, pair.Value)) throw Invalid();
            captured.Add(pair);
        }
        rows = captured.ToArray();
        Fields = new ReadOnlyDictionary<string, string>(fields);
    }

    internal bool IsCurrentPlain()
    {
        try
        {
            if (!ReferenceEquals(backing.Comparer, comparer) ||
                !ReferenceEquals(source, backing) && !ReferenceEquals(ReadOnlyBacking!.GetValue(source), backing)) return false;
            var iterator = witness; var index = 0;
            while (iterator.MoveNext())
            {
                if (index == rows.Length || iterator.Current.Key != rows[index].Key ||
                    iterator.Current.Value != rows[index++].Value) return false;
            }
            return index == rows.Length;
        }
        catch (InvalidOperationException) { return false; }
    }

    private static InvalidOperationException Invalid() => new("ordinaryNativeAttributesInvalidOrAmbiguous");
}
