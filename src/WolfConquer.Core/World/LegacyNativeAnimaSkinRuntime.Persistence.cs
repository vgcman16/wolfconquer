using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeAnimaSkinRuntime
{
    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(isEquip);
        writeLine.Add(Items.Values.Count);
        foreach (Item value in Items.Values)
        {
            writeLine.Add(value.ID);
        }
        return writeLine.Close();
    }

    public void LLoad(string? line)
    {
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
        uint num = readLine.Read(0u);
        int num2 = readLine.Read(0);
        for (int i = 0; i < num2; i++)
        {
            uint num3 = readLine.Read(0u);
            AddSkin(num3, (num3 == num) ? true : false);
        }
    }
}
