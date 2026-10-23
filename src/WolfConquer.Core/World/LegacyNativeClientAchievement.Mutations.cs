using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClientAchievement
{
    public void CreateFlaID(int id, LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        AddFlag((id / 100 % 100 - 1) * 32 + (id % 100 - 1), client, stream);
    }

    public void AddFlag(int flag, LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        if (!BitVector32.Contain(flag))
        {
            BitVector32.Add(flag);
            ShowScreen(flag, client, stream);
            Send(client, stream);
        }
    }

    public void Send(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        client.Send(stream.AchievementCreate(TypeAchievement.BigHash, client.Player.UID, client.Player.Achievement!.MyFlags()));
    }
}
