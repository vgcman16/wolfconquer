namespace WolfConquer.Core.World;

internal static partial class LegacyNativeRoleCore
{
    internal static int CreateTimer(int year, int month, int day)
    {
        return year * 10000 + month * 100 + day;
    }

    internal static int CreateTimer(DateTime timer)
    {
        return timer.Year * 10000 + timer.Month * 100 + timer.Day;
    }

    internal static DateTime GetTimer(int Timer)
    {
        int num = Timer / 10000;
        int num2 = Timer / 100 - num * 100;
        int day = Timer - num * 10000 - num2 * 100;
        return new DateTime(num, num2, day);
    }
}
