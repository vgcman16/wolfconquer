using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(Flag);
        writeLine.Add(Exp);
        writeLine.Add(Fire);
        writeLine.Add(Water);
        writeLine.Add(Earth);
        writeLine.Add(Wind);
        writeLine.Add(Lightning);
        writeLine.Add(tmp_Fire);
        writeLine.Add(tmp_Water);
        writeLine.Add(tmp_Earth);
        writeLine.Add(tmp_Wind);
        writeLine.Add(tmp_Lightning);
        writeLine.Add(Score);
        writeLine.Add(FireMastery);
        writeLine.Add(WaterMastery);
        writeLine.Add(EarthMastery);
        writeLine.Add(WindMastery);
        writeLine.Add(LightningMastery);
        writeLine.Add(UnlockMastery);
        writeLine.Add(Items.Count);
        foreach (Item value in Items.Values)
        {
            writeLine.Add(value.ItemID);
            writeLine.Add(value.Level);
            writeLine.Add(value.Position);
            writeLine.Add(value.Exp);
            writeLine.Add(value.UK);
        }
        return writeLine.Close();
    }

    public void Load(string? line)
    {
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
        Flag = readLine.Read(0uL);
        Exp = readLine.Read(0u);
        Fire = readLine.Read(0u);
        Water = readLine.Read(0u);
        Earth = readLine.Read(0u);
        Wind = readLine.Read(0u);
        Lightning = readLine.Read(0u);
        tmp_Fire = readLine.Read(0u);
        tmp_Water = readLine.Read(0u);
        tmp_Earth = readLine.Read(0u);
        tmp_Wind = readLine.Read(0u);
        tmp_Lightning = readLine.Read(0u);
        readLine.Read(0u);
        FireMastery = readLine.Read(0u);
        WaterMastery = readLine.Read(0u);
        EarthMastery = readLine.Read(0u);
        WindMastery = readLine.Read(0u);
        LightningMastery = readLine.Read(0u);
        UnlockMastery = readLine.Read(0u);
        int num = readLine.Read(0);
        for (int i = 0; i < num; i++)
        {
            Item item = new Item();
            item.ItemID = readLine.Read(0u);
            item.Level = readLine.Read(0u);
            item.Position = readLine.Read(0u);
            item.Exp = readLine.Read(0u);
            item.UK = readLine.Read(0u);
            if (!Items.ContainsKey(item.ItemID))
            {
                Items.Add(item.ItemID, item);
            }
        }
    }
}
