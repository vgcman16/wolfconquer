using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Closed predicates for later integration at the actual generic Store and
// file-writer boundaries. These do not authorize a continuation or a write.
// Both sides must be clear: a generic mutation cannot insert, erase, modify,
// or carry a pending marker while its private continuation is unresolved.
internal static class LegacyExperienceContinuationFence
{
    internal static bool AllowsOrdinaryPublication(WorldSession? before, WorldSession? after)
    {
        try
        {
            return (before is null || LegacyExperienceContinuationCodec.ReadCharacter(before.Character, before.AccountId) is null) &&
                (after is null || LegacyExperienceContinuationCodec.ReadCharacter(after.Character, after.AccountId) is null);
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        { return false; }
    }

    // Call with the exact image acquired by the actual Character writer while
    // it holds the shared file coordinator; a parsed holder is not a file proof.
    internal static bool AllowsOrdinaryCharacterImage(byte[] image, long accountId, uint characterUid)
    {
        try { return LegacyExperienceContinuationCodec.ReadCharacterImage(image, accountId, characterUid) is null; }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        { return false; }
    }

    // Generic section writers do not have an account/UID receipt. They must
    // refuse any marker presence, including malformed or duplicate presence.
    internal static bool ContainsCharacterMarker(IEnumerable<string> lines)
    {
        bool character = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                character = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
            else if (character && trimmed.Length != 0 && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                int equals = trimmed.IndexOf('=');
                string key = equals < 0 ? trimmed : trimmed[..equals].Trim();
                if (key.Equals(LegacyExperienceContinuationCodec.Key, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    internal static bool RecoveryProtectedLinesUnchanged(IEnumerable<string> before, IEnumerable<string> after)
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            LegacyExperienceContinuationCodec.Key, "UID", "Level", "Experience", "Class", "Reborn",
            "Strength", "Agility", "Vitality", "Vitaliti", "Spirit", "Atributes", "Attributes", "AttributePoints",
            "MinHitPoints", "MaxHitpoints", "MaxLife", "MinMana", "MaxMana",
        };
        static IEnumerable<string> Select(IEnumerable<string> lines, HashSet<string> keys)
        {
            bool character = false;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                    character = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                else if (character && trimmed.Length != 0 && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
                {
                    int equals = line.IndexOf('=');
                    string key = equals < 0 ? trimmed : line[..equals].Trim();
                    if (keys.Contains(key)) yield return line;
                }
            }
        }
        return Select(before, owned).SequenceEqual(Select(after, owned), StringComparer.Ordinal);
    }
}
