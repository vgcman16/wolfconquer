using System.Globalization;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeNinjaRank
{
    private static LegacyNativeDbSafeDictionary<Type, Rank> ParsePersistenceGraph(
        string[] lines, CultureInfo culture)
    {
        if (lines.Length == 0 || lines[0].Any(char.IsControl) ||
            !lines[0].StartsWith("Count=", StringComparison.Ordinal) ||
            lines[0].IndexOf('=', 6) >= 0 ||
            !int.TryParse(lines[0].AsSpan(6), NumberStyles.Integer, culture, out var count) ||
            count is < 0 or > MaximumPersistenceRows || lines.Length != count + 1)
            throw InvalidPersistence("Ninja rank header or complete row count is invalid.");

        var graph = new LegacyNativeDbSafeDictionary<Type, Rank>((int)Type.Count);
        var seen = new HashSet<uint>[(int)Type.Count];
        for (var index = 0; index < (int)Type.Count; index++)
        {
            graph.Add((Type)index, new Rank((Type)index) { MaxItems = 10 });
            seen[index] = new HashSet<uint>();
        }

        for (var index = 1; index < lines.Length; index++)
        {
            var entry = ParsePersistenceEntry(lines[index], culture);
            var category = (int)entry.Type;
            if (!seen[category].Add(entry.UID))
                throw InvalidPersistence("Ninja rank file contains a duplicate UID in one category.");
            graph[entry.Type].UpdateItem(entry);
        }
        return graph;
    }

    private static Entry ParsePersistenceEntry(string line, CultureInfo culture)
    {
        if (line.Any(char.IsControl))
            throw InvalidPersistence("Ninja rank row contains a control character.");
        var fields = line.Split('/');
        if (fields.Length != 22 || fields[^1].Length != 0)
            throw InvalidPersistence("Ninja rank row must contain 21 fields and its trailing delimiter.");
        try
        {
            var uid = ParseUInt32(fields[0], culture);
            var category = ParseByte(fields[2], culture);
            if (uid == 0 || category >= (byte)Type.Count)
                throw InvalidPersistence("Ninja rank UID or category is invalid.");
            return new Entry
            {
                UID = uid,
                Name = fields[1],
                Type = (Type)category,
                TotalPoints = ParseUInt32(fields[3], culture),
                Class = ParseUInt32(fields[4], culture),
                Level = ParseByte(fields[5], culture),
                Mesh = ParseUInt32(fields[6], culture),
                HairStyle = ParseUInt32(fields[7], culture),
                Head = ParseUInt32(fields[8], culture),
                Garment = ParseUInt32(fields[9], culture),
                LeftWeapon = ParseUInt32(fields[10], culture),
                LefttWeaponAccessory = ParseUInt32(fields[11], culture),
                RightWeapon = ParseUInt32(fields[12], culture),
                RightWeaponAccessory = ParseUInt32(fields[13], culture),
                MountArmor = ParseUInt32(fields[14], culture),
                Armor = ParseUInt32(fields[15], culture),
                Wing = ParseUInt32(fields[16], culture),
                WingPlus = ParseUInt32(fields[17], culture),
                Title = ParseUInt32(fields[18], culture),
                Flag = ParseUInt32(fields[19], culture),
                GuildName = fields[20],
            };
        }
        catch (InvalidDataException) { throw; }
        catch (Exception error) when (error is FormatException or OverflowException)
        {
            throw InvalidPersistence("Ninja rank row contains an invalid numeric field.", error);
        }
    }

    private static uint ParseUInt32(string value, CultureInfo culture)
    {
        if (!uint.TryParse(value, NumberStyles.Integer, culture, out var parsed))
            throw InvalidPersistence("Ninja rank UInt32 field is invalid.");
        return parsed;
    }

    private static byte ParseByte(string value, CultureInfo culture)
    {
        if (!byte.TryParse(value, NumberStyles.Integer, culture, out var parsed))
            throw InvalidPersistence("Ninja rank Byte field is invalid.");
        return parsed;
    }
}
