using System.Linq;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHairfaceStorageRuntime
{
    public bool Exists(uint ID, LegacyHairFaceStorageType type)
    {
        return Objects.Any(i => i.ID == ID && i.Type == type);
    }

    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/').Add(Objects.Length);
        LegacyNativeHairfaceStorageTable.Hairface[] array = Objects;
        foreach (LegacyNativeHairfaceStorageTable.Hairface hairface in array)
        {
            writeLine.Add(hairface.ID);
            writeLine.Add(hairface.EquippedColor);
            writeLine.Add(hairface.Colors!.Length);
            for (int j = 0; j < hairface.Colors.Length; j++)
            {
                writeLine.Add(hairface.Colors[j]);
            }
        }
        return writeLine.Close();
    }

    public void Load(string? line)
    {
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
        int num = readLine.Read(0);
        for (int i = 0; i < num; i++)
        {
            LegacyNativeHairfaceStorageTable.Hairface item = new LegacyNativeHairfaceStorageTable.Hairface
            {
                ID = readLine.Read(0u),
                EquippedColor = readLine.Read((byte)0),
            };
            item.Colors = new byte[readLine.Read(0)];
            for (int j = 0; j < item.Colors.Length; j++)
            {
                item.Colors[j] = readLine.Read((byte)0);
            }
            var hairface = LegacyNativeHairfaceStorageTable.Hairfaces.FirstOrDefault(z => z.ID == item.ID);
            if (hairface is not null)
            {
                item.Classes = hairface.Classes;
                item.Cost = hairface.Cost;
                item.Name = hairface.Name;
                item.RareLevel = hairface.RareLevel;
                item.RequiredVIPLevel = hairface.RequiredVIPLevel;
                item.Sex = hairface.Sex;
                item.Type = hairface.Type;

                Add(item, Send: false);
            }
        }
    }
}
