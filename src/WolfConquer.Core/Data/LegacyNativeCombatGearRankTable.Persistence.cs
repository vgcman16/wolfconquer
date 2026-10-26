#nullable disable
using System.Linq;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearRankTable
{
    // The source deliberately ignores dbLocation; the real read owner captures the configured raw prefix.
    public static void Load(string dbLocation)
    {
        lock (LegacyNativeCombatGearTable.MailboxLoadGate)
        {
            var path = WolfConquer.Core.World.LegacyNativeServerConfig.DbLocation + "CombatGearRank.txt";
            var prepared = LegacyNativeCombatGearTable.MailboxLoadStage.ReadRanksFile(path);
            Ranks = prepared;
        }
    }
    public static void Save()
    {
        using LegacyNativeDbWrite write = new LegacyNativeDbWrite("CombatGearRank.txt");
        Rank[] array = Ranks.Values.ToArray();
        Rank[] array2 = array;
        foreach (Rank rank in array2)
        {
            Entry[] array3 = rank.RankingList.Values.ToArray();
            Entry[] array4 = array3;
            foreach (Entry entry in array4)
            {
                LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
                writeLine.Add(entry.UID).Add(entry.Name).Add((byte)entry.Type)
                    .Add(entry.TotalPoints)
                    .Add(entry.Class)
                    .Add(entry.Level)
                    .Add(entry.Mesh)
                    .Add(entry.HairStyle)
                    .Add(entry.Head)
                    .Add(entry.Garment)
                    .Add(entry.LeftWeapon)
                    .Add(entry.LefttWeaponAccessory)
                    .Add(entry.RightWeapon)
                    .Add(entry.RightWeaponAccessory)
                    .Add(entry.MountArmor)
                    .Add(entry.Armor)
                    .Add(entry.Wing)
                    .Add(entry.WingPlus)
                    .Add(entry.Title)
                    .Add(entry.Flag)
                    .Add(entry.GuildName)
                    .Add(entry.WeaponWar);
                write.Add(writeLine.Close());
            }
        }
        write.Execute(LegacyNativeDbWriteMode.Open);
    }
}
