using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

/// <summary>Source per-registration RemoveOnDead policy, not a power/stat reset.</summary>
public static class LegacyStatusFlagDeathRegistration
{
    public const string AttributePrefix = "LegacyStatusFlagRemoveOnDead.";

    public static string Key(int flag) => AttributePrefix + flag.ToString(CultureInfo.InvariantCulture);

    public static CharacterSnapshot CaptureAddFlag(
        CharacterSnapshot before, CharacterSnapshot after, int flag, bool removeOnDead)
    {
        // Direct AddFlag/TryAdd rejects a duplicate and preserves its policy.
        if (!LegacyPlayerDeathState.HasStatusFlag(before.Attributes, flag))
            return CaptureAddSpellFlag(after, flag, removeOnDead);
        var key = Key(flag);
        if (!before.Attributes.TryGetValue(key, out var original))
            return Forget(after, flag); // Unknown before remains unknown; no guessed false/true.
        if (after.Attributes.TryGetValue(key, out var attempted) && attempted == original)
            return after;
        var attributes = new Dictionary<string, string>(after.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [key] = original,
        };
        return after with { Attributes = attributes };
    }

    public static CharacterSnapshot CaptureAddSpellFlag(
        CharacterSnapshot after, int flag, bool removeOnDead)
    {
        // Player.AddSpellFlag first removes the old registration, then AddFlag.
        if (!LegacyPlayerDeathState.HasStatusFlag(after.Attributes, flag)) return after;
        var attributes = new Dictionary<string, string>(after.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [Key(flag)] = removeOnDead ? "true" : "false",
        };
        return after with { Attributes = attributes };
    }

    public static bool TryRead(CharacterSnapshot character, int flag, out bool removeOnDead)
    {
        removeOnDead = false;
        return character.Attributes.TryGetValue(Key(flag), out var raw) &&
            bool.TryParse(raw, out removeOnDead);
    }

    public static CharacterSnapshot Forget(CharacterSnapshot character, int flag)
    {
        if (flag < 0 || !character.Attributes.ContainsKey(Key(flag))) return character;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(Key(flag));
        return character with { Attributes = attributes };
    }

    public static CharacterSnapshot PrepareAdd(CharacterSnapshot character, int flag) =>
        flag < 0 || LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag) ? character : Forget(character, flag);

    public static void PrepareAdd(Dictionary<string, string> attributes, int flag)
    {
        if (flag >= 0 && !LegacyPlayerDeathState.HasStatusFlag(attributes, flag)) attributes.Remove(Key(flag));
    }

    public static void ForgetAll(Dictionary<string, string> attributes)
    {
        foreach (var key in attributes.Keys.Where(key => key.StartsWith(AttributePrefix, StringComparison.Ordinal)).ToArray())
            attributes.Remove(key);
    }

    public static IReadOnlyList<int> GetDeathRemovableFlags(CharacterSnapshot character)
    {
        var flags = new List<int>();
        foreach (var (key, raw) in character.Attributes)
        {
            if (!key.StartsWith(AttributePrefix, StringComparison.Ordinal) ||
                !int.TryParse(key.AsSpan(AttributePrefix.Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var flag) || flag < 0 ||
                key != Key(flag) || !bool.TryParse(raw, out var remove) || !remove)
                continue;
            flags.Add(flag);
        }
        return flags;
    }
}
