using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyWindWalkerStatusProjection
{
    internal const int Flag = 194;
    internal const int Word = Flag / 32;
    internal const ulong Mask = 1UL << (Flag % 32);

    internal static ulong[] Read(CharacterSnapshot character)
    {
        ulong[]? captured = null;
        foreach (var name in new[] { "StatusFlags", "BitVector" })
        {
            var aliases = character.Attributes.Where(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (aliases.Length > 1) throw Invalid();
            if (aliases.Length == 0) continue;
            var words = Parse(aliases[0].Value);
            if (captured is not null && !captured.AsSpan().SequenceEqual(words)) throw Invalid();
            captured = words;
        }
        return captured ?? new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
    }

    private static ulong[] Parse(string value)
    {
        if (value is null) throw Invalid();
        var words = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
        if (string.IsNullOrWhiteSpace(value)) return words;
        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        int length = parts.Length;
        if (parts[^1].Length == 0) length--;
        if (length > words.Length) throw Invalid();
        for (int index = 0; index < length; index++)
            if (!ulong.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out words[index])) throw Invalid();
        return words;
    }

    internal static CharacterSnapshot Apply(CharacterSnapshot character,
        LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot native) =>
        Apply(character, native.Present, native.RemoveOnDeath);

    // DATA projection only; desired flags cannot create or consume a native
    // prepared change. The real publication overload supplies its captured row.
    internal static CharacterSnapshot Apply(CharacterSnapshot character, bool desired, bool removeOnDeath)
    {
        var words = Read(character);
        bool present = (words[Word] & Mask) != 0;
        string key = LegacyStatusFlagDeathRegistration.Key(Flag);
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
        var policyKeys = proof.Fields.Keys.Where(name => name.Equals(key, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (policyKeys.Length > 1) throw Invalid();
        bool policyMatches = desired
            ? policyKeys.Length == 1 && proof.Fields[policyKeys[0]] == (removeOnDeath ? "true" : "false")
            : policyKeys.Length == 0;
        if (!proof.IsCurrentPlain()) throw Invalid();
        if (present == desired && policyMatches) return character;
        words[Word] = desired ? words[Word] | Mask : words[Word] & ~Mask;
        var attributes = new Dictionary<string, string>(proof.Fields, proof.CapturedComparer);
        var aliases = attributes.Keys.Where(name => name.Equals("StatusFlags", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("BitVector", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (aliases.Length == 0) aliases = ["StatusFlags"];
        foreach (var alias in aliases) attributes[alias] = string.Join('/', words) + "/";
        if (desired) attributes[policyKeys.Length == 0 ? key : policyKeys[0]] = removeOnDeath ? "true" : "false";
        else if (policyKeys.Length != 0) attributes.Remove(policyKeys[0]);
        if (!proof.IsCurrentPlain()) throw Invalid();
        return character with { Attributes = attributes };
    }

    private static InvalidDataException Invalid() => new("WindWalker status aliases are malformed or disagree.");
}
