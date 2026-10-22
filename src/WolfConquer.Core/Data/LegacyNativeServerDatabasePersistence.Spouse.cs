using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native queued persistence leaf bodies, not an ExecuteLogin replacement.
internal static partial class LegacyNativeServerDatabasePersistence
{
    public static void DestroySpouse(LegacyNativeGameClientRuntime client)
    {
        if (client.Player.SpouseUID != 0)
        {
            LegacyNativeItemCounterShellIni iniFile = new("\\Users\\" + client.Player.SpouseUID + ".ini");
            iniFile.Write("Character", "SpouseUID", 0u);
            iniFile.WriteString("Character", "Spouse", "None");
            client.Player.SpouseUID = 0u;
        }
        client.Player.Spouse = "None";
        using LegacyNativeRecycledPacket recycledPacket = new();
        LegacyNativePacket stream = recycledPacket.GetStream();
        client.Player.SendString(stream, LegacyNativeMsgStringPacket.StringID.Spouse, false, "None");
    }

    public static void UpdateSpouse(LegacyNativeGameClientRuntime client)
    {
        if (client.Player.SpouseUID != 0)
        {
            LegacyNativeItemCounterShellIni iniFile = new("\\Users\\" + client.Player.SpouseUID + ".ini");
            iniFile.WriteString("Character", "Spouse", client.Player.Name);
        }
    }
}
