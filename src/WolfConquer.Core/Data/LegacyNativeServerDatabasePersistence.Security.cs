using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    public static string GetSecurityPassword(LegacyNativeGameClientRuntime user)
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine(',');
        writeLine.Add(user.Player.SecurityPassword);
        writeLine.Add(user.Player.OnReset);
        writeLine.Add(user.Player.ResetSecurityPassowrd.Ticks);
        return writeLine.Close();
    }

    public static void LoadSecurityPassword(string line, LegacyNativeGameClientRuntime user)
    {
        LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, ',');
        user.Player.SecurityPassword = readLine.Read(0u);
        user.Player.OnReset = readLine.Read(0u);
        if (user.Player.OnReset == 1)
        {
            user.Player.ResetSecurityPassowrd = DateTime.FromBinary(readLine.Read(0L));
            if (DateTime.Now > user.Player.ResetSecurityPassowrd)
            {
                user.Player.OnReset = 0u;
                user.Player.SecurityPassword = 0u;
            }
        }
    }
}
