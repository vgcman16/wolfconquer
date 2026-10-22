#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native Server.SendGlobalPacket only. No pool publication, socket pump or startup.
internal static class LegacyNativeServerBroadcast
{
    public static void SendGlobalPacket(LegacyNativePacket data)
    {
        LegacyNativeGameClientRuntime[] array = LegacyNativePoolClients.GamePoll.Values.ToArray();
        foreach (LegacyNativeGameClientRuntime value in LegacyNativePoolClients.GamePoll.Values)
        {
            value.Send(data);
        }
    }
}
