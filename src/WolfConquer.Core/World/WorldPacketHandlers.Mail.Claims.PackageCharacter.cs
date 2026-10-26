using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryProjectMailboxPackageCharacter(LegacyMailboxClaimBaseline baseline,
        WorldSession after, out byte[] bytes)
    {
        bytes = baseline.Character.Bytes;
        var before = baseline.Owner.Expected;
        if ((before with { Character = after.Character, PlayerItems = after.PlayerItems, CombatGear = after.CombatGear }) != after ||
            (before.Character with { Money = after.Character.Money, ConquerPoints = after.Character.ConquerPoints,
                Attributes = after.Character.Attributes }) != after.Character ||
            after.Character.Money < before.Character.Money || after.Character.ConquerPoints < before.Character.ConquerPoints ||
            after.Character.ConquerPoints > uint.MaxValue ||
            !LegacyTradeEscrowClaims.TryRead(after.Character.Attributes, out var escrow, out _)) return false;
        _ = checked(after.Character.Money + (escrow?.Money ?? 0));
        if (after.Character.ConquerPoints + (escrow?.ConquerPoints ?? 0) > uint.MaxValue) return false;
        var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (after.Character.Money != before.Character.Money)
            changes.Add("Money", after.Character.Money.ToString(CultureInfo.InvariantCulture));
        if (after.Character.ConquerPoints != before.Character.ConquerPoints)
            changes.Add("ConquerPoints", after.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture));
        foreach (var key in new[] { "ClassExperience", "MsgNinja" })
        {
            var had = before.Character.Attributes.TryGetValue(key, out var oldValue);
            var has = after.Character.Attributes.TryGetValue(key, out var newValue);
            if (had != has) return false;
            if (had && !string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                if (!baseline.Character.Fields.TryGetValue(key, out var saved) ||
                    !string.Equals(saved, oldValue, StringComparison.Ordinal) ||
                    newValue!.IndexOfAny([(char)0, '\r', '\n']) >= 0) return false;
                changes.Add(key, newValue);
            }
        }
        var wanted = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changes) wanted[key] = value;
        if (wanted.Count != after.Character.Attributes.Count || !wanted.All(pair =>
            after.Character.Attributes.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal)))
            return false;
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
            if (equals <= 0 || !changes.TryGetValue(line[..equals].Trim(), out var value)) continue;
            if (!found.Add(line[..equals].Trim())) return false;
            parts[index] = line[..(equals + 1)] + value;
        }
        if (found.Count != changes.Count) return false;
        var projected = string.Concat(parts);
        if (!string.Equals(encoding.GetString(encoding.GetBytes(projected)), projected, StringComparison.Ordinal)) return false;
        var image = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (image.Length > CharacterSnapshotFileBaseline.MaximumBytes) return false;
        var parsed = CharacterSnapshotParser.ParseText(projected, after.Character.SourceFile);
        if (parsed.Uid != after.Character.Uid || parsed.Money != after.Character.Money ||
            parsed.ConquerPoints != after.Character.ConquerPoints || parsed.Class != after.Character.Class ||
            !LegacyTradeEscrowClaims.TryRead(parsed.Attributes, out var savedEscrow, out _) || savedEscrow != escrow ||
            changes.Any(pair => !parsed.Attributes.TryGetValue(pair.Key, out var value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal))) return false;
        bytes = image;
        return true;
    }
}
