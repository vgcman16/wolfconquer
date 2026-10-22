using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCollectionStorageRuntime
{
    public void Load(string? line)
    {
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
        int num = readLine.Read(0);
        for (int i = 0; i < num; i++)
        {
            uint num2 = readLine.Read(0u);
            uint amount = readLine.Read(0u);
            Scraps.Add(num2, new Scrap
            {
                Amount = amount,
                Type = num2
            });
        }
        int num3 = readLine.Read(0);
        for (int j = 0; j < num3; j++)
        {
            uint num4 = readLine.Read(0u);
            uint activate = readLine.Read(0u);
            uint amount2 = readLine.Read(0u);
            uint remainingTime = readLine.Read(0u);
            uint f = readLine.Read(0u);
            Items.Add(num4, new Item
            {
                Amount = amount2,
                Type = num4,
                Activate = activate,
                RemainingTime = remainingTime,
                F5 = f
            });
        }
    }

    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(Scraps.Count);
        foreach (Scrap value in Scraps.Values)
        {
            writeLine.Add(value.Type);
            writeLine.Add(value.Amount);
        }
        writeLine.Add(Items.Count);
        foreach (Item value2 in Items.Values)
        {
            writeLine.Add(value2.Type);
            writeLine.Add(value2.Activate);
            writeLine.Add(value2.Amount);
            writeLine.Add(value2.RemainingTime);
            writeLine.Add(value2.F5);
        }
        return writeLine.Close();
    }
}
