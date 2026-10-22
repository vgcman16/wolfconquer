#nullable disable
using System.Linq;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeNinjaRank
{
    public static void Load()
    {
        Create();
        using LegacyNativeDbRead read = new LegacyNativeDbRead("NinjaRank.txt");
        if (read.Reader())
        {
            int count = read.Count;
            for (int i = 0; i < count; i++)
            {
                LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString("/"), '/');
                Entry entry = new Entry();
                entry.UID = readLine.Read(0u);
                entry.Name = readLine.Read("");
                entry.Type = (Type)readLine.Read((byte)0);
                entry.TotalPoints = readLine.Read(0u);
                entry.Class = readLine.Read(0u);
                entry.Level = readLine.Read((byte)0);
                entry.Mesh = readLine.Read(0u);
                entry.HairStyle = readLine.Read(0u);
                entry.Head = readLine.Read(0u);
                entry.Garment = readLine.Read(0u);
                entry.LeftWeapon = readLine.Read(0u);
                entry.LefttWeaponAccessory = readLine.Read(0u);
                entry.RightWeapon = readLine.Read(0u);
                entry.RightWeaponAccessory = readLine.Read(0u);
                entry.MountArmor = readLine.Read(0u);
                entry.Armor = readLine.Read(0u);
                entry.Wing = readLine.Read(0u);
                entry.WingPlus = readLine.Read(0u);
                entry.Title = readLine.Read(0u);
                entry.Flag = readLine.Read(0u);
                entry.GuildName = readLine.Read("");
                Ranks[entry.Type].UpdateItem(entry);
            }
        }
    }

    public static void Save()
    {
        using LegacyNativeDbWrite write = new LegacyNativeDbWrite("NinjaRank.txt");
        Rank[] array = Ranks.GetValues().ToArray();
        Rank[] array2 = array;
        foreach (Rank rank in array2)
        {
            Entry[] array3 = rank.RankingList.Values.ToArray();
            Entry[] array4 = array3;
            foreach (Entry entry in array4)
            {
                LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
                writeLine.Add(entry.UID).Add(entry.Name).Add(unchecked((byte)entry.Type))
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
                    .Add(entry.GuildName);
                write.Add(writeLine.Close());
            }
        }
        write.Execute(LegacyNativeDbWriteMode.Open);
    }
}
