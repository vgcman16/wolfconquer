using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Runtime resource state, not a saved Character scalar or a stamina grant.
internal static class LegacyStaminaState
{
    private static readonly FieldInfo? ReadOnlyBacking = typeof(ReadOnlyDictionary<string, string>)
        .GetField("m_dictionary", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static ushort RequireCurrent(CharacterSnapshot character)
    {
        var proof = new NativeValueProof(character);
        if (!proof.IsCurrentPlain()) throw Invalid();
        return proof.Value;
    }

    internal static WorldSession ForAdmission(WorldSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var proof = new NativeValueProof(session.Character, allowAbsent: true);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in proof.CapturedFields)
            if (!string.Equals(pair.Key, "Stamina", StringComparison.OrdinalIgnoreCase)) fields.Add(pair.Key, pair.Value);
        // Native birth is zero. Keeping a valid present saved value is the
        // explicit managed compatibility policy, not native loader behavior.
        fields.Add("Stamina", proof.HasValue ? proof.Text! : "0");
        var final = session with
        {
            Character = session.Character with { Attributes = new ReadOnlyDictionary<string, string>(fields) },
        };
        if (!proof.IsCurrentPlain()) throw Invalid();
        return final;
    }

    // A BCL wrapper alone is insufficient: arbitrary IDictionary backing can
    // expose real dictionary enumeration but disagree through TryGetValue.
    // Read only the exact BCL backing; unknown runtime layouts refuse.
    internal sealed class NativeValueProof
    {
        private readonly IReadOnlyDictionary<string, string> source;
        private readonly Dictionary<string, string> backing;
        private readonly IEqualityComparer<string> comparer;
        private readonly Dictionary<string, string>.Enumerator witness;
        private readonly KeyValuePair<string, string>[] rows;
        internal ushort Value { get; }
        internal bool HasValue { get; }
        internal string? Text { get; }
        internal IReadOnlyDictionary<string, string> CapturedFields { get; }

        internal NativeValueProof(CharacterSnapshot character, bool allowAbsent = false)
        {
            source = character?.Attributes ?? throw Invalid();
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
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var iterator = witness;
            while (iterator.MoveNext())
            {
                var pair = iterator.Current;
                if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !fields.TryAdd(pair.Key, pair.Value)) throw Invalid();
                captured.Add(pair);
            }
            HasValue = fields.TryGetValue("Stamina", out var text);
            ushort value = 0;
            if (!HasValue && !allowAbsent || HasValue &&
                !ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) throw Invalid();
            Value = value;
            Text = text; rows = captured.ToArray();
            CapturedFields = new ReadOnlyDictionary<string, string>(fields);
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
    }

    private static InvalidOperationException Invalid() => new("ordinaryStaminaStateInvalidOrAmbiguous");
}
