#nullable disable
using System.Linq;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearRankTable
{
    // The source deliberately ignores dbLocation; the real read owner captures the configured raw prefix.
    public static void Load(string dbLocation)
    {
        Create();
        using LegacyNativeDbRead read = new LegacyNativeDbRead("CombatGearRank.txt");
        if (read.Reader())
        {
            int count = read.Count;
            for (int i = 0; i < count; i++)
            {
                LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString(""), '/');
                Entry entry = new Entry
                {
                    UID = readLine.Read(0u),
                    Name = readLine.Read(""),
                    Type = (Type)readLine.Read((byte)0),
                    TotalPoints = readLine.Read(0u),
                    Class = readLine.Read(0u),
                    Level = readLine.Read((byte)0),
                    Mesh = readLine.Read(0u),
                    HairStyle = readLine.Read(0u),
                    Head = readLine.Read(0u),
                    Garment = readLine.Read(0u),
                    LeftWeapon = readLine.Read(0u),
                    LefttWeaponAccessory = readLine.Read(0u),
                    RightWeapon = readLine.Read(0u),
                    RightWeaponAccessory = readLine.Read(0u),
                    MountArmor = readLine.Read(0u),
                    Armor = readLine.Read(0u),
                    Wing = readLine.Read(0u),
                    WingPlus = readLine.Read(0u),
                    Title = readLine.Read(0u),
                    Flag = readLine.Read(0u),
                    GuildName = readLine.Read(""),
                    WeaponWar = readLine.Read(0u)
                };
                Ranks[entry.Type].UpdateItem(entry);
            }
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
