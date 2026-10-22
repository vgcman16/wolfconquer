namespace WolfConquer.Core.World;

// Native Program.ServerConfig declared storage only (Program.cs27..66), with public-export defaults.
// No shell.ini producer, startup, reset helper or host registration is installed.
// The original deployment key and promotional destinations are deliberately absent.
// These empty values are unconfigured storage, not working authentication or link defaults;
// a caller that needs them must explicitly supply and validate its own configuration.
internal static class LegacyNativeServerConfig
{
    public static string LogginKey = "";

    public static string CO2Folder = "";

    public static string XtremeTopLink = "";

    public static string Descord = "";

    public static uint ServerID = 0u;

    public static uint ConquerorWinner = 0u;

    public static string IPAddres = "";

    public static ushort AuthPort = 0;

    public static ushort GamePort = 0;

    public static string ServerName = "Test";

    public static ushort Port_BackLog;

    public static ushort Port_ReceiveSize = 16384;

    public static ushort Port_SendSize = 16384;

    public static string DbLocation = "";

    public static uint ExpRateSpell = 1000u;

    public static uint ExpRateProf = 1000u;

    public static uint UserExpRate = 1000u;

    public static uint PhysicalDamage = 100u;

    public static bool IsInterServer = false;
}
