using System.Collections.Generic;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeInnerPowerTable
{
    // Complete native InnerPowerTable153..201. No transaction, normalization or UpdateStatus added.
    public static void Save()
    {
        using LegacyNativeDbWrite write = new LegacyNativeDbWrite("InnerPower.txt");
        ICollection<LegacyNativeInnerPowerRuntime> values = LegacyNativeInnerPowerRuntime.InnerPowerPolle.Values;
        foreach (LegacyNativeInnerPowerRuntime item in values)
        {
            write.Add(item.ToString());
        }
        write.Execute(LegacyNativeDbWriteMode.Open);
    }

    public static void Load()
    {
        using LegacyNativeDbRead read = new LegacyNativeDbRead("InnerPower.txt");
        if (!read.Reader())
        {
            return;
        }
        int count = read.Count;
        for (uint num = 0u; num < count; num++)
        {
            LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString(""), '/');
            LegacyNativeInnerPowerRuntime innerPower = new LegacyNativeInnerPowerRuntime(readLine.Read("")!, readLine.Read(0u));
            innerPower.Potency = readLine.Read(0);
            int num2 = readLine.Read(0);
            for (int i = 0; i < num2; i++)
            {
                LegacyNativeInnerPowerRuntime.Stage stage = innerPower.Stages[i];
                stage.ID = readLine.Read((ushort)0);
                stage.UnLocked = readLine.Read((byte)0) == 1;
                int num3 = readLine.Read(0);
                for (int j = 0; j < num3; j++)
                {
                    LegacyNativeInnerPowerRuntime.Stage.NeiGong neiGong = stage.NeiGongs[j];
                    neiGong.ID = readLine.Read((byte)0);
                    neiGong.Score = readLine.Read((byte)0);
                    neiGong.Unlocked = readLine.Read((byte)0) == 1;
                    neiGong.level = readLine.Read((byte)0);
                    neiGong.Complete = readLine.Read((byte)0) == 1;
                }
            }
            LegacyNativeInnerPowerRuntime.InnerPowerPolle.TryAdd(innerPower.UID, innerPower);
            LegacyNativeInnerPowerRuntime.InnerPowerRank.UpdateRank(innerPower);
        }
    }
}
