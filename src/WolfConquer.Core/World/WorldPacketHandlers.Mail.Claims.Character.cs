using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryProjectMailboxClaimCharacter(LegacyMailboxClaimPlan plan, out byte[] bytes)
    {
        var before = plan.Baseline.Owner.Expected;
        var after = plan.Updated;
        bytes = plan.Baseline.Character.Bytes;
        if ((before with { Character = after.Character, PlayerItems = after.PlayerItems }) != after ||
            (before.Character with { Money = after.Character.Money, ConquerPoints = after.Character.ConquerPoints,
                Attributes = after.Character.Attributes }) != after.Character ||
            !LegacyTradeEscrowClaims.TryRead(after.Character.Attributes, out var claim, out _) ||
            after.Character.Money < before.Character.Money || after.Character.ConquerPoints < before.Character.ConquerPoints ||
            after.Character.ConquerPoints > uint.MaxValue) return false;
        // Keep enough room to refund an already genuine, unchanged Trade escrow.
        _ = checked(after.Character.Money + (claim?.Money ?? 0));
        if (after.Character.ConquerPoints + (claim?.ConquerPoints ?? 0) > uint.MaxValue) return false;
        var changed = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (after.Character.Money != before.Character.Money) changed.Add("Money", after.Character.Money);
        if (after.Character.ConquerPoints != before.Character.ConquerPoints) changed.Add("ConquerPoints", after.Character.ConquerPoints);
        if (!MailboxClaimAttributesMatch(before.Character.Attributes, after.Character.Attributes, changed)) return false;
        if (changed.Count == 0) return true;
        var text = CharacterSnapshotFileEncoding.Decode(bytes, out var encoding);
        var parts = Regex.Split(text, "(\r\n|\r|\n)");
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inCharacter = false;
        for (var index = 0; index < parts.Length; index += 2)
        {
            var line = parts[index];
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            { inCharacter = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase); continue; }
            if (!inCharacter || trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;
            var equals = line.IndexOf('=');
            if (equals <= 0 || !changed.TryGetValue(line[..equals].Trim(), out var value)) continue;
            if (!found.Add(line[..equals].Trim())) return false;
            parts[index] = line[..(equals + 1)] + value.ToString(CultureInfo.InvariantCulture);
        }
        if (found.Count != changed.Count) return false;
        var projected = string.Concat(parts);
        if (!string.Equals(encoding.GetString(encoding.GetBytes(projected)), projected, StringComparison.Ordinal)) return false;
        bytes = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (bytes.Length > CharacterSnapshotFileBaseline.MaximumBytes) return false;
        var parsed = CharacterSnapshotParser.ParseText(projected, after.Character.SourceFile);
        return parsed.Uid == after.Character.Uid && parsed.Money == after.Character.Money &&
            parsed.ConquerPoints == after.Character.ConquerPoints &&
            LegacyTradeEscrowClaims.TryRead(parsed.Attributes, out var diskClaim, out _) && diskClaim == claim;
    }

    private static bool MailboxClaimAttributesMatch(IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after, IReadOnlyDictionary<string, long> changed)
    {
        var expected = new Dictionary<string, string>(before, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changed) expected[key] = value.ToString(CultureInfo.InvariantCulture);
        var actual = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in after)
            if (key is null || value is null || !actual.TryAdd(key, value)) return false;
        return expected.Count == actual.Count && expected.All(pair =>
            actual.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.Ordinal));
    }
}
