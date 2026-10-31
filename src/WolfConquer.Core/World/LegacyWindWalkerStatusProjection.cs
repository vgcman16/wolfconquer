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
        LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot native)
    {
        var words = Read(character);
        bool present = (words[Word] & Mask) != 0;
        string key = LegacyStatusFlagDeathRegistration.Key(Flag);
        bool policyMatches = native.Present
            ? character.Attributes.TryGetValue(key, out var policy) && policy == (native.RemoveOnDeath ? "true" : "false")
            : !character.Attributes.ContainsKey(key);
        if (present == native.Present && policyMatches) return character;
        words[Word] = native.Present ? words[Word] | Mask : words[Word] & ~Mask;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        var aliases = attributes.Keys.Where(name => name.Equals("StatusFlags", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("BitVector", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (aliases.Length == 0) aliases = ["StatusFlags"];
        foreach (var alias in aliases) attributes[alias] = string.Join('/', words) + "/";
        if (native.Present) attributes[key] = native.RemoveOnDeath ? "true" : "false";
        else attributes.Remove(key);
        return character with { Attributes = attributes };
    }

    private static InvalidDataException Invalid() => new("WindWalker status aliases are malformed or disagree.");
}
