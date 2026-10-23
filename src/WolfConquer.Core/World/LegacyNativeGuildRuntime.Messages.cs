using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGuildRuntime
{
    // Complete native Guild.SendMessajGuild820..833, no membership/host adoption.
    public void SendMessajGuild(string Messaj, LegacyNativeMsgMessage.ChatMode ChatType = LegacyNativeMsgMessage.ChatMode.Guild,
        LegacyNativeMsgMessage.MsgColor color = LegacyNativeMsgMessage.MsgColor.yellow)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        foreach (LegacyNativeGameClientRuntime value in LegacyNativePoolClients.GamePoll!.Values)
        {
            if (value.Player.GuildID == Info.GuildID)
            {
                value.Send(new LegacyNativeMsgMessage(Messaj, color, ChatType).GetArray(stream));
            }
        }
    }
}
