using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Complete native GameClient1518..1541; internal only because its genuine
    // message/enum owner is internal. Defaults and all four branches are literal.
    internal void SendSysMesage(string Messaj,
        LegacyNativeMsgMessage.ChatMode ChatType = LegacyNativeMsgMessage.ChatMode.TopLeftSystem,
        LegacyNativeMsgMessage.MsgColor color = LegacyNativeMsgMessage.MsgColor.red,
        bool SendScren = false, bool self = false)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        if (!self)
        {
            if (SendScren)
            {
                Player.View.SendView(new LegacyNativeMsgMessage(Messaj, color, ChatType).GetArray(stream), me: true);
            }
            else
            {
                Send(new LegacyNativeMsgMessage(Messaj, color, ChatType).GetArray(stream));
            }
        }
        else if (SendScren)
        {
            Player.View.SendView(new LegacyNativeMsgMessage(Messaj, Player.Name, color, ChatType).GetArray(stream), me: true);
        }
        else
        {
            Send(new LegacyNativeMsgMessage(Messaj, Player.Name, color, ChatType).GetArray(stream));
        }
    }
}
