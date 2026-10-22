using System.Linq;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHairfaceStorageRuntime
{
    public bool Exists(uint ID, LegacyHairFaceStorageType type)
    {
        return Objects.Where(i => i.ID == ID && i.Type == type).Count() > 0;
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
            if (LegacyNativeHairfaceStorageTable.Hairfaces.Where(z => z.ID == item.ID).Count() > 0)
            {
                LegacyNativeHairfaceStorageTable.Hairface hairface =
                    LegacyNativeHairfaceStorageTable.Hairfaces.Where(z => z.ID == item.ID).FirstOrDefault()!;
                item.Classes = hairface.Classes;
                item.Cost = hairface.Cost;
                item.Name = hairface.Name;
                item.RareLevel = hairface.RareLevel;
                item.RequiredVIPLevel = hairface.RequiredVIPLevel;
                item.Sex = hairface.Sex;
                item.Type = hairface.Type;

                // Native Load calls Add(item, Send:false). Inline that exact reachable
                // branch: no owner lookup, send callback or public Add/send substitute.
                if (!Exists(item.ID, item.Type))
                {
                    items.Add(item);
                    objects = items.ToArray();
                }
            }
        }
    }
}
