using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native title comma-record persistence methods. These do not install
// SaveClient/ExecuteLogin callers or replace the snapshot host's title handlers.
internal static partial class LegacyNativeServerDatabasePersistence
{
    public static string GetSpecialTitles(LegacyNativeGameClientRuntime user)
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine(',');
        writeLine.Add((uint)user.Player.SpecialTitles.Count);
        foreach (LegacyNativeMsgTitleStorage.TitleType specialTitle in user.Player.SpecialTitles)
        {
            writeLine.Add((uint)specialTitle);
            if (user.Player.SpecialTitleID / 10000 == (uint)specialTitle || user.Player.SpecialWingID / 10000 == (uint)specialTitle)
            {
                writeLine.Add((byte)1);
            }
            else
            {
                writeLine.Add((byte)0);
            }
        }
        return writeLine.Close();
    }

    public static void LoadSpecialTitles(LegacyNativeGameClientRuntime user, string line)
    {
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, ',');
        uint num = readLine.Read(0u);
        for (int i = 0; i < num; i++)
        {
            uint num2 = readLine.Read(0u);
            uint num3 = readLine.Read((byte)0);
            user.Player.SpecialTitles.Add((LegacyNativeMsgTitleStorage.TitleType)num2);
            if (num3 == 1 && LegacyNativeTitleStorage.Titles.TryGetValue(num2, out var value))
            {
                if (value.ID < 4001)
                {
                    user.Player.SpecialTitleScore = value.Score;
                    user.Player.SpecialTitleID = value.ID * 10000 + value.SubID;
                }
                else
                {
                    user.Player.SpecialWingID = value.ID * 10000 + value.SubID;
                }
            }
        }
    }
}
