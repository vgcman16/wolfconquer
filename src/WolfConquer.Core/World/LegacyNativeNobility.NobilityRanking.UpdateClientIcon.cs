using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNobility
{
    public partial class NobilityRanking
    {
        internal void UpdateClientIcon(LegacyNativePacket stream, LegacyNativeNobility user)
        {
            if (LegacyNativePoolClients.GamePoll!.TryGetValue(user.UID, out var value))
            {
                value.Player.NobilityRank = user.Rank;
                value.Send(stream.NobilityIconCreate(user));
            }
        }
    }
}
