using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Complete native Player4768..4798 overloads, using actual View/Owner packet sends.
    internal void SendString(LegacyNativePacket stream, LegacyNativeMsgStringPacket.StringID id,
        bool SendScreen, params string[] args)
    {
        LegacyNativeMsgStringPacket msgStringPacket = new LegacyNativeMsgStringPacket();
        msgStringPacket.ID = id;
        msgStringPacket.UID = UID;
        msgStringPacket.Strings = args;
        if (SendScreen)
        {
            View.SendView(stream.StringPacketCreate(msgStringPacket), me: true);
        }
        else
        {
            Owner.Send(stream.StringPacketCreate(msgStringPacket));
        }
    }

    internal void SendString(LegacyNativePacket stream, LegacyNativeMsgStringPacket.StringID id,
        uint _uid, bool SendScreen, params string[] args)
    {
        LegacyNativeMsgStringPacket msgStringPacket = new LegacyNativeMsgStringPacket();
        msgStringPacket.ID = id;
        msgStringPacket.UID = _uid;
        msgStringPacket.Strings = args;
        if (SendScreen)
        {
            View.SendView(stream.StringPacketCreate(msgStringPacket), me: true);
        }
        else
        {
            Owner.Send(stream.StringPacketCreate(msgStringPacket));
        }
    }
}
