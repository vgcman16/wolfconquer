using System.Globalization;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    internal sealed partial class MailboxLoadStage
    {
        private static Dictionary<LegacyNativeCombatGearRankTable.Type, LegacyNativeCombatGearRankTable.Rank> ReadRanks(
            string[] lines, CultureInfo culture)
        {
            if (lines.Length == 0 || !lines[0].StartsWith("Count=", StringComparison.Ordinal) ||
                lines[0].IndexOf('=', 6) >= 0 || !int.TryParse(lines[0].AsSpan(6), NumberStyles.Integer, culture, out var count) ||
                count is < 0 or > MaximumRows || lines.Length != count + 1)
                throw new InvalidDataException("CombatGear rank header or complete row count is invalid.");
            var ranks = new Dictionary<LegacyNativeCombatGearRankTable.Type, LegacyNativeCombatGearRankTable.Rank>();
            foreach (var type in Enum.GetValues<LegacyNativeCombatGearRankTable.Type>())
                ranks.Add(type, new LegacyNativeCombatGearRankTable.Rank(type) { MaxItems = 10 });
            for (var index = 1; index < lines.Length; index++)
            {
                var entry = RankEntry(lines[index], culture);
                if (!ranks.TryGetValue(entry.Type, out var owner))
                    throw new InvalidDataException("CombatGear rank type has no real owner.");
                // Genuine detached rank owner performs source replacement,
                // descending score ordering and top-ten trimming for every row.
                owner.UpdateItem(entry);
            }
            return ranks;
        }

        private static LegacyNativeCombatGearRankTable.Entry RankEntry(string line, CultureInfo culture)
        {
            var f = line.Split('/');
            if (f.Length != 22 && (f.Length != 23 || f[^1].Length != 0))
                throw new InvalidDataException("CombatGear rank row has missing or extra fields.");
            return new LegacyNativeCombatGearRankTable.Entry
            {
                UID = U(f[0], culture), Name = f[1], Type = (LegacyNativeCombatGearRankTable.Type)Convert.ToByte(f[2], culture),
                TotalPoints = U(f[3], culture), Class = U(f[4], culture), Level = Convert.ToByte(f[5], culture),
                Mesh = U(f[6], culture), HairStyle = U(f[7], culture), Head = U(f[8], culture), Garment = U(f[9], culture),
                LeftWeapon = U(f[10], culture), LefttWeaponAccessory = U(f[11], culture), RightWeapon = U(f[12], culture),
                RightWeaponAccessory = U(f[13], culture), MountArmor = U(f[14], culture), Armor = U(f[15], culture),
                Wing = U(f[16], culture), WingPlus = U(f[17], culture), Title = U(f[18], culture), Flag = U(f[19], culture),
                GuildName = f[20], WeaponWar = U(f[21], culture),
            };
        }
    }
}
