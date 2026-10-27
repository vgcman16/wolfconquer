using System.Text;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    private static List<PrestigeSaveRow> CapturePrestigeSaveRows()
    {
        var ranks = Ranks ?? throw new InvalidOperationException("Prestige Save requires a ranking graph.");
        var rows = new List<PrestigeSaveRow>();
        var identities = new HashSet<uint>();
        // Enumerate the real graph. GetValues may be a stale array and must not
        // hide a current row or owner. This path does not repair that cache.
        foreach (var bucket in ranks)
        {
            if ((uint)bucket.Key > 11 || bucket.Value is not { } rank || rank._Type != bucket.Key)
            {
                throw new InvalidOperationException("Prestige Save found an invalid ranking owner.");
            }
            if (bucket.Key == Type.World)
            {
                continue;
            }
            if (rank.Items is null)
            {
                throw new InvalidOperationException("Prestige Save requires class ranking items.");
            }
            foreach (var item in rank.Items)
            {
                rows.Add(CapturePrestigeSaveRow(bucket.Key, item.Key, item.Value, identities));
            }
        }
        return rows;
    }

    private static PrestigeSaveRow CapturePrestigeSaveRow(
        Type category, uint key, Entry? entry, HashSet<uint> identities)
    {
        if (entry is null)
        {
            throw new InvalidOperationException("Prestige Save requires a ranking row.");
        }
        var uid = entry.UID;
        var rowCategory = entry.type;
        if (uid == 0 || uid != key || rowCategory != category)
        {
            throw new InvalidOperationException("Prestige Save found an invalid ranking row identity or category.");
        }
        if (!identities.Add(uid))
        {
            throw new InvalidOperationException("Prestige Save found a duplicate persisted character UID.");
        }
        var name = RequirePrestigeSaveText(entry.Name, "character name", allowEmpty: false);
        var guildName = RequirePrestigeSaveText(entry.GuildName, "guild name", allowEmpty: true);
        var points = entry.Points
            ?? throw new InvalidOperationException("Prestige Save requires a present component array.");
        uint[] numbers =
        [
            uid, (uint)rowCategory, entry.Class, entry.Level, entry.Mesh, entry.TotalPoints,
            entry.HairStyle, entry.Head, entry.Garment, entry.LeftWeapon, entry.LefttWeaponAccessory,
            entry.RightWeapon, entry.RightWeaponAccessory, entry.MountArmor, entry.Armor,
            entry.Wing, entry.WingPlus, entry.Title, entry.Flag,
        ];
        return new PrestigeSaveRow(numbers, name, guildName, (uint[])points.Clone());
    }

    private static string RequirePrestigeSaveText(string? value, string name, bool allowEmpty)
    {
        if (value is null || (!allowEmpty && string.IsNullOrWhiteSpace(value)) ||
            value.Any(character => character == '/' || char.IsControl(character)))
        {
            throw new InvalidOperationException($"Prestige Save found an invalid {name}.");
        }
        try
        {
            _ = PrestigeSaveTextEncoding.GetByteCount(value);
        }
        catch (EncoderFallbackException error)
        {
            throw new InvalidOperationException($"Prestige Save found invalid Unicode in its {name}.", error);
        }
        return value;
    }
}
