using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Whole native GameClient1131..1174; independent calculation, NOT GainExpBall.
    // It deliberately reads the original/current level row again after projecting
    // nextlevel and retains missing-key faults, the byte loop and signed product.
    public ulong CalcExpBall(double amount, out ushort nextlevel)
    {
        if (Player.Level >= 140)
        {
            nextlevel = 0;
            return 0uL;
        }
        LegacyNativeDbLevExp dBLevExp = LegacyNativePoolLevelInfo.LevelInfo[LegacyNativeDbLevExp.Sort.User][(byte)Player.Level];
        if (dBLevExp == null)
        {
            nextlevel = 0;
            return 0uL;
        }
        double num = (double)unchecked((long)Player.Experience * (long)dBLevExp.UpLevTime) / (double)dBLevExp.Experience;
        num += amount;
        byte b = (byte)Player.Level;
        int upLevTime = dBLevExp.UpLevTime;
        while (b < 140 && !(num < (double)upLevTime))
        {
            num -= (double)upLevTime;
            b++;
            dBLevExp = LegacyNativePoolLevelInfo.LevelInfo[LegacyNativeDbLevExp.Sort.User][b];
            if (dBLevExp == null)
            {
                break;
            }
            upLevTime = dBLevExp.UpLevTime;
        }
        if (upLevTime < 1)
        {
            nextlevel = b;
            return 0uL;
        }
        num /= (double)upLevTime;
        dBLevExp = LegacyNativePoolLevelInfo.LevelInfo[LegacyNativeDbLevExp.Sort.User][(byte)Player.Level];
        if (dBLevExp == null)
        {
            nextlevel = b;
            return 0uL;
        }
        ulong result = LegacyNativeExperienceConversions.UInt64(num * (double)dBLevExp.Experience);
        nextlevel = b;
        return result;
    }
}
