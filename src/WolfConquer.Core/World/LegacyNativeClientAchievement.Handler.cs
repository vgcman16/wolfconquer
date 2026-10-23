using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClientAchievement
{
    // Whole source receiver body, deliberately unregistered: no Packet(2307).
    public static void AchievementHandler(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        stream.GetAchievement(out var action, out var UID, out var Flag);
        switch (action)
        {
            case TypeAchievement.CheakView:
            {
                ILegacyNativeMapObj obj = null!;
                if (client.Player.View.TryGetValue(UID, out obj, LegacyNativeMapObjectType.Player) && obj is LegacyNativePlayerRuntime player)
                {
                    client.Send(stream.AchievementCreate(TypeAchievement.CheakView, player.UID, player.Achievement!.MyFlags()));
                }
                break;
            }
            case TypeAchievement.SendQueue:
                client.Player.Achievement!.CreateFlaID(unchecked((int)Flag), client, stream);
                break;
        }
    }
}
