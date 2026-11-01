using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Runtime stance, not a saved scalar grant or an enum-based permission.
internal static class LegacyActionState
{
    private static readonly string[] KnownNames = Enum.GetNames<LegacyNativePlayerMapFlags.ConquerAction>();

    internal static uint RequireCurrent(CharacterSnapshot character)
    {
        var proof = new NativeValueProof(character);
        if (!proof.IsCurrentPlain()) throw Invalid();
        return proof.Value;
    }

    internal static WorldSession ForAdmission(WorldSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var proof = new NativeValueProof(session.Character, allowAbsent: true);
        var fields = new Dictionary<string, string>(proof.CapturedComparer);
        foreach (var pair in proof.CapturedFields)
            if (!string.Equals(pair.Key, "Action", StringComparison.OrdinalIgnoreCase)) fields.Add(pair.Key, pair.Value);
        // Present source-enum text is explicit managed compatibility. Native
        // birth is None0; server state is never narrowed to its wire echo.
        fields.Add("Action", proof.Value.ToString(CultureInfo.InvariantCulture));
        var final = session with
        {
            Character = session.Character with { Attributes = new ReadOnlyDictionary<string, string>(fields) },
        };
        if (!proof.IsCurrentPlain()) throw Invalid();
        return final;
    }

    private static bool TryParse(string text, out uint value)
    {
        if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
        // Enum.TryParse alone accepts comma combinations, even without Flags.
        // Only one exact known name (case-insensitive) is compatibility input.
        foreach (var name in KnownNames)
            if (string.Equals(text, name, StringComparison.OrdinalIgnoreCase))
            {
                value = (uint)Enum.Parse<LegacyNativePlayerMapFlags.ConquerAction>(name);
                return true;
            }
        return false;
    }

    internal sealed class NativeValueProof
    {
        private readonly LegacyNativeAttributeMapProof attributes;
        internal uint Value { get; }
        internal IReadOnlyDictionary<string, string> CapturedFields => attributes.Fields;
        internal IEqualityComparer<string> CapturedComparer => attributes.CapturedComparer;

        internal NativeValueProof(CharacterSnapshot character, bool allowAbsent = false)
        {
            attributes = LegacyNativeAttributeMapProof.CaptureRaw(character?.Attributes ?? throw Invalid());
            var matches = attributes.Fields.Where(pair => string.Equals(pair.Key, "Action", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1) throw Invalid();
            bool present = matches.Length == 1;
            var text = present ? matches[0].Value : null;
            uint value = 0;
            if (!present && !allowAbsent || present && !TryParse(text!, out value)) throw Invalid();
            Value = value;
        }

        internal bool IsCurrentPlain() => attributes.IsCurrentPlain();
    }

    private static InvalidOperationException Invalid() => new("ordinaryActionStateInvalidOrAmbiguous");
}
