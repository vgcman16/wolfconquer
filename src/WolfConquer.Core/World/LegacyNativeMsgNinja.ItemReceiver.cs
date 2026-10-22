#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    public partial class MsgNinjaItem
    {
        // Complete native receiver body only. No Packet attribute, dispatcher registration,
        // GamePoll startup publication or modern wire-authority claim is installed.
        private static void Process(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
        {
            stream.GetNinjaItem(out var pQuery);
            TypeID type = pQuery.Type;
            TypeID typeID = type;
            if (typeID == TypeID.View && LegacyNativePoolClients.GamePoll.TryGetValue(pQuery.UID, out var value))
            {
                Create(client, value.MyNinja, TypeID.View, value.Player.UID);
                MsgNinjaUser.Create(client, value.MyNinja, value.Player.UID);
            }
        }
    }
}
