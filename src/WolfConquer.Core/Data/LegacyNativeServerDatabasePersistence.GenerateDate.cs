namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    public static string GenerateDate()
    {
        DateTime now = DateTime.Now;
        return now.Year + "and" + now.Month + "and" + now.Day + " and " + now.Hour + " and " + now.Minute + " and " + now.Second;
    }
}
