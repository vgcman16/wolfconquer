using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClientAchievement
{
    private void ShowScreen(int flag, LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        uint flag2 = (uint)(10100 + 100 * unchecked((byte)(flag / 32)) + unchecked((byte)(flag % 32)) + 1);
        client.Send(stream.AchievementCreate(TypeAchievement.SendQueue, client.Player.UID, flag2));
        string text = client.Player.Name + " received [Achievement " + flag2 + "]! ";
        if (client.Player.MyGuild != null)
        {
            client.Player.MyGuild.SendMessajGuild(text);
            return;
        }
        LegacyNativeMsgMessage msgMessage = new LegacyNativeMsgMessage(text, client.Player.Name, LegacyNativeMsgMessage.MsgColor.red, LegacyNativeMsgMessage.ChatMode.Talk);
        client.Send(msgMessage.GetArray(stream));
    }
}
